using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Panels;

public class SelectedMonumentPanelRenderer : PanelRendererBase
{
    private readonly MonumentService _monumentService;
    private readonly LocalizationService _localization;
    private readonly GameControllerService _gameControllerService;

    public bool HasSelection => _monumentService.SelectedInvestable != null;

    /// <summary>
    /// Destruction de la Spire en deux temps : le premier clic arme le bouton, le second confirme.
    /// L'action est irréversible et coûteuse (ressources investies perdues, rayon remis à 1), et le
    /// bouton siège juste sous ceux d'investissement — un clic isolé ne doit pas suffire. Désarmé
    /// par tout autre clic, par la fermeture du panneau et par un changement de sélection.
    /// </summary>
    private bool _destroyConfirmPending;

    public SelectedMonumentPanelRenderer(
        MonumentService monumentService,
        LocalizationService localization,
        GameControllerService gameControllerService)
    {
        _monumentService = monumentService;
        _localization = localization;
        _gameControllerService = gameControllerService;
        _monumentService.SelectionChanged += (_, _) => { Collapsed = false; _destroyConfirmPending = false; };
    }

    /// <summary>
    /// Lignes de bonus affichées sous la liste de ressources : bonus actuel (doré), prochain bonus
    /// et bonus secondaires déjà actifs (bronze/prestige combinés à d'autres branches) sont tous
    /// rendus en doré ; seul le "prochain bonus" à venir est atténué.
    /// </summary>
    private List<(string Text, bool Active)> GetBonusLines(Monument monument, SettlersOfIdlestan.Model.Civilization.Civilization playerCiv)
    {
        var prestige = _gameControllerService.MainGameController.PrestigeController;
        var lines = new List<(string Text, bool Active)>();

        switch (monument)
        {
            case Wonder wonder:
            {
                int timeFactor = prestige.GetWonderBonusDetails().TimeFactor;
                if (wonder.Level > 0)
                    lines.Add((_localization.GetFormated("monument_bonus_wonder_current", wonder.Level, wonder.Level * timeFactor), true));
                if (!wonder.IsMaxLevel)
                    lines.Add((_localization.GetFormated("monument_bonus_wonder_next", wonder.Level + 1, (wonder.Level + 1) * timeFactor), false));
                break;
            }
            case GreatLighthouse greatLighthouse:
            {
                if (greatLighthouse.Level > 0)
                    lines.Add((_localization.GetFormated("monument_bonus_great_lighthouse_current", greatLighthouse.Level, greatLighthouse.Level * 10), true));
                if (!greatLighthouse.IsMaxLevel)
                    lines.Add((_localization.GetFormated("monument_bonus_great_lighthouse_next", greatLighthouse.Level + 1, (greatLighthouse.Level + 1) * 10), false));

                bool watchtowerUnlocked = playerCiv.ModifierAggregator.ApplyModifiers(SettlersOfIdlestan.Model.GameplayModifier.Modifier.ECategory.BUILDING_MAX_LEVEL, "Watchtower", 0) > 0;
                if (greatLighthouse.Level >= 1 && watchtowerUnlocked)
                    lines.Add((_localization.Get("monument_bonus_great_lighthouse_watchtower_active"), true));

                if (greatLighthouse.Level >= 2)
                {
                    bool maritimeRoutesUnlocked = playerCiv.ModifierAggregator.HasModifier(SettlersOfIdlestan.Model.GameplayModifier.Modifier.ECategory.UNLOCK_MARITIME_ROUTES);
                    lines.Add((_localization.Get(maritimeRoutesUnlocked
                        ? "monument_bonus_great_lighthouse_maritime_active"
                        : "monument_bonus_great_lighthouse_maritime_beacon_active"), true));
                }

                if (greatLighthouse.Level >= 3)
                    lines.Add((_localization.Get("monument_bonus_great_lighthouse_tier_picker_active"), true));
                break;
            }
            case Observatory observatory:
            {
                // Le multiplicateur est la grandeur qui compte pour le joueur : il pilote la marche
                // exponentielle du coût en points de recherche de chaque nouvelle route du Vide.
                lines.Add((_localization.GetFormated("monument_bonus_observatory_current",
                    observatory.Level, FormatMultiplier(observatory.VoidRouteCostMultiplier)), observatory.Level > 0));
                if (!observatory.IsMaxLevel)
                    lines.Add((_localization.GetFormated("monument_bonus_observatory_next",
                        observatory.Level + 1,
                        FormatMultiplier(Observatory.GetVoidRouteCostMultiplierForLevel(observatory.Level + 1))), false));
                break;
            }
            case Necropolis necropolis:
            {
                // Le bonus porte sur la conversion essence -> points divins de la prochaine Ascension
                // (voir AscensionController.GetGodPointsGain) : on l'exprime en pourcentage cumulé.
                lines.Add((_localization.GetFormated("monument_bonus_necropolis_current",
                    necropolis.Level, FormatPercent(Necropolis.GetAscensionGainBonusForLevel(necropolis.Level))), necropolis.Level > 0));
                if (!necropolis.IsMaxLevel)
                    lines.Add((_localization.GetFormated("monument_bonus_necropolis_next",
                        necropolis.Level + 1,
                        FormatPercent(Necropolis.GetAscensionGainBonusForLevel(necropolis.Level + 1))), false));
                break;
            }
            case DeepestMine mine:
                lines.Add(mine.Dug
                    ? (_localization.Get("monument_bonus_deepest_mine_current"), true)
                    : mine.WasEverDug
                        ? (_localization.Get("monument_bonus_deepest_mine_lost"), false)
                        : (_localization.Get("monument_bonus_deepest_mine_next"), false));
                break;
            case CorruptionSpire spire:
                lines.Add((_localization.GetFormated("monument_bonus_corruption_spire_decay_radius", spire.Radius), true));
                AddCorruptionClearPotentialLine(lines, spire.Position);
                break;
            case AbyssGate gate:
                lines.Add(gate.Built
                    ? (_localization.Get("monument_bonus_abyss_gate_current"), true)
                    : gate.WasEverBuilt
                        ? (_localization.Get("monument_bonus_abyss_gate_lost"), false)
                        : (_localization.Get("monument_bonus_abyss_gate_next"), false));
                lines.Add((_localization.Get("monument_bonus_corruption_spire_decay"), true));
                AddCorruptionClearPotentialLine(lines, gate.Position);
                break;
            case DivineBones bones:
            {
                var godState = _gameControllerService.MainGameController.CurrentMainState?.GodState;
                int essence = godState?.DivineEssence ?? 0;
                int cap = bones.GetEssenceCap();
                lines.Add((_localization.GetFormated("divine_bones_essence_status", essence, cap), essence < cap));
                // Les Os non purifiés corrompent leur propre hex (voir CorruptionController.ProcessDivineBonesCorruptionGrowth).
                lines.Add((_localization.GetFormated("monument_bonus_divine_bones_corruption", bones.GetCorruptionCap()), true));
                break;
            }
        }

        return lines;
    }

