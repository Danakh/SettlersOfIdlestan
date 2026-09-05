using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class PrestigeRenderer : PopupRendererBase
{
    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService   _localization;
    private readonly Action<bool>          _prestigeRequested;
    private readonly PrestigeEssenceLossPopupRenderer _essenceLossPopup;
    private readonly PrestigeCorruptionWarningPopupRenderer _corruptionWarningPopup;

    public PrestigeRenderer(
        GameControllerService gameControllerService,
        LocalizationService   localization,
        Action<bool>          prestigeRequested,
        TooltipRenderer       tooltipRenderer)
    {
        _gameControllerService = gameControllerService;
        _localization          = localization;
        _prestigeRequested     = prestigeRequested;
        _essenceLossPopup      = new PrestigeEssenceLossPopupRenderer(localization,
            onConfirm: corrupted => _prestigeRequested(corrupted));
        // Le garde-fou de corruption passe avant la perte d'essences : une fois confirme, le
        // prestige corrompu reprend le chemin normal, confirmation d'essences comprise.
        _corruptionWarningPopup = new PrestigeCorruptionWarningPopupRenderer(localization,
            onConfirm: () => ConfirmEssenceLossOrPrestige(corrupted: true));
    }

    public override void Initialize(SKSize canvasSize)
    {
        base.Initialize(canvasSize);
        _essenceLossPopup.Initialize(canvasSize);
        _corruptionWarningPopup.Initialize(canvasSize);
    }

    public override void Close()
    {
        base.Close();
        _essenceLossPopup.Close();
        _corruptionWarningPopup.Close();
    }

    /// <summary>
    /// Instantane de la confirmation portee par ce popup qui est ouverte, s'il y en a une. Les deux
    /// s'enchainent (corruption puis essences) et ne sont donc jamais ouvertes en meme temps, mais
    /// l'ordre de priorite reste celui de l'enchainement.
    /// </summary>
    public ModalPopupSnapshot GetOverlayModalSnapshot()
        => _corruptionWarningPopup.IsOpen ? _corruptionWarningPopup.GetSnapshot() : _essenceLossPopup.GetSnapshot();

    /// <summary>Declenche un bouton de l'une de ces confirmations, depuis la vue de l'hote.</summary>
    public void InvokeOverlayModalButtonFromHost(string popupId, string key)
    {
        switch (popupId)
        {
            case ModalPopupSnapshot.IdPrestigeEssenceLoss:       _essenceLossPopup.InvokeButton(key);       break;
            case ModalPopupSnapshot.IdPrestigeCorruptionWarning: _corruptionWarningPopup.InvokeButton(key); break;
        }
    }

    /// <summary>
    /// Instantane du popup pour une vue portee par l'hote. Reprend les memes appels au
    /// PrestigeController que Render, dans le meme ordre, et les memes conditions d'affichage :
    /// un bonus nul reste masque, il n'est pas affiche a zero.
    /// </summary>
    public PrestigePopupSnapshot GetSnapshot()
    {
        if (!IsOpen || Disposed) return PrestigePopupSnapshot.Closed;

        var controller = _gameControllerService.MainGameController.PrestigeController;
        var rows = new List<PrestigeRowSnapshot>();

        foreach (var source in controller.GetPrestigePointSources())
            rows.Add(new PrestigeRowSnapshot(
                Label: _localization.Get(source.LabelKey),
                Value: SkiaTextUtils.FormatNumber(source.Points),
                IsWarning: false,
                Tooltip: source.TooltipKey != null ? [_localization.Get(source.TooltipKey)] : []));

        // Monstres : le bonus n'est acquis qu'une fois la surface nettoyee.
        bool hasMonstersLeft = controller.HasSurfaceMonsters();
        rows.Add(new PrestigeRowSnapshot(
            Label: _localization.Get("prestige_monster_bonus"),
            Value: hasMonstersLeft ? "+0%" : "+20%",
            IsWarning: hasMonstersLeft,
            Tooltip: [_localization.Get("prestige_tooltip_monster_bonus")]));

        double greatLighthouseBonus = controller.GetGreatLighthousePrestigeBonus();
        if (greatLighthouseBonus > 0)
        {
            int level = controller.GetGreatLighthouseLevel();
            var tooltip = new List<string> { _localization.Get("prestige_tooltip_great_lighthouse_bonus") };
            if (level >= 2) tooltip.Add(_localization.Get("prestige_tooltip_great_lighthouse_secondary_maritime"));
            if (level >= 3) tooltip.Add(_localization.Get("prestige_tooltip_great_lighthouse_secondary_war_fleet"));
            rows.Add(new PrestigeRowSnapshot(
                _localization.GetFormated("prestige_great_lighthouse_bonus", level),
                $"+{greatLighthouseBonus * 100:0}%", false, tooltip));
        }

        // Spire de Corruption : le bonus n'existe que tant qu'une Spire (ou la Faille qui lui succede)
        // est batie sur l'ile courante, et vaut alors 2 x le niveau de corruption du monde — soit le
        // niveau de la Source qu'elle a detruite.
        int corruptionClearMultiplier = controller.GetCorruptionClearBonusMultiplier();
        if (corruptionClearMultiplier > 1)
            rows.Add(new PrestigeRowSnapshot(
                _localization.GetFormated("prestige_corruption_spire_bonus", controller.GetCorruptionLevel()),
                $"×{corruptionClearMultiplier}", false,
                [_localization.Get("prestige_tooltip_corruption_spire_bonus")]));

        // Theologie de l'Ascension : multiplicateur compose, affiche comme la Spire et la Merveille.
        double divineBonesMultiplier = controller.GetDivineBonesPrestigeMultiplier();
        if (divineBonesMultiplier > 1.0)
            rows.Add(new PrestigeRowSnapshot(
                _localization.GetFormated("prestige_divine_bones_bonus", controller.GetPurifiedDivineBonesCount()),
                $"×{divineBonesMultiplier:0.##}", false,
                [_localization.Get("prestige_tooltip_divine_bones_bonus")]));

        double gainBonus = controller.GetPrestigeGainBonus();
        if (gainBonus > 0)
            rows.Add(new PrestigeRowSnapshot(
                _localization.Get("prestige_gain_bonus"), $"+{gainBonus * 100:0.#}%", false,
                [_localization.Get("prestige_tooltip_prestige_gain_bonus")]));

        // Bonus ou malus propre a la race (les Gobelins ont -25%) : seul de ces bonus a pouvoir
        // etre negatif, d'ou la mise en garde.
        double raceBonus = controller.GetRaceGainBonus();
        if (raceBonus != 0)
            rows.Add(new PrestigeRowSnapshot(
                _localization.Get("prestige_race_bonus"),
                $"{(raceBonus >= 0 ? "+" : "")}{raceBonus * 100:0.#}%", raceBonus < 0,
                [_localization.Get("prestige_tooltip_race_bonus")]));

        double templeBonus = controller.GetTemplePrestigeBonus();
        if (templeBonus > 0)
            rows.Add(new PrestigeRowSnapshot(
                _localization.GetFormated("prestige_temple_bonus", controller.GetTempleCount()),
                $"+{templeBonus * 100:0.#}%", false,
                [_localization.Get("prestige_tooltip_temple_bonus")]));

        double seaportBonus = controller.GetSeaportPrestigeBonus();
        if (seaportBonus > 0)
            rows.Add(new PrestigeRowSnapshot(
                _localization.GetFormated("prestige_seaport_bonus", controller.GetSeaportMaxLevelCount()),
                $"+{seaportBonus * 100:0}%", false,
                [_localization.Get("prestige_tooltip_seaport_bonus")]));

        double civDestroyedBonus = controller.GetCivilizationsDestroyedBonus();
        if (civDestroyedBonus > 0)
            rows.Add(new PrestigeRowSnapshot(
                _localization.GetFormated("prestige_civilizations_destroyed_bonus", controller.GetCivilizationsDestroyedCount()),
                $"+{civDestroyedBonus * 100:0}%", false,
                [_localization.Get("prestige_tooltip_civilizations_destroyed_bonus")]));

        int tier = controller.GetTier();
        double tierBonus = controller.GetTierBonus();
        if (tierBonus > 0)
            rows.Add(new PrestigeRowSnapshot(
                _localization.GetFormated("prestige_tier_bonus", tier),
                $"+{tierBonus * 100:0}%", false,
                [_localization.Get("prestige_tooltip_tier_bonus")]));

        // Merveille : seul bonus exprime en multiplicateur, et affiche hors du defilement.
        PrestigeRowSnapshot? wonderRow = null;
        bool canSkipWonderTime = false;
        var wonderSkipTooltip = new List<string>();
        if (controller.WondersUnlocked())
        {
            var (wonderLevel, timeFactor, runTicks) = controller.GetWonderBonusDetails();
            if (wonderLevel >= 1)
            {
                wonderRow = new PrestigeRowSnapshot(
                    _localization.GetFormated("prestige_wonder_bonus", wonderLevel, timeFactor, FormatRunDuration(runTicks)),
                    $"×{Math.Max(1, wonderLevel * timeFactor)}", false,
                    [_localization.Get("prestige_tooltip_wonder_bonus")]);
                canSkipWonderTime = controller.CanSkipToNextWonderMultiplier();
                wonderSkipTooltip.Add(_localization.Get(
                    canSkipWonderTime ? "tooltip_wonder_skip_time" : "tooltip_wonder_skip_time_disabled"));
            }
        }

        bool canPrestige = controller.PrestigeIsAvailable();
        var actions = new List<PrestigeActionSnapshot>
        {
            new(PrestigePopupSnapshot.ActionNormal, _localization.Get("prestige_action"), null,
                canPrestige, false, [_localization.Get("prestige_tooltip_action")]),
        };

        // Le bouton de prestige corrompu reste visible des que l'Abysse est debloque (3 vertex de
        // prestige), meme avant que la Spire de Corruption ne soit batie : le message explique alors
        // comment y arriver plutot que de faire disparaitre le bouton.
        if (controller.IsCorruptedPrestigeUnlocked())
        {
            var step = controller.GetCorruptedPrestigeStep();
            bool spireBuilt = step == PrestigeController.CorruptedPrestigeStep.Available;
            int corruptionLevel = controller.GetCorruptionLevel();
            var tooltip = new List<string> { _localization.Get("prestige_tooltip_corrupted_action") };
            if (spireBuilt)
            {
                tooltip.Add(_localization.Get("prestige_tooltip_corrupted_action_risk"));
                tooltip.Add(_localization.Get("prestige_tooltip_corrupted_action_reward"));
            }
            else
            {
                // Chaque étape restante (voir PrestigeController.CorruptedPrestigeStep) pointe vers
                // l'action concrète à accomplir ensuite : explorer, placer la Spire, ou l'achever.
                string lockedKey = step switch
                {
                    PrestigeController.CorruptedPrestigeStep.SpireUnderConstruction => "prestige_tooltip_corrupted_action_locked_building",
                    PrestigeController.CorruptedPrestigeStep.SourceAwaitingSpire    => "prestige_tooltip_corrupted_action_locked_awaiting_spire",
                    _                                                              => "prestige_tooltip_corrupted_action_locked_no_source",
                };
                tooltip.Add(_localization.Get(lockedKey));
            }

            actions.Add(new PrestigeActionSnapshot(
                PrestigePopupSnapshot.ActionCorrupted,
                _localization.Get("prestige_corrupted_action"),
                spireBuilt ? $"{corruptionLevel} -> {corruptionLevel + 1}" : null,
                canPrestige && spireBuilt, true,
                tooltip));
        }

        bool hasEnoughPoints = controller.CalculatePrestigePoints() >= PrestigeController.PrestigeRequiredPoints;

        // Le plafond de prestige de la demo passe devant : une fois atteint, le prestige reste
        // possible mais ne rapporte plus rien, ce qui ne se devine pas depuis le total affiche.
        var mainState = _gameControllerService.MainGameController.CurrentMainState;
        bool demoCapReached = mainState?.Settings.DemoMode == true
            && (mainState.PrestigeState?.TotalPrestigePointsEarned ?? 0) >= PrestigeState.DemoMaxTotalPrestigePointsEarned;

        string? warning =
            demoCapReached ? _localization.Get("prestige_demo_cap_reached")
            : hasEnoughPoints && !controller.HasImperialPort() ? _localization.Get("prestige_requires_imperial_port")
            : null;

        return new PrestigePopupSnapshot(
            IsOpen: true,
            Title: _localization.Get("prestige_title"),
            Rows: rows,
            WonderRow: wonderRow,
            CanSkipWonderTime: canSkipWonderTime,
            WonderSkipTooltip: wonderSkipTooltip,
            TotalLabel: _localization.Get("prestige_total"),
            TotalValue: SkiaTextUtils.FormatNumber(controller.CalculatePrestigePoints()),
            Actions: actions,
            Warning: warning);
    }

    /// <summary>
    /// Declenche un prestige depuis une vue portee par l'hote. La garde vit ici : l'action est
    /// declenchee par deux chemins et une garde dupliquee finirait par diverger.
    /// </summary>
    public void InvokeActionFromHost(string key)
    {
        if (!IsOpen) return;
        if (!_gameControllerService.MainGameController.PrestigeController.PrestigeIsAvailable()) return;

        switch (key)
        {
            case PrestigePopupSnapshot.ActionNormal:    TryPrestige(corrupted: false); break;
            case PrestigePopupSnapshot.ActionCorrupted: TryPrestige(corrupted: true);  break;
        }
    }

    /// <summary>Avance au prochain palier de multiplicateur de merveille, depuis la vue de l'hote.</summary>
    public void SkipWonderTimeFromHost()
    {
        if (!IsOpen) return;
        RequestWonderTimeJump();
    }

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

    // Deux confirmations possibles, dans cet ordre : d'abord la montee de corruption avant la
    // premiere Ascension (choix irreversible pour tout le cycle), puis la perte d'essences divines.
    private void TryPrestige(bool corrupted)
    {
        var controller = _gameControllerService.MainGameController.PrestigeController;
        var godState = _gameControllerService.MainGameController.CurrentMainState?.GodState;

        if (corrupted && godState != null && controller.CorruptedPrestigeNeedsAscensionWarning(godState))
        {
            _corruptionWarningPopup.Open(controller.GetCorruptionLevel() + 1);
            return;
        }

        ConfirmEssenceLossOrPrestige(corrupted);
    }

    // Ouvre une confirmation si le prestige entraînerait la perte d'essences divines
    // (au-delà de ce que le Reliquaire Sacré/Renforcé permet de conserver), sinon prestige immédiat.
    private void ConfirmEssenceLossOrPrestige(bool corrupted)
    {
        var godState = _gameControllerService.MainGameController.CurrentMainState?.GodState;
        int essenceLoss = godState != null
            ? _gameControllerService.MainGameController.PrestigeController.GetDivineEssenceLoss(godState)
            : 0;

        if (essenceLoss > 0)
            _essenceLossPopup.Open(essenceLoss, corrupted);
        else
            _prestigeRequested(corrupted);
    }

    private static string FormatRunDuration(long ticks)
    {
        int totalMinutes = (int)(ticks / 6000);
        int hours   = totalMinutes / 60;
        int minutes = totalMinutes % 60;
        if (hours > 0 && minutes > 0) return $"{hours}h{minutes:D2}m";
        if (hours > 0) return $"{hours}h";
        return $"{Math.Max(1, minutes)}m";
    }

    public override void Dispose()
    {
        if (Disposed) return;
        _essenceLossPopup.Dispose();
        _corruptionWarningPopup.Dispose();
        base.Dispose();
    }
}
