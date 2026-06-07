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

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

public sealed class PlayerCivilizationPanelRenderer : IDisposable
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
    private readonly TradeRenderer _tradeRenderer;
    private readonly PrestigeRenderer _prestigeRenderer;
    private WonderSelectionService? _wonderSelectionService;
    private readonly TooltipRenderer _tooltipRenderer;

    private const float CollapseTabW = 14f;
    private const float CollapseTabH = 24f;
    private bool _collapsed = false;
    private SKRect _collapseTabRect = SKRect.Empty;
    public float TopOverride { get; set; } = 0f;

    private SKSize _canvasSize;
    private SKRect _panelBounds        = SKRect.Empty;
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

    private readonly SKPaint _panelBgPaint        = new() { Color = new SKColor(24, 24, 30, 230), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _panelBorderPaint    = new() { Color = SKColors.Gold, StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _sectionTitlePaint   = new() { Color = new SKColor(160, 160, 175), IsAntialias = true };
    private readonly SKPaint _separatorPaint      = new() { Color = new SKColor(60, 60, 80), StrokeWidth = 0.8f, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _btnPaint            = new() { Color = new SKColor(46, 125, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _btnHoverPaint       = new() { Color = new SKColor(60, 150, 64), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _btnDisabledPaint    = new() { Color = new SKColor(70, 70, 78), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _btnTextPaint        = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _btnDisabledTxtPaint = new() { Color = new SKColor(160, 160, 165), IsAntialias = true };
    private readonly SKPaint _onPaint             = new() { Color = new SKColor(46, 125, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _onHoverPaint        = new() { Color = new SKColor(60, 150, 64), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _offPaint            = new() { Color = new SKColor(160, 50, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _offHoverPaint       = new() { Color = new SKColor(185, 65, 65), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _toggleBorderPaint   = new() { Color = new SKColor(180, 180, 200), StrokeWidth = 1.2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _toggleKnobPaint     = new() { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _rowLabelPaint       = new() { Color = new SKColor(215, 215, 225), IsAntialias = true };
    private readonly SKPaint _rowLabelDimPaint    = new() { Color = new SKColor(140, 140, 150, 160), IsAntialias = true };
    private readonly SKPaint _dimTogglePaint             = new() { Color = new SKColor(70, 70, 80),   Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _indeterminatePaint         = new() { Color = new SKColor(90, 90, 105),  Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _indeterminateHoverPaint    = new() { Color = new SKColor(110, 110, 125), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _collapseTabBgPaint  = new() { Color = new SKColor(24, 24, 30, 230), Style = SKPaintStyle.Fill, IsAntialias = true };

    private readonly SKFont _sectionFont = new() { Size = TitleSize, Typeface = SkiaFonts.Regular };
    private readonly SKFont _btnFont     = new() { Size = 13f,       Typeface = SkiaFonts.Bold };
    private readonly SKFont _btnSmFont   = new() { Size = 11f,       Typeface = SkiaFonts.Bold };
    private readonly SKFont _labelFont   = new() { Size = 13f,       Typeface = SkiaFonts.Regular };

    public PlayerCivilizationPanelRenderer(
        GameControllerService gameControllerService,
        LocalizationService localization,
        Action closeAll,
        TradeRenderer tradeRenderer,
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

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void ConnectWonderSelectionService(WonderSelectionService service)
        => _wonderSelectionService = service;

    public bool ContainsPoint(SKPoint point) =>
        (!_panelBounds.IsEmpty && _panelBounds.Contains(point.X, point.Y)) ||
        (!_collapseTabRect.IsEmpty && _collapseTabRect.Contains(point.X, point.Y));

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState) return;

        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return;

        bool tradeVisible    = IsTradeVisible();
        bool prestigeVisible = IsPrestigeVisible();
        bool prestigeAvail   = prestigeVisible && IsPrestigeAvailable();
        int  prestigePoints  = prestigeVisible ? GetPrestigePoints() : 0;
        bool wonderVisible   = IsWonderVisible() && CanPlaceWonder();
        _wonderEnabled = wonderVisible && context.CurrentLayer == 0;
        bool hasBarracks        = HasBuilt<Barracks>(civ);
        bool hasLabs            = HasBuilt<Laboratory>(civ);
        bool hasSmelters        = HasBuilt<Smelter>(civ);
        bool hasSteelWeapons    = hasBarracks && civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_STEEL_WEAPONS);

        bool showActions  = tradeVisible || prestigeVisible || wonderVisible;
        bool showControls = hasBarracks || hasLabs || hasSmelters;

        _tradeButtonRect = _prestigeButtonRect = _wonderButtonRect = SKRect.Empty;
        _barracksToggleRect = _labToggleRect = _smelterToggleRect = _steelWeaponsToggleRect = SKRect.Empty;

        if (!showActions && !showControls)
        {
            _panelBounds = SKRect.Empty;
            _collapseTabRect = SKRect.Empty;
            return;
        }

        float contentW = PanelWidth - PanelPadding * 2;
        float panelTop = (TopOverride > 0f ? TopOverride : PlayerResourcesOverlayRenderer.BarHeight) + 10f;
        float tabTop = panelTop + 8f;

        if (_collapsed)
        {
            _collapseTabRect = new SKRect(0, tabTop, CollapseTabW, tabTop + CollapseTabH);
            _panelBounds = _collapseTabRect;
            canvas.DrawRoundRect(_collapseTabRect, 4, 4, _collapseTabBgPaint);
            canvas.DrawRoundRect(_collapseTabRect, 4, 4, _panelBorderPaint);
            canvas.DrawText("►", _collapseTabRect.MidX, _collapseTabRect.MidY + 5f, SKTextAlign.Center, _btnFont, _btnTextPaint);
            return;
        }

        // Measure total height
        float h = PanelPadding;
        if (showActions)
        {
            int actionCount = (tradeVisible ? 1 : 0) + (prestigeVisible ? 1 : 0) + (wonderVisible ? 1 : 0);
            int actionRows  = (actionCount + 1) / 2;
            h += TitleHeight + actionRows * (BtnHeight + BtnSpacing);
        }
        if (showActions && showControls) h += SepSpacing * 2 + 1f;
        if (showControls)
        {
            h += TitleHeight;
            if (hasBarracks)      h += RowHeight;
            if (hasSteelWeapons)  h += RowHeight;
            if (hasLabs)          h += RowHeight;
            if (hasSmelters)      h += RowHeight;
        }
        h += PanelPadding;

        _panelBounds = new SKRect(PanelLeft, panelTop, PanelLeft + PanelWidth, panelTop + h);
        canvas.DrawRoundRect(_panelBounds, 8, 8, _panelBgPaint);
        canvas.DrawRoundRect(_panelBounds, 8, 8, _panelBorderPaint);

        // Onglet collapse (bord droit du panneau)
        _collapseTabRect = new SKRect(PanelLeft + PanelWidth, tabTop, PanelLeft + PanelWidth + CollapseTabW, tabTop + CollapseTabH);
        canvas.DrawRoundRect(_collapseTabRect, 4, 4, _collapseTabBgPaint);
        canvas.DrawRoundRect(_collapseTabRect, 4, 4, _panelBorderPaint);
        canvas.DrawText("◄", _collapseTabRect.MidX, _collapseTabRect.MidY + 5f, SKTextAlign.Center, _btnFont, _btnTextPaint);

        float x = PanelLeft + PanelPadding;
        float y = panelTop + PanelPadding;

        if (showActions)
        {
            canvas.DrawText(_localization.Get("panel_civ_actions"), x, y + TitleSize, _sectionFont, _sectionTitlePaint);
            y += TitleHeight;

            const float colGap   = 6f;
            float       colW     = (contentW - colGap) / 2f;
            int  actionCount = (tradeVisible ? 1 : 0) + (prestigeVisible ? 1 : 0) + (wonderVisible ? 1 : 0);
            float actionsY   = y;
            int   btnIdx     = 0;

            SKRect BtnRect(int idx)
            {
                float col       = idx % 2;
                float row       = idx / 2;
                bool  lastOdd   = idx == actionCount - 1 && actionCount % 2 == 1;
                float bw        = lastOdd ? contentW : colW;
                float bx        = x + col * (colW + colGap);
                float by        = actionsY + row * (BtnHeight + BtnSpacing);
                return new SKRect(bx, by, bx + bw, by + BtnHeight);
            }

            if (tradeVisible)
            {
                _tradeButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_tradeButtonRect, 6, 6, _hoveredTrade ? _btnHoverPaint : _btnPaint);
                canvas.DrawText(_localization.Get("trade_action"), _tradeButtonRect.MidX, _tradeButtonRect.MidY + 4f, SKTextAlign.Center, _btnSmFont, _btnTextPaint);
            }

            if (prestigeVisible)
            {
                _prestigeButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_prestigeButtonRect, 6, 6, prestigeAvail ? (_hoveredPrestige ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                string prestigeLabel = $"{_localization.Get("prestige_action")} ({prestigePoints})";
                canvas.DrawText(prestigeLabel, _prestigeButtonRect.MidX, _prestigeButtonRect.MidY + 4f, SKTextAlign.Center, _btnSmFont, prestigeAvail ? _btnTextPaint : _btnDisabledTxtPaint);
            }

            if (wonderVisible)
            {
                _wonderButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_wonderButtonRect, 6, 6, _wonderEnabled ? (_hoveredWonder ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                canvas.DrawText(_localization.Get("wonder_action_short"), _wonderButtonRect.MidX, _wonderButtonRect.MidY + 4f, SKTextAlign.Center, _btnSmFont, _wonderEnabled ? _btnTextPaint : _btnDisabledTxtPaint);
            }

            y = actionsY + ((btnIdx + 1) / 2) * (BtnHeight + BtnSpacing);
        }

        if (showActions && showControls)
        {
            y += SepSpacing;
            canvas.DrawLine(x, y, x + contentW, y, _separatorPaint);
            y += SepSpacing + 1f;
        }

        if (showControls)
        {
            canvas.DrawText(_localization.Get("panel_civ_controls"), x, y + TitleSize, _sectionFont, _sectionTitlePaint);
            y += TitleHeight;

            if (hasBarracks)
            {
                bool? allOn = AreAllActiveNullable<Barracks>(civ);
                _barracksToggleRect = DrawToggleRow(canvas, x, y, allOn, _hoveredBarracks, _localization.Get("building_barracks_name"));
                y += RowHeight;
            }

            if (hasSteelWeapons)
            {
                bool? allOn = AreAllSteelWeaponsActiveNullable(civ);
                bool noBarracksActive = !civ.Cities.SelectMany(c => c.Buildings.OfType<Barracks>()).Any(b => b.Level >= 1 && b.ActivationStatus == ActivationStatus.ACTIVE);
                _steelWeaponsToggleRect = DrawToggleRow(canvas, x, y, allOn, _hoveredSteelWeapons, _localization.Get("toggle_steel_weapons"), isDimmed: noBarracksActive);
                y += RowHeight;
            }

            if (hasLabs)
            {
                bool? allOn = AreAllActiveNullable<Laboratory>(civ);
                _labToggleRect = DrawToggleRow(canvas, x, y, allOn, _hoveredLab, _localization.Get("building_laboratory_name"));
                y += RowHeight;
            }

            if (hasSmelters)
            {
                bool? allOn = AreAllActiveNullable<Smelter>(civ);
                _smelterToggleRect = DrawToggleRow(canvas, x, y, allOn, _hoveredSmelter, _localization.Get("building_smelter_name"));
            }
        }

        // Tooltips — appelés chaque frame pour persister tant que la souris ne bouge pas
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
        float toggleY  = y + (RowHeight - ToggleHeight) / 2f;
        float radius   = ToggleHeight / 2f;
        var   trackRect = new SKRect(x, toggleY, x + ToggleWidth, toggleY + ToggleHeight);

        SKPaint fill;
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

        float knobR  = radius - 3f;
        float knobCy = toggleY + radius;
        float knobCx = isOn == null
            ? x + ToggleWidth / 2f               // centre quand mixte
            : (isOn.Value
                ? x + ToggleWidth - radius - 1f  // droite quand ON
                : x + radius + 1f);              // gauche quand OFF
        canvas.DrawCircle(knobCx, knobCy, knobR, _toggleKnobPaint);

        canvas.DrawText(label, x + ToggleWidth + 10f, y + RowHeight / 2f + 5f, _labelFont, isDimmed ? _rowLabelDimPaint : _rowLabelPaint);

        return trackRect;
    }

    public void HandlePointerMoved(SKPoint pos)
    {
        if (_disposed) return;
        _hoveredTrade    = !_tradeButtonRect.IsEmpty    && _tradeButtonRect.Contains(pos.X, pos.Y);
        _hoveredPrestige = !_prestigeButtonRect.IsEmpty && _prestigeButtonRect.Contains(pos.X, pos.Y);
        _hoveredWonder   = !_wonderButtonRect.IsEmpty   && _wonderButtonRect.Contains(pos.X, pos.Y);
        _hoveredBarracks = !_barracksToggleRect.IsEmpty && _barracksToggleRect.Contains(pos.X, pos.Y);
        _hoveredLab      = !_labToggleRect.IsEmpty      && _labToggleRect.Contains(pos.X, pos.Y);
        _hoveredSmelter      = !_smelterToggleRect.IsEmpty      && _smelterToggleRect.Contains(pos.X, pos.Y);
        _hoveredSteelWeapons = !_steelWeaponsToggleRect.IsEmpty && _steelWeaponsToggleRect.Contains(pos.X, pos.Y);

    }

    public bool HandlePointerPressed(SKPoint pos)
    {
        if (_disposed) return false;

        if (!_collapseTabRect.IsEmpty && _collapseTabRect.Contains(pos.X, pos.Y))
        {
            _collapsed = !_collapsed;
            return true;
        }

        if (!_panelBounds.Contains(pos.X, pos.Y)) return false;

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

        return true; // absorb clicks that land on the panel background
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

    public void Dispose()
    {
        if (_disposed) return;
        _panelBgPaint.Dispose();
        _panelBorderPaint.Dispose();
        _sectionTitlePaint.Dispose();
        _separatorPaint.Dispose();
        _btnPaint.Dispose();
        _btnHoverPaint.Dispose();
        _btnDisabledPaint.Dispose();
        _btnTextPaint.Dispose();
        _btnDisabledTxtPaint.Dispose();
        _onPaint.Dispose();
        _onHoverPaint.Dispose();
        _offPaint.Dispose();
        _offHoverPaint.Dispose();
        _toggleBorderPaint.Dispose();
        _toggleKnobPaint.Dispose();
        _rowLabelPaint.Dispose();
        _rowLabelDimPaint.Dispose();
        _dimTogglePaint.Dispose();
        _indeterminatePaint.Dispose();
        _indeterminateHoverPaint.Dispose();
        _collapseTabBgPaint.Dispose();
        _sectionFont.Dispose();
        _btnFont.Dispose();
        _btnSmFont.Dispose();
        _labelFont.Dispose();
        _disposed = true;
    }
}