    /// <summary>
    /// Multiplicateur du coût des routes du Vide, sans décimale inutile : "4" plutôt que "4,00",
    /// mais "3,67" quand l'Observatoire est à mi-chemin.
    /// </summary>
    private static string FormatMultiplier(double multiplier)
        => multiplier == Math.Floor(multiplier) ? multiplier.ToString("0") : multiplier.ToString("0.00");

    /// <summary>Fraction affichée en pourcentage entier : 0.15 → "15".</summary>
    private static string FormatPercent(double fraction)
        => Math.Round(fraction * 100).ToString("0");

    /// <summary>
    /// Ligne de bonus commune à la Spire de Corruption et à la Faille des Abysses : le bonus de
    /// prestige n'est pas lié à la construction elle-même, il dépend du pic de corruption que le
    /// nettoyage garanti (ProcessMonumentCorruptionDecay) finira par atteindre sur cet hex (voir
    /// PrestigeController.GetCorruptionClearBonusMultiplier).
    /// </summary>
    private void AddCorruptionClearPotentialLine(List<(string Text, bool Active)> lines, HexCoord position)
    {
        var corruption = _gameControllerService.MainGameController.CurrentMainState?.CurrentWorldState?.Features
            .OfType<Corruption>()
            .FirstOrDefault(c => c.Position.Equals(position));
        if (corruption != null)
            lines.Add((_localization.GetFormated("monument_bonus_corruption_spire_potential", 2 * corruption.PeakLevel), true));
    }

