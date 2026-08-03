using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Expand;
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
    protected override float PopupWidth    => 460;
    protected override float PopupHeight   => 448 + (ShowTierPicker ? 44 : 0);
    protected override float TitleFontSize => 20f;
    protected override float BodyFontSize  => 14f;
    protected override float BtnFontSize   => 14f;

    private const float Padding         = 18;
    private const float ButtonHeight    = 36;
    private const float SourceRowHeight = 24;
    private const float RowHeight       = 28;

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService   _localization;
    private readonly TooltipRenderer       _tooltipRenderer;
    private readonly Action<bool>          _prestigeRequested;
    private readonly PrestigeEssenceLossPopupRenderer _essenceLossPopup;

    private SKRect  _prestigeButtonRect          = SKRect.Empty;
    private SKRect  _corruptedPrestigeButtonRect = SKRect.Empty;
    private SKRect  _wonderSkipButtonRect = SKRect.Empty;
    private SKRect  _closeButtonRect     = SKRect.Empty;
    private SKRect  _tierMinusButtonRect = SKRect.Empty;
    private SKRect  _tierPlusButtonRect  = SKRect.Empty;
    private SKPoint _lastPointerPosition;

    private bool ShowTierPicker
        => _gameControllerService.MainGameController.PrestigeController.CanChooseNextIslandTier();

    // Rects testés en espace écran (chrome fixe : boutons, total, merveille, etc.)
    private readonly List<(SKRect Rect, string[] Keys)> _hoverRects = new();
    // Rects testés en espace "contenu" (zone défilante) : ajuster le pointeur de _scrollOffsetPx avant comparaison
    private readonly List<(SKRect Rect, string[] Keys)> _scrollHoverRects = new();
    private SKFont? _smallFont;

    // État de défilement de la zone de contenu (sources + monstres + bonus), hors Merveille/Total/footer
    private SKRect _viewportRect        = SKRect.Empty;
    private SKRect _scrollTrackRect     = SKRect.Empty;
    private SKRect _scrollThumbRect     = SKRect.Empty;
    private float  _scrollOffsetPx      = 0f;
    private float  _contentH            = 0f;
    private float  _viewportH           = 0f;
    private bool   _isDraggingScrollbar = false;
    private float  _scrollDragStartY      = 0f;
    private float  _scrollDragStartOffset = 0f;

    private readonly SKPaint _buttonPaint         = new() { Color = new SKColor(46, 125, 50),      Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _buttonDisabledPaint = new() { Color = new SKColor(70, 70, 70, 220),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _corruptedButtonPaint = new() { Color = new SKColor(120, 40, 150),     Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _warningTextPaint    = new() { Color = new SKColor(220, 70, 70),       IsAntialias = true };
    private readonly SKPaint _separatorPaint      = new() { Color = new SKColor(100, 100, 110, 180), StrokeWidth = 1, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _scrollTrackPaint    = new() { Color = new SKColor(50, 50, 65, 200),   Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _scrollThumbPaint    = new() { Color = new SKColor(130, 130, 165, 210), Style = SKPaintStyle.Fill,  IsAntialias = true };

    public PrestigeRenderer(
        GameControllerService gameControllerService,
        LocalizationService   localization,
        Action<bool>          prestigeRequested,
        TooltipRenderer       tooltipRenderer)
    {
        _gameControllerService = gameControllerService;
        _localization          = localization;
        _prestigeRequested     = prestigeRequested;
        _tooltipRenderer       = tooltipRenderer;
        _essenceLossPopup      = new PrestigeEssenceLossPopupRenderer(localization,
            onConfirm: corrupted => _prestigeRequested(corrupted));
    }

    public override void Initialize(SKSize canvasSize)
    {
        base.Initialize(canvasSize);
        _essenceLossPopup.Initialize(canvasSize);
    }

    protected override void OnFontsUpdated(float s)
    {
        _smallFont?.Dispose();
        _smallFont = new SKFont { Size = 9 * s, Typeface = SkiaFonts.Regular };
    }

    public override void Close()
    {
        base.Close();
        _scrollOffsetPx      = 0f;
        _isDraggingScrollbar = false;
        _essenceLossPopup.Close();
    }

    public void HandlePointerMoved(SKPoint position)
    {
        if (!IsOpen) return;
        _lastPointerPosition = position;
        if (_essenceLossPopup.IsOpen) return;

        if (_isDraggingScrollbar)
        {
            float dy          = position.Y - _scrollDragStartY;
            float thumbRange   = _scrollTrackRect.Height - _scrollThumbRect.Height;
            float maxScroll    = Math.Max(0, _contentH - _viewportH);
            float scrollPerPx  = thumbRange > 0 ? maxScroll / thumbRange : 0;
            _scrollOffsetPx = Math.Clamp(_scrollDragStartOffset + dy * scrollPerPx, 0, maxScroll);
        }
    }

    public void HandlePointerReleased(SKPoint position)
    {
        _isDraggingScrollbar = false;
    }

    public void HandleScroll(float delta)
    {
        if (!IsOpen || _essenceLossPopup.IsOpen) return;
        float step      = RowHeight + 4;
        float dir        = delta > 0 ? -1f : 1f;
        float maxScroll  = Math.Max(0, _contentH - _viewportH);
        _scrollOffsetPx  = Math.Clamp(_scrollOffsetPx + dir * step, 0, maxScroll);
    }

    public void Render(SKCanvas canvas)
    {
        if (!IsOpen || Disposed) return;

        var popup = GetPopupRect();
        DrawBackground(canvas, popup);

        SkiaTextUtils.DrawText(canvas, _localization.Get("prestige_title"), popup.MidX, popup.Top + 30, SKTextAlign.Center, TitleFont!, TextPaint);

        _closeButtonRect = GetCloseRect(popup);
        DrawCloseButton(canvas, _closeButtonRect);

        var controller       = _gameControllerService.MainGameController.PrestigeController;
        var sources          = controller.GetPrestigePointSources();
        bool wondersUnlocked = controller.WondersUnlocked();
        bool showSpireBonus      = controller.GetMaxCorruptionLevelCleared() > 0;
        double gainBonus         = controller.GetPrestigeGainBonus();
        double raceBonus         = controller.GetRaceGainBonus();
        double seaportBonus      = controller.GetSeaportPrestigeBonus();
        double templeBonus       = controller.GetTemplePrestigeBonus();
        double civDestroyedBonus = controller.GetCivilizationsDestroyedBonus();
        int tier                 = controller.GetTier();
        double tierBonus         = controller.GetTierBonus();
        double greatLighthouseBonus  = controller.GetGreatLighthousePrestigeBonus();
        bool showGainBonus       = gainBonus > 0;
        bool showRaceBonus       = raceBonus != 0;
        bool showSeaportBonus    = seaportBonus > 0;
        bool showTempleBonus     = templeBonus > 0;
        bool showCivBonus        = civDestroyedBonus > 0;
        bool showGreatLighthouseBonus = greatLighthouseBonus > 0;
        bool showTierBonus       = tierBonus > 0;
        bool showTierPicker      = ShowTierPicker;

        // ── Footer fixe (bas de la popup, jamais affecté par le défilement) ──────────
        // Merveille + Total restent toujours visibles sous la zone défilante, comme demandé.
        const float footerRowH     = 28f;
        const float footerGap      = 10f;
        const float tierPickerBtn  = 24f;
        float tierPickerBlockH = showTierPicker ? tierPickerBtn + 8f : 0f;
        float belowTotalH      = ButtonHeight + tierPickerBlockH;
        float totalRowTop      = popup.Bottom - Padding - belowTotalH - footerGap - footerRowH;
        float wonderRowTop     = wondersUnlocked ? totalRowTop - footerRowH : totalRowTop;
        float footerTop        = wonderRowTop;

        // ── Zone défilante (sources + monstres + bonus) ──────────────────────────────
        float headerBottom = popup.Top + 68;
        _viewportRect = new SKRect(popup.Left, headerBottom, popup.Right, footerTop - 6);
        _viewportH    = _viewportRect.Height;

        _contentH = sources.Count * SourceRowHeight + RowHeight;
        if (showGreatLighthouseBonus) _contentH += RowHeight;
        if (showSpireBonus)           _contentH += RowHeight;
        if (showGainBonus)            _contentH += RowHeight;
        if (showRaceBonus)            _contentH += RowHeight;
        if (showTempleBonus)          _contentH += RowHeight;
        if (showSeaportBonus)         _contentH += RowHeight;
        if (showCivBonus)             _contentH += RowHeight;
        if (showTierBonus)            _contentH += RowHeight;

        float maxScroll = Math.Max(0, _contentH - _viewportH);
        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx, 0, maxScroll);
        bool needsScroll = _contentH > _viewportH + 1f;

        _hoverRects.Clear();
        _scrollHoverRects.Clear();

        canvas.Save();
        // Le clip est étendu vers le haut pour ne pas rogner les majuscules/accents de la première ligne,
        // dont le texte est dessiné avec sa baseline juste sur le bord de _viewportRect (hauteur d'ascendante
        // ~= BodyFontSize), et légèrement vers le bas pour les descendantes de la dernière ligne visible.
        canvas.ClipRect(new SKRect(_viewportRect.Left, _viewportRect.Top - BodyFontSize, _viewportRect.Right, _viewportRect.Bottom + 4));
        canvas.Translate(0, -_scrollOffsetPx);

        float y = _viewportRect.Top;

        foreach (var source in sources)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get(source.LabelKey), popup.Left + Padding, y, BodyFont!, TextPaint);
            SkiaTextUtils.DrawText(canvas, SkiaTextUtils.FormatNumber(source.Points), popup.Right - Padding, y, SKTextAlign.Right, BtnFont!, TextPaint);
            if (source.TooltipKey != null)
                _scrollHoverRects.Add((new SKRect(popup.Left, y - BodyFont!.Size, popup.Right, y + 6), new[] { source.TooltipKey }));
            y += SourceRowHeight;
        }

        // Monstres
        bool hasMonstersLeft = controller.HasSurfaceMonsters();
        canvas.DrawLine(popup.Left + Padding, y, popup.Right - Padding, y, _separatorPaint);
        float monsterTextY = y + 14;
        SkiaTextUtils.DrawText(canvas, _localization.Get("prestige_monster_bonus"), popup.Left + Padding, monsterTextY, BodyFont!, SubtlePaint);
        if (hasMonstersLeft)
            SkiaTextUtils.DrawText(canvas, "+0%",  popup.Right - Padding, monsterTextY, SKTextAlign.Right, BtnFont!, _warningTextPaint);
        else
            SkiaTextUtils.DrawText(canvas, "+20%", popup.Right - Padding, monsterTextY, SKTextAlign.Right, BtnFont!, SubtlePaint);
        _scrollHoverRects.Add((new SKRect(popup.Left, y, popup.Right, y + RowHeight), new[] { "prestige_tooltip_monster_bonus" }));
        y += RowHeight;

        // Grand Phare (affiché quand niveau > 0)
        if (showGreatLighthouseBonus)
        {
            int greatLighthouseLevel = controller.GetGreatLighthouseLevel();
            string greatLighthouseLabel = _localization.GetFormated("prestige_great_lighthouse_bonus", greatLighthouseLevel);
            var greatLighthouseTooltipKeys = new List<string> { "prestige_tooltip_great_lighthouse_bonus" };
            if (greatLighthouseLevel >= 2) greatLighthouseTooltipKeys.Add("prestige_tooltip_great_lighthouse_secondary_maritime");
            if (greatLighthouseLevel >= 3) greatLighthouseTooltipKeys.Add("prestige_tooltip_great_lighthouse_secondary_tier_picker");
            y = DrawBonusRow(canvas, popup, y, greatLighthouseLabel, $"+{greatLighthouseBonus * 100:0}%", SubtlePaint, greatLighthouseTooltipKeys.ToArray());
        }

        // Bonus de nettoyage de la Corruption (affiché dès qu'une zone a été entièrement nettoyée)
        if (showSpireBonus)
        {
            string spireLabel = _localization.GetFormated("prestige_corruption_spire_bonus", controller.GetMaxCorruptionLevelCleared());
            y = DrawBonusRow(canvas, popup, y, spireLabel, $"×{controller.GetCorruptionClearBonusMultiplier()}", SubtlePaint, new[] { "prestige_tooltip_corruption_spire_bonus" });
        }

        // Bonus Prestige/Recherche (affiché quand > 0)
        if (showGainBonus)
            y = DrawBonusRow(canvas, popup, y, _localization.Get("prestige_gain_bonus"), $"+{gainBonus * 100:0.#}%", SubtlePaint, new[] { "prestige_tooltip_prestige_gain_bonus" });

        // Bonus/malus de prestige propre à la race (ex : Gobelins -25%), distinct du Bonus Prestige/Recherche
        if (showRaceBonus)
        {
            var raceBonusPaint = raceBonus < 0 ? _warningTextPaint : SubtlePaint;
            y = DrawBonusRow(canvas, popup, y, _localization.Get("prestige_race_bonus"), $"{(raceBonus >= 0 ? "+" : "")}{raceBonus * 100:0.#}%", raceBonusPaint, new[] { "prestige_tooltip_race_bonus" });
        }

        // Bonus Grand Temple (affiché quand > 0)
        if (showTempleBonus)
        {
            int templeCount = controller.GetTempleCount();
            y = DrawBonusRow(canvas, popup, y, _localization.GetFormated("prestige_temple_bonus", templeCount), $"+{templeBonus * 100:0.#}%", SubtlePaint, new[] { "prestige_tooltip_temple_bonus" });
        }

        // Bonus Ports niv. 4 (affiché quand > 0)
        if (showSeaportBonus)
        {
            int portCount = controller.GetSeaportLevel4Count();
            y = DrawBonusRow(canvas, popup, y, _localization.GetFormated("prestige_seaport_bonus", portCount), $"+{seaportBonus * 100:0}%", SubtlePaint, new[] { "prestige_tooltip_seaport_bonus" });
        }

        // Bonus civilisations détruites (affiché quand > 0)
        if (showCivBonus)
        {
            int civCount = controller.GetCivilizationsDestroyedCount();
            y = DrawBonusRow(canvas, popup, y, _localization.GetFormated("prestige_civilizations_destroyed_bonus", civCount), $"+{civDestroyedBonus * 100:0}%", SubtlePaint, new[] { "prestige_tooltip_civilizations_destroyed_bonus" });
        }

        // Bonus de palier (Tier) — affiché quand > 0
        if (showTierBonus)
            y = DrawBonusRow(canvas, popup, y, _localization.GetFormated("prestige_tier_bonus", tier), $"+{tierBonus * 100:0}%", SubtlePaint, new[] { "prestige_tooltip_tier_bonus" });

        canvas.Restore();

        if (needsScroll)
            DrawScrollbar(canvas, maxScroll);

        // Merveille (affichée quand débloquée) — toujours affichée sous la zone défilante, juste au-dessus
        // du Total, et seul bonus affiché sous forme de multiplicateur (×N) plutôt qu'en pourcentage.
        if (wondersUnlocked)
        {
            canvas.DrawLine(popup.Left + Padding, wonderRowTop, popup.Right - Padding, wonderRowTop, _separatorPaint);
            var (wonderLevel, timeFactor, runTicks) = controller.GetWonderBonusDetails();
            string duration    = FormatRunDuration(runTicks);
            string wonderLabel = _localization.GetFormated("prestige_wonder_bonus", wonderLevel, timeFactor, duration);
            float wonderRowY = wonderRowTop + 14;

            bool canSkipWonderTime = controller.CanSkipToNextWonderMultiplier();
            const float skipBtnSize = 20f;
            const float skipBtnGap  = 6f;
            _wonderSkipButtonRect = new SKRect(popup.Right - Padding - skipBtnSize, wonderRowY - skipBtnSize + 4, popup.Right - Padding, wonderRowY + 4);
            canvas.DrawRoundRect(_wonderSkipButtonRect, 4, 4, canSkipWonderTime ? _buttonPaint : _buttonDisabledPaint);
            SkiaTextUtils.DrawText(canvas, "⏩", _wonderSkipButtonRect.MidX, _wonderSkipButtonRect.MidY + 5, SKTextAlign.Center, BtnFont!, TextPaint);

            SkiaTextUtils.DrawText(canvas, wonderLabel, popup.Left + Padding, wonderRowY, BodyFont!, SubtlePaint);
            SkiaTextUtils.DrawText(canvas, $"×{Math.Max(1, wonderLevel * timeFactor)}", _wonderSkipButtonRect.Left - skipBtnGap, wonderRowY, SKTextAlign.Right, BtnFont!, SubtlePaint);
            _hoverRects.Add((_wonderSkipButtonRect, new[] { canSkipWonderTime ? "tooltip_wonder_skip_time" : "tooltip_wonder_skip_time_disabled" }));
            _hoverRects.Add((new SKRect(popup.Left, wonderRowTop, _wonderSkipButtonRect.Left - skipBtnGap, wonderRowTop + RowHeight), new[] { "prestige_tooltip_wonder_bonus" }));
        }
        else
        {
            _wonderSkipButtonRect = SKRect.Empty;
        }

        // Total
        canvas.DrawLine(popup.Left + Padding, totalRowTop, popup.Right - Padding, totalRowTop, _separatorPaint);
        var total = controller.CalculatePrestigePoints();
        SkiaTextUtils.DrawText(canvas, _localization.Get("prestige_total"), popup.Left + Padding, totalRowTop + 14, BtnFont!, TextPaint);
        SkiaTextUtils.DrawText(canvas, SkiaTextUtils.FormatNumber(total), popup.Right - Padding, totalRowTop + 14, SKTextAlign.Right, BtnFont!, TextPaint);

        // Tier de la prochaine île (Grand Phare niveau 3) — dans l'espace réservé sous le Total
        if (showTierPicker)
        {
            int minTier = tier;
            int maxTier = minTier + PrestigeController.MaxNextIslandTierChoiceBonus;
            int chosenTier = controller.GetNextIslandTierChoice();
            float btnSize = 24f;
            float rowY = popup.Bottom - Padding - ButtonHeight - 8 - btnSize;
            _tierMinusButtonRect = new SKRect(popup.Left + Padding, rowY, popup.Left + Padding + btnSize, rowY + btnSize);
            _tierPlusButtonRect  = new SKRect(popup.Right - Padding - btnSize, rowY, popup.Right - Padding, rowY + btnSize);
            canvas.DrawRoundRect(_tierMinusButtonRect, 4, 4, chosenTier > minTier ? _buttonPaint : _buttonDisabledPaint);
            SkiaTextUtils.DrawText(canvas, "-", _tierMinusButtonRect.MidX, _tierMinusButtonRect.MidY + 5, SKTextAlign.Center, BtnFont!, TextPaint);
            canvas.DrawRoundRect(_tierPlusButtonRect, 4, 4, chosenTier < maxTier ? _buttonPaint : _buttonDisabledPaint);
            SkiaTextUtils.DrawText(canvas, "+", _tierPlusButtonRect.MidX, _tierPlusButtonRect.MidY + 5, SKTextAlign.Center, BtnFont!, TextPaint);
            SkiaTextUtils.DrawText(canvas, _localization.GetFormated("prestige_next_island_tier_picker", chosenTier),
                popup.MidX, rowY + btnSize / 2 + 5, SKTextAlign.Center, BodyFont!, TextPaint);
            _hoverRects.Add((new SKRect(popup.Left, rowY - 4, popup.Right, rowY + btnSize + 4), new[] { "tooltip_prestige_next_island_tier_picker" }));
        }
        else
        {
            _tierMinusButtonRect = SKRect.Empty;
            _tierPlusButtonRect  = SKRect.Empty;
        }

        bool canPrestige     = controller.PrestigeIsAvailable();
        bool hasEnoughPoints = controller.CalculatePrestigePoints() >= PrestigeController.PrestigeRequiredPoints;
        bool hasImperialPort = controller.HasImperialPort();
        bool hasSpire        = controller.HasCorruptionSpireBuilt();

        if (hasSpire)
        {
            const float gap  = 10;
            const float btnW = 150;
            _prestigeButtonRect = new SKRect(popup.MidX - btnW - gap / 2, popup.Bottom - Padding - ButtonHeight, popup.MidX - gap / 2, popup.Bottom - Padding);
            _corruptedPrestigeButtonRect = new SKRect(popup.MidX + gap / 2, popup.Bottom - Padding - ButtonHeight, popup.MidX + gap / 2 + btnW, popup.Bottom - Padding);

            canvas.DrawRoundRect(_corruptedPrestigeButtonRect, 7, 7, canPrestige ? _corruptedButtonPaint : _buttonDisabledPaint);
            SkiaTextUtils.DrawText(canvas, _localization.Get("prestige_corrupted_action"), _corruptedPrestigeButtonRect.MidX, _corruptedPrestigeButtonRect.MidY - 1, SKTextAlign.Center, BtnFont!, TextPaint);
            int currentCorruptionLevel = controller.GetCorruptionLevel();
            SkiaTextUtils.DrawText(canvas, $"{currentCorruptionLevel} -> {currentCorruptionLevel + 1}", _corruptedPrestigeButtonRect.MidX, _corruptedPrestigeButtonRect.MidY + 13, SKTextAlign.Center, _smallFont!, TextPaint);

            _hoverRects.Add((_corruptedPrestigeButtonRect, new[]
            {
                "prestige_tooltip_corrupted_action",
                "prestige_tooltip_corrupted_action_risk",
                "prestige_tooltip_corrupted_action_reward",
            }));
        }
        else
        {
            _corruptedPrestigeButtonRect = SKRect.Empty;
            _prestigeButtonRect = new SKRect(popup.MidX - 75, popup.Bottom - Padding - ButtonHeight, popup.MidX + 75, popup.Bottom - Padding);
        }

        canvas.DrawRoundRect(_prestigeButtonRect, 7, 7, canPrestige ? _buttonPaint : _buttonDisabledPaint);
        SkiaTextUtils.DrawText(canvas, _localization.Get("prestige_action"), _prestigeButtonRect.MidX, _prestigeButtonRect.MidY + 5, SKTextAlign.Center, BtnFont!, TextPaint);
        _hoverRects.Add((_prestigeButtonRect, new[] { "prestige_tooltip_action" }));

        if (hasEnoughPoints && !hasImperialPort)
        {
            SkiaTextUtils.DrawText(canvas,
                _localization.Get("prestige_requires_imperial_port"),
                popup.MidX,
                _prestigeButtonRect.Bottom + 18,
                SKTextAlign.Center,
                BodyFont!, SubtlePaint);
        }

        bool tooltipSet = false;
        foreach (var (rect, keys) in _hoverRects)
        {
            if (rect.Contains(_lastPointerPosition.X, _lastPointerPosition.Y))
            {
                _tooltipRenderer.SetTooltipLines(keys.Select(_localization.Get).ToArray(), new SKPoint(rect.Right, rect.Top));
                tooltipSet = true;
                break;
            }
        }

        if (!tooltipSet && _viewportRect.Contains(_lastPointerPosition.X, _lastPointerPosition.Y))
        {
            var adj = new SKPoint(_lastPointerPosition.X, _lastPointerPosition.Y + _scrollOffsetPx);
            foreach (var (rect, keys) in _scrollHoverRects)
            {
                if (rect.Contains(adj.X, adj.Y))
                {
                    _tooltipRenderer.SetTooltipLines(keys.Select(_localization.Get).ToArray(), new SKPoint(rect.Right, rect.Top - _scrollOffsetPx));
                    break;
                }
            }
        }

        _essenceLossPopup.Render(canvas, CanvasSize);
    }

    // Ouvre une confirmation si le prestige entraînerait la perte d'essences divines
    // (au-delà de ce que le Reliquaire Sacré/Renforcé permet de conserver), sinon prestige immédiat.
    private void TryPrestige(bool corrupted)
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

    // Dessine une ligne de bonus standard (séparateur + libellé + valeur, 28px de haut) dans la zone défilante
    // et retourne le Y de départ de la ligne suivante.
    private float DrawBonusRow(SKCanvas canvas, SKRect popup, float y, string label, string value, SKPaint valuePaint, string[] tooltipKeys)
    {
        canvas.DrawLine(popup.Left + Padding, y, popup.Right - Padding, y, _separatorPaint);
        float textY = y + 14;
        SkiaTextUtils.DrawText(canvas, label, popup.Left + Padding, textY, BodyFont!, SubtlePaint);
        SkiaTextUtils.DrawText(canvas, value, popup.Right - Padding, textY, SKTextAlign.Right, BtnFont!, valuePaint);
        _scrollHoverRects.Add((new SKRect(popup.Left, y, popup.Right, y + RowHeight), tooltipKeys));
        return y + RowHeight;
    }

    private void DrawScrollbar(SKCanvas canvas, float maxScroll)
    {
        const float scrollW = 5f;
        float trackX   = _viewportRect.Right - scrollW - 4;
        float trackTop = _viewportRect.Top;
        float trackH   = _viewportRect.Height;

        _scrollTrackRect = new SKRect(trackX, trackTop, trackX + scrollW, trackTop + trackH);

        float thumbRatio = _viewportH / _contentH;
        float thumbH     = Math.Max(20f, thumbRatio * trackH);
        float thumbTop   = trackTop + (_scrollOffsetPx / maxScroll) * (trackH - thumbH);
        _scrollThumbRect = new SKRect(trackX, thumbTop, trackX + scrollW, thumbTop + thumbH);

        canvas.DrawRoundRect(_scrollTrackRect, 3, 3, _scrollTrackPaint);
        canvas.DrawRoundRect(_scrollThumbRect, 3, 3, _scrollThumbPaint);
    }

    public bool HandlePointerPressed(SKPoint position, PointerButton button)
    {
        if (!IsOpen) return false;

        if (_essenceLossPopup.IsOpen)
        {
            _essenceLossPopup.HandlePointerPressed(position, button);
            return true;
        }

        if (button != PointerButton.Left)
            return GetPopupRect().Contains(position.X, position.Y);

        if (_closeButtonRect.Contains(position.X, position.Y)) { Close(); return true; }

        if (!_scrollThumbRect.IsEmpty && _scrollThumbRect.Contains(position.X, position.Y))
        {
            _isDraggingScrollbar   = true;
            _scrollDragStartY      = position.Y;
            _scrollDragStartOffset = _scrollOffsetPx;
            return true;
        }
        if (!_scrollTrackRect.IsEmpty && _scrollTrackRect.Contains(position.X, position.Y))
        {
            float relY      = position.Y - _scrollTrackRect.Top;
            float maxScroll = Math.Max(0, _contentH - _viewportH);
            _scrollOffsetPx = Math.Clamp(relY / _scrollTrackRect.Height * maxScroll, 0, maxScroll);
            return true;
        }

        var prestigeController = _gameControllerService.MainGameController.PrestigeController;

        if (!_tierMinusButtonRect.IsEmpty && _tierMinusButtonRect.Contains(position.X, position.Y))
        {
            prestigeController.SetNextIslandTierChoice(prestigeController.GetNextIslandTierChoice() - 1);
            return true;
        }

        if (!_tierPlusButtonRect.IsEmpty && _tierPlusButtonRect.Contains(position.X, position.Y))
        {
            prestigeController.SetNextIslandTierChoice(prestigeController.GetNextIslandTierChoice() + 1);
            return true;
        }

        if (!_wonderSkipButtonRect.IsEmpty && _wonderSkipButtonRect.Contains(position.X, position.Y))
        {
            prestigeController.SkipToNextWonderMultiplier();
            return true;
        }

        if (_prestigeButtonRect.Contains(position.X, position.Y)
            && _gameControllerService.MainGameController.PrestigeController.PrestigeIsAvailable())
        {
            TryPrestige(corrupted: false);
            return true;
        }

        if (!_corruptedPrestigeButtonRect.IsEmpty && _corruptedPrestigeButtonRect.Contains(position.X, position.Y)
            && _gameControllerService.MainGameController.PrestigeController.PrestigeIsAvailable())
        {
            TryPrestige(corrupted: true);
            return true;
        }

        if (!GetPopupRect().Contains(position.X, position.Y)) { Close(); return false; }
        return true;
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

    private SKRect GetPopupRect()
    {
        float width  = Math.Min(PopupWidth,  CanvasSize.Width  - 30);
        float height = Math.Min(PopupHeight, CanvasSize.Height - 30);
        float x = (CanvasSize.Width  - width)  / 2;
        float y = (CanvasSize.Height - height) / 2;
        return new SKRect(x, y, x + width, y + height);
    }

    public override void Dispose()
    {
        if (Disposed) return;
        _buttonPaint.Dispose();
        _buttonDisabledPaint.Dispose();
        _corruptedButtonPaint.Dispose();
        _warningTextPaint.Dispose();
        _separatorPaint.Dispose();
        _scrollTrackPaint.Dispose();
        _scrollThumbPaint.Dispose();
        _smallFont?.Dispose();
        _essenceLossPopup.Dispose();
        base.Dispose();
    }
}
