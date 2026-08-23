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

    private bool ShowTierPicker
        => _gameControllerService.MainGameController.PrestigeController.CanChooseNextIslandTier();

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
            if (level >= 3) tooltip.Add(_localization.Get("prestige_tooltip_great_lighthouse_secondary_tier_picker"));
            rows.Add(new PrestigeRowSnapshot(
                _localization.GetFormated("prestige_great_lighthouse_bonus", level),
                $"+{greatLighthouseBonus * 100:0}%", false, tooltip));
        }

        int maxCorruptionCleared = controller.GetMaxCorruptionLevelCleared();
        if (maxCorruptionCleared > 0)
            rows.Add(new PrestigeRowSnapshot(
                _localization.GetFormated("prestige_corruption_spire_bonus", maxCorruptionCleared),
                $"×{controller.GetCorruptionClearBonusMultiplier()}", false,
                [_localization.Get("prestige_tooltip_corruption_spire_bonus")]));

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
                _localization.GetFormated("prestige_seaport_bonus", controller.GetSeaportLevel4Count()),
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

        // Choix du palier de la prochaine ile : debloque par le Grand Phare niveau 3.
        string? tierPickerLabel = null;
        bool canDecreaseTier = false, canIncreaseTier = false;
        var tierPickerTooltip = new List<string>();
        if (ShowTierPicker)
        {
            int chosenTier = controller.GetNextIslandTierChoice();
            int maxTier = tier + PrestigeController.MaxNextIslandTierChoiceBonus;
            tierPickerLabel = _localization.GetFormated("prestige_next_island_tier_picker", chosenTier);
            canDecreaseTier = chosenTier > tier;
            canIncreaseTier = chosenTier < maxTier;
            tierPickerTooltip.Add(_localization.Get("tooltip_prestige_next_island_tier_picker"));
        }

        bool canPrestige = controller.PrestigeIsAvailable();
        var actions = new List<PrestigeActionSnapshot>
        {
            new(PrestigePopupSnapshot.ActionNormal, _localization.Get("prestige_action"), null,
                canPrestige, false, [_localization.Get("prestige_tooltip_action")]),
        };

        // Le prestige corrompu n'existe qu'une fois une Spire de Corruption batie.
        if (controller.HasCorruptionSpireBuilt())
        {
            int corruptionLevel = controller.GetCorruptionLevel();
            actions.Add(new PrestigeActionSnapshot(
                PrestigePopupSnapshot.ActionCorrupted,
                _localization.Get("prestige_corrupted_action"),
                $"{corruptionLevel} -> {corruptionLevel + 1}",
                canPrestige, true,
                [
                    _localization.Get("prestige_tooltip_corrupted_action"),
                    _localization.Get("prestige_tooltip_corrupted_action_risk"),
                    _localization.Get("prestige_tooltip_corrupted_action_reward"),
                ]));
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
            TierPickerLabel: tierPickerLabel,
            CanDecreaseTier: canDecreaseTier,
            CanIncreaseTier: canIncreaseTier,
            TierPickerTooltip: tierPickerTooltip,
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

    /// <summary>Ajuste le palier choisi pour la prochaine ile, depuis la vue de l'hote.</summary>
    public void ChangeTierChoiceFromHost(bool increase)
    {
        if (!IsOpen) return;
        var controller = _gameControllerService.MainGameController.PrestigeController;
        controller.SetNextIslandTierChoice(controller.GetNextIslandTierChoice() + (increase ? 1 : -1));
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