    /// <summary>
    /// Instantané du panneau pour une vue portée par l'hôte. Toute la logique polymorphe
    /// (Merveille, Grand Phare, Os Divins, Faille des Abysses) reste ici : la vue n'a aucune
    /// connaissance du modèle et ne peut donc pas diverger de l'affichage Skia.
    /// </summary>
    public MonumentPanelSnapshot GetSnapshot()
    {
        var monument = _monumentService.SelectedInvestable;
        var playerCiv = _gameControllerService.PlayerCivilization;
        if (monument == null || playerCiv == null) return MonumentPanelSnapshot.Hidden;

        bool wonderMaxed = (monument is Wonder { IsMaxLevel: true })
                        || (monument is GreatLighthouse { IsMaxLevel: true })
                        || (monument is Observatory { IsMaxLevel: true })
                        || (monument is Necropolis { IsMaxLevel: true });
        bool bonesPurified = monument is DivineBones { Purified: true };
        bool showResearchRow = monument.UsesResearchInvestment;
        bool showCorruptedPrestige = monument is CorruptionSpire { Built: true };
        bool showEvolve = showCorruptedPrestige
                       && _gameControllerService.MainGameController.AbyssGateController.IsAbyssGateEligible();
        bool showWonderSkip = monument is Wonder { Level: >= 1 };
        bool showNoCityWarning = !wonderMaxed && !MonumentInvestment.HasAdjacentCity(monument.Position, playerCiv);

        var rows = new List<InvestmentRowSnapshot>();
        if (!wonderMaxed)
        {
            foreach (var kvp in monument.GetInvestmentCost(playerCiv))
            {
                long invested = monument.InvestedResources.TryGetValue(kvp.Key, out var inv) ? inv : 0;
                rows.Add(new InvestmentRowSnapshot(
                    Key: kvp.Key.ToString(),
                    IconName: kvp.Key.ToString(),
                    Label: _localization.Get("resource_" + kvp.Key.ToString().ToLower()),
                    Invested: invested,
                    Required: kvp.Value,
                    IsEnabled: monument.InvestmentEnabled.Contains(kvp.Key),
                    IsDone: invested >= kvp.Value,
                    InvestedLabel: SkiaTextUtils.FormatNumber(invested),
                    RequiredLabel: SkiaTextUtils.FormatNumber(kvp.Value)));
            }
        }

        if (showResearchRow)
        {
            long required = monument.GetRequiredResearch(playerCiv);
            rows.Add(new InvestmentRowSnapshot(
                Key: InvestmentRowSnapshot.ResearchKey,
                IconName: null,
                Label: _localization.Get("research_points_label"),
                Invested: monument.InvestedResearch,
                Required: required,
                IsEnabled: monument.ResearchInvestmentEnabled,
                IsDone: monument.InvestedResearch >= required,
                InvestedLabel: SkiaTextUtils.FormatNumber(monument.InvestedResearch),
                RequiredLabel: SkiaTextUtils.FormatNumber(required)));
        }

        string title = _localization.Get(monument.PanelTitleKey)
                     + (monument.PanelTitleSuffix != null ? " " + monument.PanelTitleSuffix : "");

        return new MonumentPanelSnapshot(
            IsVisible: true,
            Title: title,
            Rows: rows,
            BonusLines: GetBonusLines(monument, playerCiv)
                .Select(b => new BonusLineSnapshot(b.Text, b.Active)).ToList(),
            WonderMaxedMessage: wonderMaxed ? _localization.Get("wonder_max_level_reached") : null,
            PurifiedMessage: bonesPurified
                ? _localization.Get(monument is DivineBones { EssenceGranted: true }
                    ? "divine_bones_purified_message"
                    : "divine_bones_purified_no_essence_message")
                : null,
            PurifiedGrantedEssence: monument is DivineBones { EssenceGranted: true },
            NoCityWarning: showNoCityWarning ? _localization.Get("tooltip_requires_adjacent_city") : null,
            CorruptedPrestigeMessage: showCorruptedPrestige
                ? _localization.Get("corruption_spire_panel_corrupted_prestige_available")
                : null,
            EvolveButtonLabel: showEvolve ? _localization.Get("abyss_gate_evolve_button") : null,
            WonderSkipButtonLabel: showWonderSkip ? _localization.Get("wonder_skip_time_button") : null,
            CanSkipWonder: showWonderSkip
                && _gameControllerService.MainGameController.PrestigeController.CanSkipToNextWonderMultiplier(),
            // Le libellé porte l'état d'armement : la confirmation en deux temps reste ici, la vue
            // se contente d'afficher le texte courant et de renvoyer les clics.
            DestroyButtonLabel: monument is CorruptionSpire
                ? _localization.Get(_destroyConfirmPending
                    ? "corruption_spire_destroy_confirm_button"
                    : "corruption_spire_destroy_button")
                : null);
    }

