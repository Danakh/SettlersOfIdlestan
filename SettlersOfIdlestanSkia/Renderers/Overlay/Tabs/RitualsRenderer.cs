using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Magic;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;

/// <summary>
/// Écran Rituels : liste des rituels connus, lancement/arrêt, réglage de la puissance,
/// coûts en cristaux et capacité des Tours de Mages.
/// </summary>
public sealed class RitualsRenderer : IDisposable
{
    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;
    private readonly TargetSelectionService? _targetSelectionService;

    private bool _disposed;

    public RitualsRenderer(GameControllerService gameControllerService, LocalizationService localization,
        TargetSelectionService? targetSelectionService = null)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _targetSelectionService = targetSelectionService;
    }

    private string[] BuildPowerTooltipLines(SettlersOfIdlestan.Controller.Magic.MagicController magic)
    {
        var lines = new List<string>();
        double towerBonusPercent = magic.MageTowerTotalLevel
            * SettlersOfIdlestan.Controller.Magic.MagicController.MageTowerPowerBonusPerLevel * 100.0;
        lines.Add(_localization.GetFormated("rituals_power_tooltip_towers", $"{towerBonusPercent:0.#}"));

        double totalExact = magic.TotalPowerBudgetExact;
        double otherPercent = (totalExact - 1.0 - towerBonusPercent / 100.0) * 100.0;
        if (Math.Abs(otherPercent) > 0.01)
            lines.Add(_localization.GetFormated("rituals_power_tooltip_other", $"{otherPercent:0.#}"));

        lines.Add(_localization.GetFormated("rituals_power_tooltip_total", $"{totalExact:0.#}"));
        return lines.ToArray();
    }

    /// <summary>
    /// Construit les lignes d'infobulle à partir des mêmes listes de sources (clé, taux) que l'infobulle
    /// de la barre de ressources — même format d'affichage, mêmes clés de localisation par source, pour
    /// garantir que les deux infobulles restent cohérentes entre elles par construction.
    /// </summary>
    private string[] BuildCrystalTooltipLines(
        System.Collections.Generic.List<(string SourceKey, double Rate)> gains,
        System.Collections.Generic.List<(string SourceKey, double Rate)> losses)
    {
        var lines = new List<string>();
        foreach (var (sourceKey, rate) in gains.OrderByDescending(g => g.Rate))
            lines.Add($"{_localization.Get(sourceKey)} : +{rate:0.#}/s");
        foreach (var (sourceKey, rate) in losses.OrderByDescending(l => l.Rate))
            lines.Add($"{_localization.Get(sourceKey)} : -{rate:0.#}/s");
        if (lines.Count == 0)
            lines.Add(_localization.Get("rituals_crystals_tooltip_none"));
        return lines.ToArray();
    }

    /// <summary>
    /// Formate le bonus total (Value × puissance) de chaque modificateur d'un rituel actif,
    /// joint par ", " — à distinguer du texte de description qui décrit le bonus par point de puissance.
    /// </summary>
    private string FormatRitualTotalBonus(RitualDefinition def, int power)
    {
        var parts = new List<string>();
        foreach (var mod in def.ModifiersPerPower)
        {
            double total = mod.Value * power;
            parts.Add(mod.Category switch
            {
                Modifier.ECategory.HARVEST_SPEED => string.IsNullOrEmpty(mod.SubCategory)
                    ? $"+{(int)(total * 100)}% {_localization.Get("prestige_tooltip_harvest_speed")}"
                    : $"+{(int)(total * 100)}% {_localization.Get($"building_{mod.SubCategory.ToLower()}_name")} {_localization.Get("prestige_tooltip_harvest_speed")}",
                Modifier.ECategory.HARVEST_PRODUCTION_BONUS => $"+{(int)total}% {_localization.Get("ritual_tooltip_double_chance")}",
                Modifier.ECategory.PRODUCTION_BUILDING_SPEED => $"+{(int)(total * 100)}% {_localization.Get("ritual_tooltip_production_building_speed")}",
                Modifier.ECategory.CITY_MAX_SOLDIERS_BONUS => $"+{(int)total} {_localization.Get("prestige_tooltip_city_max_soldiers")}",
                Modifier.ECategory.UNIT_PRODUCTION_SPEED => $"+{(int)(total * 100)}% {_localization.Get("prestige_tooltip_unit_speed")}",
                Modifier.ECategory.CITY_DEFENSE => $"+{(int)total} {_localization.Get("prestige_tooltip_city_defense")}",
                Modifier.ECategory.CITY_DEFENSE_REGEN_SPEED => $"+{(int)(total * 100)}% {_localization.Get("prestige_tooltip_city_defense_regen")}",
                Modifier.ECategory.RESEARCH_PRODUCTION_SPEED => $"+{(int)(total * 100)}% {_localization.Get("prestige_tooltip_research_production_speed")}",
                Modifier.ECategory.TEMPLE_MONSTER_DAMAGE_PER_SECOND => $"+{(int)total} {_localization.Get("prestige_tooltip_temple_monster_damage")}",
                _ => $"+{total:0.#} {mod.Category}",
            });
        }
        return string.Join(", ", parts);
    }

    /// <summary>Formate une durée en ticks (100 ticks = 1 s) dans l'unité la plus grande atteinte, 1 décimale.</summary>
    private static string FormatSpellDuration(long ticks)
    {
        double seconds = ticks / 100.0;
        if (seconds >= 3600) return $"{seconds / 3600:0.#}h";
        if (seconds >= 60) return $"{seconds / 60:0.#}m";
        return $"{seconds:0.#}s";
    }

    // ── Pont vers l'hôte Avalonia ─────────────────────────────────────────────

    /// <summary>
    /// Instantané de l'onglet pour une vue portée par l'hôte. Le <c>MagicController</c> reste la
    /// seule source des coûts, du bonus courant et des raisons de blocage : rien n'est recalculé ici.
    /// </summary>
    /// <param name="isVisible">L'onglet Rituels est-il actif ? La règle appartient à
    /// <c>OverlayRenderer</c>, qui détient l'onglet courant.</param>
    public RitualsSnapshot GetSnapshot(bool isVisible)
    {
        if (_disposed || !isVisible) return RitualsSnapshot.Hidden;

        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return RitualsSnapshot.Hidden;

        var magic = _gameControllerService.MainGameController.MagicController;

        int crystals = civ.GetResourceQuantity(Resource.Crystal);
        var (crystalGains, crystalLosses) = magic.GetCrystalGainsAndLosses();
        double net = crystalGains.Sum(g => g.Rate) - crystalLosses.Sum(l => l.Rate);
        string ratePart = $"{(net >= 0 ? "+" : "")}{net:0.#}";

        var rituals = new List<RitualRowSnapshot>();
        foreach (var def in magic.GetKnownRituals())
        {
            var active = magic.GetActiveRitual(def.Id);
            bool isActive = active != null;

            rituals.Add(new RitualRowSnapshot(
                Key: def.Id.ToString(),
                Name: _localization.Get(def.NameKey),
                Description: _localization.Get(def.DescKey),
                CostText: isActive
                    ? _localization.GetFormated("ritual_upkeep_cost", magic.GetUpkeepCost(def, active!.Power))
                    : _localization.GetFormated("ritual_launch_cost", magic.GetLaunchCost(def, 1)),
                BonusText: isActive
                    ? _localization.GetFormated("ritual_bonus_current", FormatRitualTotalBonus(def, active!.Power))
                    : null,
                IsActive: isActive,
                ButtonLabel: _localization.Get(isActive ? "ritual_button_stop" : "ritual_button_launch"),
                IsButtonEnabled: isActive || magic.CanLaunchRitual(def.Id),
                Power: active?.Power ?? 0,
                CanIncreasePower: isActive && magic.CanIncreaseRitualPower(def.Id),
                IsAutomated: magic.IsRitualAutomated(def.Id),
                CanAutomate: magic.IsDivineRitualsActive,
                AutoLabel: _localization.Get("ritual_auto_label"),
                AutoTooltip: _localization.Get("ritual_auto_tooltip")));
        }

        var spells = new List<SpellRowSnapshot>();
        foreach (var def in magic.GetKnownSpells())
        {
            int spellCost = magic.GetSpellCost(def);
            bool canCast = magic.CanCastSpell(def.Id);
            string? blockedReasonKey = canCast ? null : magic.GetSpellBlockedReasonKey(def.Id);

            int stacks = magic.GetSpellExhaustionStacks(def.Id);
            string description = _localization.Get(def.DescKey) + "\n"
                + _localization.GetFormated("spell_exhaustion_desc", stacks, magic.GetSpellCostMultiplier(def.Id));
            string cooldownTooltip = _localization.GetFormated("spell_cooldown_tooltip",
                FormatSpellDuration(magic.GetSpellCooldownRemainingTicks(def.Id)), FormatSpellDuration(def.CooldownTicks));

            int charges = magic.GetSpellCharges(def.Id);
            int maxCharges = magic.GetSpellMaxCharges(def.Id);
            string chargesTooltip = _localization.GetFormated("spell_charges_tooltip", charges, maxCharges);

            spells.Add(new SpellRowSnapshot(
                Key: def.Id.ToString(),
                Name: _localization.Get(def.NameKey),
                Description: description,
                CostText: def.TargetKind switch
                {
                    SpellTargetKind.AllyCity => _localization.GetFormated("spell_cast_cost_troops", spellCost, def.TroopReward),
                    SpellTargetKind.BuildableVertex => _localization.GetFormated("spell_cast_cost_city", spellCost),
                    SpellTargetKind.VoidRoad => _localization.GetFormated("spell_cast_cost_void_bridge", spellCost),
                    _ => _localization.GetFormated("spell_cast_cost", spellCost, def.GoldReward),
                },
                WarningText: blockedReasonKey != null ? _localization.Get(blockedReasonKey) : null,
                ButtonLabel: _localization.Get("spell_button_cast"),
                CanCast: canCast,
                ExhaustionStacks: stacks,
                CooldownRatio: magic.GetSpellCooldownRatio(def.Id),
                CooldownTooltip: cooldownTooltip,
                Charges: charges,
                MaxCharges: maxCharges,
                ChargesTooltip: chargesTooltip));
        }

        return new RitualsSnapshot(
            IsVisible: true,
            Title: _localization.Get("tab_rituals"),
            PowerLabel: _localization.GetFormated("rituals_power_max_label", magic.TotalPowerBudget),
            PowerTooltip: BuildPowerTooltipLines(magic),
            CrystalsLabel: _localization.GetFormated("rituals_crystals_label", crystals, ratePart),
            CrystalsTooltip: BuildCrystalTooltipLines(crystalGains, crystalLosses),
            NoRitualsMessage: rituals.Count == 0 ? _localization.Get("rituals_none_known") : null,
            Rituals: rituals,
            SpellsHeader: _localization.Get("rituals_spells_header"),
            Spells: spells);
    }

    /// <summary>Lance ou arrête un rituel depuis une vue portée par l'hôte.</summary>
    public void ToggleRitualFromHost(string key)
    {
        if (!Enum.TryParse<RitualId>(key, out var id)) return;
        var magic = _gameControllerService.MainGameController.MagicController;
        if (magic.GetActiveRitual(id) != null) magic.StopRitual(id);
        else magic.LaunchRitual(id);
    }

    /// <summary>Ajuste la puissance d'un rituel depuis une vue portée par l'hôte.</summary>
    public void ChangeRitualPowerFromHost(string key, bool increase)
    {
        if (!Enum.TryParse<RitualId>(key, out var id)) return;
        var magic = _gameControllerService.MainGameController.MagicController;
        if (increase) magic.IncreaseRitualPower(id);
        else magic.DecreaseRitualPower(id);
    }

    /// <summary>Active ou désactive l'ajustement automatique de puissance d'un rituel depuis une vue portée par l'hôte.</summary>
    public void SetRitualAutomatedFromHost(string key, bool automated)
    {
        if (!Enum.TryParse<RitualId>(key, out var id)) return;
        _gameControllerService.MainGameController.MagicController.SetRitualAutomated(id, automated);
    }

    /// <summary>Lance un sort, ou entre en sélection de cible, depuis une vue portée par l'hôte.</summary>
    public void CastSpellFromHost(string key)
    {
        if (!Enum.TryParse<SpellId>(key, out var id)) return;
        CastOrTargetSpell(id, _gameControllerService.MainGameController.MagicController);
    }

    private void CastOrTargetSpell(SpellId id, SettlersOfIdlestan.Controller.Magic.MagicController magic)
    {
        var def = SpellDefinitions.Get(id);
        if (def == null) return;

        if (def.TargetKind == SpellTargetKind.AllyCity)
        {
            if (_targetSelectionService == null) return;
            var targets = magic.GetAllyCityTargets();
            _targetSelectionService.EnterVertexSelection("spell_select_ally_city", targets,
                target => magic.CastSpellOnCity(id, target), TargetSelectionTheme.Friendly);
        }
        else if (def.TargetKind == SpellTargetKind.BuildableVertex)
        {
            if (_targetSelectionService == null) return;
            var targets = magic.GetBuildableCityTargets();
            _targetSelectionService.EnterVertexSelection("spell_select_buildable_vertex", targets,
                target => magic.CastSpellOnVertex(id, target), TargetSelectionTheme.Friendly);
        }
        else if (def.TargetKind == SpellTargetKind.VoidRoad)
        {
            if (_targetSelectionService == null) return;
            var targets = magic.GetVoidBridgeTargets();
            _targetSelectionService.EnterEdgeSelection("spell_select_void_road", targets,
                target => magic.CastSpellOnVoidRoad(id, target), TargetSelectionTheme.Friendly);
        }
        else
        {
            magic.CastSpell(id);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
