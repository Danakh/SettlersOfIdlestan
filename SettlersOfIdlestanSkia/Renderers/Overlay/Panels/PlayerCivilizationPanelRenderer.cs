using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Buildings;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Panels;

public sealed class PlayerCivilizationPanelRenderer : PanelRendererBase
{
    private const float PanelLeft    = 10f;
    private const float PanelWidth   = 240f;
    private const float PanelPadding = 12f;
    private const float BtnHeight    = 38f;
    private const float BtnSpacing   = 6f;
    private const float TitleSize    = 11f;
    private const float TitleHeight  = 20f;
    private const float ToggleWidth  = 46f;
    private const float ToggleHeight = 24f;
    private const float RowHeight    = 36f;
    private const float SepSpacing   = 8f;

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;
    private readonly Action _closeAll;
    private readonly TradePopupRenderer _tradeRenderer;
    private readonly PrestigeRenderer _prestigeRenderer;
    private WonderSelectionService? _wonderSelectionService;
    private readonly TooltipRenderer _tooltipRenderer;

    public bool IsCollapsed  => Collapsed;
    public void Collapse()   => Collapsed = true;
    public Action? OnExpanded { get; set; }

    private SKRect _tradeButtonRect    = SKRect.Empty;
    private SKRect _prestigeButtonRect = SKRect.Empty;
    private SKRect _wonderButtonRect   = SKRect.Empty;
    private SKRect _barracksToggleRect = SKRect.Empty;
    private SKRect _labToggleRect      = SKRect.Empty;
    private SKRect _smelterToggleRect      = SKRect.Empty;
    private SKRect _steelWeaponsToggleRect = SKRect.Empty;

    private bool _hoveredTrade, _hoveredPrestige, _hoveredWonder;
    private bool _hoveredBarracks, _hoveredLab, _hoveredSmelter, _hoveredSteelWeapons;
    private bool _wonderEnabled;
    private bool _disposed;

    // CivPanel-specific paints
    private SKPaint? _sectionTitlePaint;
    private SKPaint? _separatorPaint;
    private SKPaint? _btnPaint;
    private SKPaint? _btnHoverPaint;
    private SKPaint? _btnDisabledPaint;
    private SKPaint? _btnDisabledTxtPaint;
    private SKPaint? _onPaint;
    private SKPaint? _onHoverPaint;
    private SKPaint? _offPaint;
    private SKPaint? _offHoverPaint;
    private SKPaint? _toggleBorderPaint;
    private SKPaint? _toggleKnobPaint;
    private SKPaint? _rowLabelPaint;
    private SKPaint? _rowLabelDimPaint;
    private SKPaint? _dimTogglePaint;
    private SKPaint? _indeterminatePaint;
    private SKPaint? _indeterminateHoverPaint;

    // CivPanel-specific fonts (different sizes than base Font10/12/15)
    private SKFont? _sectionFont;
    private SKFont? _btnFont;
    private SKFont? _btnSmFont;
    private SKFont? _labelFont;