    /// <summary>Ferme le panneau depuis une vue portée par l'hôte.</summary>
    public void CloseFromHost()
    {
        _destroyConfirmPending = false;
        _monumentService.ClearSelectedInvestable();
    }

    /// <summary>
    /// Détruit la Spire depuis une vue portée par l'hôte. Comme le bouton Skia, le premier appel
    /// arme la confirmation et le second seulement détruit (voir <see cref="_destroyConfirmPending"/>).
    /// </summary>
    public void DestroyFromHost() => TryDestroySpire();

    /// <summary>
    /// Confirmation en deux temps de la destruction de la Spire, partagée par le bouton Skia et la
    /// vue hôte. Retourne true une fois la Spire réellement détruite.
    /// </summary>
    private bool TryDestroySpire()
    {
        if (!_destroyConfirmPending)
        {
            _destroyConfirmPending = true;
            return false;
        }

        _destroyConfirmPending = false;
        if (!_gameControllerService.MainGameController.CorruptionSpireController.DestroyCorruptionSpire())
            return false;

        _monumentService.ClearSelectedInvestable();
        return true;
    }

    /// <summary>Bascule l'investissement d'une ligne depuis une vue portée par l'hôte.</summary>
    public void ToggleInvestmentFromHost(string rowKey)
    {
        _destroyConfirmPending = false;
        if (rowKey == InvestmentRowSnapshot.ResearchKey)
        {
            _monumentService.ToggleResearchInvestment();
            return;
        }

        if (Enum.TryParse<Resource>(rowKey, out var resource))
            _monumentService.ToggleInvestment(resource);
    }

    /// <summary>Fait évoluer la Spire en Faille des Abysses, depuis une vue portée par l'hôte.</summary>
    public void EvolveFromHost()
    {
        _destroyConfirmPending = false;
        var gate = _gameControllerService.MainGameController.AbyssGateController.PlaceAbyssGate();
        if (gate != null) _monumentService.SetSelectedInvestable(gate);
    }

    /// <summary>Saute au prochain multiplicateur de merveille, depuis une vue portée par l'hôte.</summary>
    public void SkipWonderFromHost() => RequestWonderTimeJump();

    /// <summary>
    /// Programme le saut jusqu'au prochain palier de la Merveille. La simulation elle-même est
    /// étalée par <see cref="TimeJumpService"/> sur les ticks suivants : la déclencher ici
    /// figerait la fenêtre le temps de simuler jusqu'à une heure de jeu.
    /// </summary>
    private void RequestWonderTimeJump()
    {
        var prestige = _gameControllerService.MainGameController.PrestigeController;
        if (!prestige.CanSkipToNextWonderMultiplier()) return;

        _gameControllerService.RequestTimeJump(
            prestige.GetTicksUntilNextWonderMultiplier(), "time_jump_reason_wonder");
    }

    public void Close()
    {
        _monumentService.ClearSelectedInvestable();
        Collapsed = false;
        _destroyConfirmPending = false;
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}