    public PlayerCivilizationPanelRenderer(
        GameControllerService gameControllerService,
        LocalizationService localization,
        Action closeAll,
        TradePopupRenderer tradeRenderer,
        PrestigeRenderer prestigeRenderer,
        WonderSelectionService? wonderSelectionService,
        TooltipRenderer tooltipRenderer)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _closeAll = closeAll;
        _tradeRenderer = tradeRenderer;
        _prestigeRenderer = prestigeRenderer;
        _wonderSelectionService = wonderSelectionService;
        _tooltipRenderer = tooltipRenderer;
    }

    public override void Initialize(SKSize canvasSize)
    {
        base.Initialize(canvasSize);
        _sectionTitlePaint    = new SKPaint { Color = new SKColor(160, 160, 175),      IsAntialias = true };
        _separatorPaint       = new SKPaint { Color = new SKColor(60, 60, 80),         StrokeWidth = 0.8f, Style = SKPaintStyle.Stroke };
        _btnPaint             = new SKPaint { Color = new SKColor(46, 125, 50),        Style = SKPaintStyle.Fill, IsAntialias = true };
        _btnHoverPaint        = new SKPaint { Color = new SKColor(60, 150, 64),        Style = SKPaintStyle.Fill, IsAntialias = true };
        _btnDisabledPaint     = new SKPaint { Color = new SKColor(70, 70, 78),         Style = SKPaintStyle.Fill, IsAntialias = true };
        _btnDisabledTxtPaint  = new SKPaint { Color = new SKColor(160, 160, 165),      IsAntialias = true };
        _onPaint              = new SKPaint { Color = new SKColor(46, 125, 50),        Style = SKPaintStyle.Fill, IsAntialias = true };
        _onHoverPaint         = new SKPaint { Color = new SKColor(60, 150, 64),        Style = SKPaintStyle.Fill, IsAntialias = true };
        _offPaint             = new SKPaint { Color = new SKColor(160, 50, 50),        Style = SKPaintStyle.Fill, IsAntialias = true };
        _offHoverPaint        = new SKPaint { Color = new SKColor(185, 65, 65),        Style = SKPaintStyle.Fill, IsAntialias = true };
        _toggleBorderPaint    = new SKPaint { Color = new SKColor(180, 180, 200),      StrokeWidth = 1.2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
        _toggleKnobPaint      = new SKPaint { Color = SKColors.White,                  Style = SKPaintStyle.Fill, IsAntialias = true };
        _rowLabelPaint        = new SKPaint { Color = new SKColor(215, 215, 225),      IsAntialias = true };
        _rowLabelDimPaint     = new SKPaint { Color = new SKColor(140, 140, 150, 160), IsAntialias = true };
        _dimTogglePaint       = new SKPaint { Color = new SKColor(70, 70, 80),         Style = SKPaintStyle.Fill, IsAntialias = true };
        _indeterminatePaint      = new SKPaint { Color = new SKColor(90, 90, 105),     Style = SKPaintStyle.Fill, IsAntialias = true };
        _indeterminateHoverPaint = new SKPaint { Color = new SKColor(110, 110, 125),   Style = SKPaintStyle.Fill, IsAntialias = true };
    }

    public void ConnectWonderSelectionService(WonderSelectionService service)
        => _wonderSelectionService = service;

    public override void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState) return;

        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return;

        float s = context.UiScale;
        float prevScale = LastUiScale;
        UpdateScale(s);
        if (LastUiScale != prevScale)
        {
            _sectionFont?.Dispose(); _sectionFont = new SKFont { Size = TitleSize * s, Typeface = SkiaFonts.Regular };
            _btnFont?.Dispose();     _btnFont     = new SKFont { Size = 13f * s,       Typeface = SkiaFonts.Bold };
            _btnSmFont?.Dispose();   _btnSmFont   = new SKFont { Size = 11f * s,       Typeface = SkiaFonts.Bold };
            _labelFont?.Dispose();   _labelFont   = new SKFont { Size = 13f * s,       Typeface = SkiaFonts.Regular };
        }

        float panelLeft    = PanelLeft * s;
        float panelWidth   = PanelWidth * s;
        float panelPadding = PanelPadding * s;
        float btnHeight    = BtnHeight * s;
        float btnSpacing   = BtnSpacing * s;
        float titleSize    = TitleSize * s;
        float titleHeight  = TitleHeight * s;
        float rowHeight    = RowHeight * s;
        float sepSpacing   = SepSpacing * s;
        float collapseTabW = CollapseTabW * s;
        float collapseTabH = CollapseTabH * s;

        bool tradeVisible    = IsTradeVisible();
        bool prestigeVisible = IsPrestigeVisible();
        bool prestigeAvail   = prestigeVisible && IsPrestigeAvailable();
        int  prestigePoints  = prestigeVisible ? GetPrestigePoints() : 0;
        bool wonderVisible   = IsWonderVisible() && CanPlaceWonder();
        _wonderEnabled = wonderVisible && context.CurrentLayer == 0;
        bool hasBarracks     = HasBuilt<Barracks>(civ);
        bool hasLabs         = HasBuilt<Laboratory>(civ);
        bool hasSmelters     = HasBuilt<Smelter>(civ);
        bool hasSteelWeapons = hasBarracks && civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_STEEL_WEAPONS);

        bool showActions  = tradeVisible || prestigeVisible || wonderVisible;
        bool showControls = hasBarracks || hasLabs || hasSmelters;

        _tradeButtonRect = _prestigeButtonRect = _wonderButtonRect = SKRect.Empty;
        _barracksToggleRect = _labToggleRect = _smelterToggleRect = _steelWeaponsToggleRect = SKRect.Empty;

        if (!showActions && !showControls)
        {
            PanelBounds = SKRect.Empty;
            CollapseTabRect = SKRect.Empty;
            return;
        }

        float contentW = panelWidth - panelPadding * 2;
        float panelTop = (TopOverride > 0f ? TopOverride : PlayerResourcesOverlayRenderer.BarHeight * s) + 10f * s;
        float tabTop   = panelTop + 8f * s;

        if (Collapsed)
        {
            CollapseTabRect = new SKRect(0, tabTop, collapseTabW, tabTop + collapseTabH);
            PanelBounds = CollapseTabRect;
            DrawCollapseTabRect(canvas, CollapseTabRect, true);
            return;
        }

        // Measure total panel height
        float h = panelPadding;
        if (showActions)
        {
            int actionCount = (tradeVisible ? 1 : 0) + (prestigeVisible ? 1 : 0) + (wonderVisible ? 1 : 0);
            int actionRows  = (actionCount + 1) / 2;
            h += titleHeight + actionRows * (btnHeight + btnSpacing);
        }
        if (showActions && showControls) h += sepSpacing * 2 + 1f;
        if (showControls)
        {
            h += titleHeight;
            if (hasBarracks)     h += rowHeight;
            if (hasSteelWeapons) h += rowHeight;
            if (hasLabs)         h += rowHeight;
            if (hasSmelters)     h += rowHeight;
        }
        h += panelPadding;

        PanelBounds = new SKRect(panelLeft, panelTop, panelLeft + panelWidth, panelTop + h);
        DrawPanelChrome(canvas, panelLeft, panelTop, panelWidth, h, cornerRadius: 8f);

        // Collapse handle — shifted left to slightly overlap the panel
        float tabOverlap = 6f * s;
        CollapseTabRect = new SKRect(panelLeft + panelWidth - tabOverlap, tabTop, panelLeft + panelWidth - tabOverlap + collapseTabW, tabTop + collapseTabH);
        DrawCollapseTabRect(canvas, CollapseTabRect, false);

        float x = panelLeft + panelPadding;
        float y = panelTop + panelPadding;

        if (showActions)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get("panel_civ_actions"), x, y + titleSize, _sectionFont, _sectionTitlePaint);
            y += titleHeight;

            float colGap = 6f * s;
            float colW   = (contentW - colGap) / 2f;
            int   actionCount = (tradeVisible ? 1 : 0) + (prestigeVisible ? 1 : 0) + (wonderVisible ? 1 : 0);
            float actionsY   = y;
            int   btnIdx     = 0;

            SKRect BtnRect(int idx)
            {
                float col     = idx % 2;
                float row     = idx / 2;
                bool  lastOdd = idx == actionCount - 1 && actionCount % 2 == 1;
                float bw      = lastOdd ? contentW : colW;
                float bx      = x + col * (colW + colGap);
                float by      = actionsY + row * (btnHeight + btnSpacing);
                return new SKRect(bx, by, bx + bw, by + btnHeight);
            }

            if (tradeVisible)
            {
                _tradeButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_tradeButtonRect, 6 * s, 6 * s, _hoveredTrade ? _btnHoverPaint : _btnPaint);
                SkiaTextUtils.DrawText(canvas, _localization.Get("trade_action"), _tradeButtonRect.MidX, _tradeButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, TextPaint);
            }

            if (prestigeVisible)
            {
                _prestigeButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_prestigeButtonRect, 6 * s, 6 * s, prestigeAvail ? (_hoveredPrestige ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                string prestigeLabel = $"{_localization.Get("prestige_action")} ({prestigePoints})";
                SkiaTextUtils.DrawText(canvas, prestigeLabel, _prestigeButtonRect.MidX, _prestigeButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, prestigeAvail ? TextPaint : _btnDisabledTxtPaint);
            }

            if (wonderVisible)
            {
                _wonderButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_wonderButtonRect, 6 * s, 6 * s, _wonderEnabled ? (_hoveredWonder ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                SkiaTextUtils.DrawText(canvas, _localization.Get("wonder_action_short"), _wonderButtonRect.MidX, _wonderButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, _wonderEnabled ? TextPaint : _btnDisabledTxtPaint);
            }

            y = actionsY + ((btnIdx + 1) / 2) * (btnHeight + btnSpacing);
        }

        if (showActions && showControls)
        {
            y += sepSpacing;
            canvas.DrawLine(x, y, x + contentW, y, _separatorPaint);
            y += sepSpacing + 1f;
        }

        if (showControls)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get("panel_civ_controls"), x, y + titleSize, _sectionFont, _sectionTitlePaint);
            y += titleHeight;

            if (hasBarracks)
            {
                bool? allOn = AreAllActiveNullable<Barracks>(civ);
                _barracksToggleRect = DrawToggleRow(canvas, x, y, allOn, _hoveredBarracks, _localization.Get("building_barracks_name"));
                y += rowHeight;
            }

            if (hasSteelWeapons)
            {
                bool? allOn = AreAllSteelWeaponsActiveNullable(civ);
                bool noBarracksActive = !civ.Cities.SelectMany(c => c.Buildings.OfType<Barracks>()).Any(b => b.Level >= 1 && b.ActivationStatus == ActivationStatus.ACTIVE);
                _steelWeaponsToggleRect = DrawToggleRow(canvas, x, y, allOn, _hoveredSteelWeapons, _localization.Get("toggle_steel_weapons"), isDimmed: noBarracksActive);
                y += rowHeight;
            }

            if (hasLabs)
            {
                bool? allOn = AreAllActiveNullable<Laboratory>(civ);
                _labToggleRect = DrawToggleRow(canvas, x, y, allOn, _hoveredLab, _localization.Get("building_laboratory_name"));
                y += rowHeight;
            }

            if (hasSmelters)
            {
                bool? allOn = AreAllActiveNullable<Smelter>(civ);
                _smelterToggleRect = DrawToggleRow(canvas, x, y, allOn, _hoveredSmelter, _localization.Get("building_smelter_name"));
            }
        }

        // Tooltips — set each frame so they persist while hovering
        if (_hoveredWonder && !_wonderEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_wonder_surface_only"), new SKPoint(_wonderButtonRect.Right, _wonderButtonRect.Top));
        else if (_hoveredBarracks)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_toggle_barracks"), new SKPoint(_barracksToggleRect.Right, _barracksToggleRect.Top));
        else if (_hoveredSteelWeapons)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_toggle_steel_weapons"), new SKPoint(_steelWeaponsToggleRect.Right, _steelWeaponsToggleRect.Top));
        else if (_hoveredLab)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_toggle_lab"), new SKPoint(_labToggleRect.Right, _labToggleRect.Top));
        else if (_hoveredSmelter)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_toggle_smelter"), new SKPoint(_smelterToggleRect.Right, _smelterToggleRect.Top));
    }

    private SKRect DrawToggleRow(SKCanvas canvas, float x, float y, bool? isOn, bool isHovered, string label, bool isDimmed = false)
    {
        float s       = LastUiScale;
        float toggleW = ToggleWidth * s;
        float toggleH = ToggleHeight * s;
        float rowH    = RowHeight * s;
        float toggleY = y + (rowH - toggleH) / 2f;
        float radius  = toggleH / 2f;
        var trackRect = new SKRect(x, toggleY, x + toggleW, toggleY + toggleH);

        SKPaint? fill;
        if (isDimmed)
            fill = _dimTogglePaint;
        else if (isOn == null)
            fill = isHovered ? _indeterminateHoverPaint : _indeterminatePaint;
        else if (isOn.Value)
            fill = isHovered ? _onHoverPaint : _onPaint;
        else
            fill = isHovered ? _offHoverPaint : _offPaint;

        canvas.DrawRoundRect(trackRect, radius, radius, fill);
        canvas.DrawRoundRect(trackRect, radius, radius, _toggleBorderPaint);

        float knobR  = radius - 3f * s;
        float knobCy = toggleY + radius;
        float knobCx = isOn == null
            ? x + toggleW / 2f
            : (isOn.Value
                ? x + toggleW - radius - 1f * s
                : x + radius + 1f * s);
        canvas.DrawCircle(knobCx, knobCy, knobR, _toggleKnobPaint);

        SkiaTextUtils.DrawText(canvas, label, x + toggleW + 10f * s, y + rowH / 2f + 5f * s, _labelFont, isDimmed ? _rowLabelDimPaint : _rowLabelPaint);

        return trackRect;
    }

    public void HandlePointerMoved(SKPoint pos)
    {
        if (_disposed) return;
        _hoveredTrade    = !_tradeButtonRect.IsEmpty    && _tradeButtonRect.Contains(pos.X, pos.Y);
        _hoveredPrestige = !_prestigeButtonRect.IsEmpty && _prestigeButtonRect.Contains(pos.X, pos.Y);
        _hoveredWonder   = !_wonderButtonRect.IsEmpty   && _wonderButtonRect.Contains(pos.X, pos.Y);
        _hoveredBarracks     = !_barracksToggleRect.IsEmpty     && _barracksToggleRect.Contains(pos.X, pos.Y);
        _hoveredLab          = !_labToggleRect.IsEmpty          && _labToggleRect.Contains(pos.X, pos.Y);
        _hoveredSmelter      = !_smelterToggleRect.IsEmpty      && _smelterToggleRect.Contains(pos.X, pos.Y);
        _hoveredSteelWeapons = !_steelWeaponsToggleRect.IsEmpty && _steelWeaponsToggleRect.Contains(pos.X, pos.Y);
    }

    public bool HandlePointerPressed(SKPoint pos)
    {
        if (_disposed) return false;

        if (!CollapseTabRect.IsEmpty && CollapseTabRect.Contains(pos.X, pos.Y))
        {
            bool wasCollapsed = Collapsed;
            Collapsed = !Collapsed;
            if (wasCollapsed && !Collapsed)
                OnExpanded?.Invoke();
            return true;
        }

        if (!PanelBounds.Contains(pos.X, pos.Y)) return false;

        if (!_tradeButtonRect.IsEmpty && _tradeButtonRect.Contains(pos.X, pos.Y))
        {
            _closeAll();
            _tradeRenderer.Open();
            return true;
        }

        if (!_prestigeButtonRect.IsEmpty && _prestigeButtonRect.Contains(pos.X, pos.Y) && IsPrestigeAvailable())
        {
            _closeAll();
            _prestigeRenderer.Open();
            return true;
        }

        if (!_wonderButtonRect.IsEmpty && _wonderButtonRect.Contains(pos.X, pos.Y) && _wonderEnabled && _wonderSelectionService != null)
        {
            _closeAll();
            _wonderSelectionService.Enter();
            return true;
        }

        var civ = _gameControllerService.PlayerCivilization;
        if (civ != null)
        {
            if (!_barracksToggleRect.IsEmpty && _barracksToggleRect.Contains(pos.X, pos.Y))
            {
                ToggleAll<Barracks>(civ);
                return true;
            }

            if (!_labToggleRect.IsEmpty && _labToggleRect.Contains(pos.X, pos.Y))
            {
                ToggleAll<Laboratory>(civ);
                return true;
            }

            if (!_smelterToggleRect.IsEmpty && _smelterToggleRect.Contains(pos.X, pos.Y))
            {
                ToggleAll<Smelter>(civ);
                return true;
            }

            if (!_steelWeaponsToggleRect.IsEmpty && _steelWeaponsToggleRect.Contains(pos.X, pos.Y))
            {
                ToggleAllSteelWeapons(civ);
                return true;
            }
        }

        return true;
    }

    private bool IsTradeVisible()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.TradeController.IsTradeAvailable(civ.Index); }
        catch { return false; }
    }

    private bool IsPrestigeVisible()
    {
        try { return _gameControllerService.MainGameController.PrestigeController.PrestigeIsVisible(); }
        catch { return false; }
    }

    private bool IsPrestigeAvailable()
    {
        try { return _gameControllerService.MainGameController.PrestigeController.PrestigeIsAvailable(); }
        catch { return false; }
    }

    private int GetPrestigePoints()
    {
        try { return _gameControllerService.MainGameController.PrestigeController.CalculatePrestigePoints(); }
        catch { return 0; }
    }

    private bool IsWonderVisible()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.WonderController.HasWondersUnlocked(civ); }
        catch { return false; }
    }

    private bool CanPlaceWonder()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.WonderController.CanPlaceWonder(civ); }
        catch { return false; }
    }

    private static bool HasBuilt<T>(Civilization civ) where T : Building
        => civ.Cities.Any(c => c.Buildings.OfType<T>().Any(b => b.Level >= 1));

    private static bool? AreAllActiveNullable<T>(Civilization civ) where T : Building
    {
        var list = civ.Cities.SelectMany(c => c.Buildings.OfType<T>()).Where(b => b.Level >= 1).ToList();
        if (list.Count == 0) return false;
        bool allOn = list.All(b => b.ActivationStatus == ActivationStatus.ACTIVE);
        if (allOn) return true;
        bool anyOn = list.Any(b => b.ActivationStatus == ActivationStatus.ACTIVE);
        return anyOn ? null : false;
    }

    private static void ToggleAll<T>(Civilization civ) where T : Building
    {
        var list = civ.Cities.SelectMany(c => c.Buildings.OfType<T>()).Where(b => b.Level >= 1).ToList();
        bool allActive = list.All(b => b.ActivationStatus == ActivationStatus.ACTIVE);
        var next = allActive ? ActivationStatus.INACTIVE : ActivationStatus.ACTIVE;
        foreach (var b in list) b.ActivationStatus = next;
    }

    private static bool? AreAllSteelWeaponsActiveNullable(Civilization civ)
    {
        var list = civ.Cities.SelectMany(c => c.Buildings.OfType<Barracks>()).Where(b => b.Level >= 1).ToList();
        if (list.Count == 0) return false;
        bool allOn = list.All(b => b.UsesSteelWeapons);
        if (allOn) return true;
        bool anyOn = list.Any(b => b.UsesSteelWeapons);
        return anyOn ? null : false;
    }

    private static void ToggleAllSteelWeapons(Civilization civ)
    {
        var list = civ.Cities.SelectMany(c => c.Buildings.OfType<Barracks>()).Where(b => b.Level >= 1).ToList();
        bool allOn = list.All(b => b.UsesSteelWeapons);
        foreach (var b in list) b.UsesSteelWeapons = !allOn;
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _sectionTitlePaint?.Dispose();
        _separatorPaint?.Dispose();
        _btnPaint?.Dispose();
        _btnHoverPaint?.Dispose();
        _btnDisabledPaint?.Dispose();
        _btnDisabledTxtPaint?.Dispose();
        _onPaint?.Dispose();
        _onHoverPaint?.Dispose();
        _offPaint?.Dispose();
        _offHoverPaint?.Dispose();
        _toggleBorderPaint?.Dispose();
        _toggleKnobPaint?.Dispose();
        _rowLabelPaint?.Dispose();
        _rowLabelDimPaint?.Dispose();
        _dimTogglePaint?.Dispose();
        _indeterminatePaint?.Dispose();
        _indeterminateHoverPaint?.Dispose();
        _sectionFont?.Dispose();
        _btnFont?.Dispose();
        _btnSmFont?.Dispose();
        _labelFont?.Dispose();
        _disposed = true;
        base.Dispose();
    }
}
