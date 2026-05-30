using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Services.Localization;
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
    private const float PanelWidth   = 200f;
    private const float PanelPadding = 12f;
    private const float BtnHeight    = 34f;
    private const float BtnSpacing   = 6f;
    private const float TitleSize    = 11f;
    private const float TitleHeight  = 20f;
    private const float ToggleWidth  = 52f;
    private const float ToggleHeight = 26f;
    private const float RowHeight    = 36f;
    private const float SepSpacing   = 8f;

    private readonly GameControllerService _gameControllerService;
    private readonly ILocalizationService _localization;
    private readonly Action _closeAll;
    private readonly TradeRenderer _tradeRenderer;
    private readonly PrestigeRenderer _prestigeRenderer;
    private WonderSelectionService? _wonderSelectionService;

    private const float CollapseTabW = 14f;
    private const float CollapseTabH = 24f;
    private bool _collapsed = false;
    private SKRect _collapseTabRect = SKRect.Empty;

    private SKSize _canvasSize;
    private SKRect _panelBounds        = SKRect.Empty;
    private SKRect _tradeButtonRect    = SKRect.Empty;
    private SKRect _prestigeButtonRect = SKRect.Empty;
    private SKRect _wonderButtonRect   = SKRect.Empty;
    private SKRect _barracksToggleRect = SKRect.Empty;
    private SKRect _labToggleRect      = SKRect.Empty;

    private bool _hoveredTrade, _hoveredPrestige, _hoveredWonder;
    private bool _hoveredBarracks, _hoveredLab;
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
    private readonly SKPaint _offPaint            = new() { Color = new SKColor(70, 70, 78), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _offHoverPaint       = new() { Color = new SKColor(90, 90, 100), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _toggleBorderPaint   = new() { Color = new SKColor(120, 120, 140), StrokeWidth = 1.2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _toggleTextPaint     = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _rowLabelPaint       = new() { Color = new SKColor(215, 215, 225), IsAntialias = true };
    private readonly SKPaint _collapseTabBgPaint  = new() { Color = new SKColor(24, 24, 30, 230), Style = SKPaintStyle.Fill, IsAntialias = true };

    private readonly SKFont _sectionFont = new() { Size = TitleSize, Typeface = SkiaFonts.Regular };
    private readonly SKFont _btnFont     = new() { Size = 13f,       Typeface = SkiaFonts.Bold };
    private readonly SKFont _toggleFont  = new() { Size = 11f,       Typeface = SkiaFonts.Bold };
    private readonly SKFont _labelFont   = new() { Size = 13f,       Typeface = SkiaFonts.Regular };

    public PlayerCivilizationPanelRenderer(
        GameControllerService gameControllerService,
        ILocalizationService localization,
        Action closeAll,
        TradeRenderer tradeRenderer,
        PrestigeRenderer prestigeRenderer,
        WonderSelectionService? wonderSelectionService)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _closeAll = closeAll;
        _tradeRenderer = tradeRenderer;
        _prestigeRenderer = prestigeRenderer;
        _wonderSelectionService = wonderSelectionService;
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
        bool wonderVisible   = IsWonderVisible();
        bool canWonder       = wonderVisible && CanPlaceWonder();
        bool hasBarracks     = HasBuilt<Barracks>(civ);
        bool hasLabs         = HasBuilt<Laboratory>(civ);

        bool showActions  = tradeVisible || prestigeVisible || wonderVisible;
        bool showControls = hasBarracks || hasLabs;

        _tradeButtonRect = _prestigeButtonRect = _wonderButtonRect = SKRect.Empty;
        _barracksToggleRect = _labToggleRect = SKRect.Empty;

        if (!showActions && !showControls)
        {
            _panelBounds = SKRect.Empty;
            _collapseTabRect = SKRect.Empty;
            return;
        }

        float contentW = PanelWidth - PanelPadding * 2;
        float panelTop = PlayerResourcesOverlayRenderer.BarHeight + 10f;
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
            h += TitleHeight;
            if (tradeVisible)    h += BtnHeight + BtnSpacing;
            if (prestigeVisible) h += BtnHeight + BtnSpacing;
            if (wonderVisible)   h += BtnHeight + BtnSpacing;
        }
        if (showActions && showControls) h += SepSpacing * 2 + 1f;
        if (showControls)
        {
            h += TitleHeight;
            if (hasBarracks) h += RowHeight;
            if (hasLabs)     h += RowHeight;
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

            if (tradeVisible)
            {
                _tradeButtonRect = new SKRect(x, y, x + contentW, y + BtnHeight);
                canvas.DrawRoundRect(_tradeButtonRect, 6, 6, _hoveredTrade ? _btnHoverPaint : _btnPaint);
                canvas.DrawText(_localization.Get("trade_action"), _tradeButtonRect.MidX, _tradeButtonRect.MidY + 5f, SKTextAlign.Center, _btnFont, _btnTextPaint);
                y += BtnHeight + BtnSpacing;
            }

            if (prestigeVisible)
            {
                _prestigeButtonRect = new SKRect(x, y, x + contentW, y + BtnHeight);
                var ctrl = _gameControllerService.MainGameController.PrestigeController;
                int pts  = ctrl.CalculatePrestigePoints();
                string label = prestigeAvail
                    ? $"{_localization.Get("prestige_action")} ({pts})"
                    : $"{_localization.Get("prestige_action")} ({pts}/{PrestigeController.PrestigeRequiredPoints})";
                canvas.DrawRoundRect(_prestigeButtonRect, 6, 6, prestigeAvail ? (_hoveredPrestige ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                canvas.DrawText(label, _prestigeButtonRect.MidX, _prestigeButtonRect.MidY + 5f, SKTextAlign.Center, _btnFont, prestigeAvail ? _btnTextPaint : _btnDisabledTxtPaint);
                y += BtnHeight + BtnSpacing;
            }

            if (wonderVisible)
            {
                _wonderButtonRect = new SKRect(x, y, x + contentW, y + BtnHeight);
                canvas.DrawRoundRect(_wonderButtonRect, 6, 6, canWonder ? (_hoveredWonder ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                canvas.DrawText(_localization.Get("wonder_action"), _wonderButtonRect.MidX, _wonderButtonRect.MidY + 5f, SKTextAlign.Center, _btnFont, canWonder ? _btnTextPaint : _btnDisabledTxtPaint);
                y += BtnHeight + BtnSpacing;
            }
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
                bool allOn = AreAllActive<Barracks>(civ);
                _barracksToggleRect = DrawToggleRow(canvas, x, y, allOn, _hoveredBarracks, _localization.Get("building_barracks_name"));
                y += RowHeight;
            }

            if (hasLabs)
            {
                bool allOn = AreAllActive<Laboratory>(civ);
                _labToggleRect = DrawToggleRow(canvas, x, y, allOn, _hoveredLab, _localization.Get("building_laboratory_name"));
            }
        }
    }

    private SKRect DrawToggleRow(SKCanvas canvas, float x, float y, bool isOn, bool isHovered, string label)
    {
        float toggleY = y + (RowHeight - ToggleHeight) / 2f;
        var toggleRect = new SKRect(x, toggleY, x + ToggleWidth, toggleY + ToggleHeight);

        var fill = isOn ? (isHovered ? _onHoverPaint : _onPaint) : (isHovered ? _offHoverPaint : _offPaint);
        canvas.DrawRoundRect(toggleRect, 5, 5, fill);
        canvas.DrawRoundRect(toggleRect, 5, 5, _toggleBorderPaint);
        canvas.DrawText(isOn ? _localization.Get("automation_on") : _localization.Get("automation_off"),
            toggleRect.MidX, toggleRect.MidY + 4f, SKTextAlign.Center, _toggleFont, _toggleTextPaint);

        canvas.DrawText(label, x + ToggleWidth + 10f, y + RowHeight / 2f + 5f, _labelFont, _rowLabelPaint);

        return toggleRect;
    }

    public void HandlePointerMoved(SKPoint pos)
    {
        if (_disposed) return;
        _hoveredTrade    = !_tradeButtonRect.IsEmpty    && _tradeButtonRect.Contains(pos.X, pos.Y);
        _hoveredPrestige = !_prestigeButtonRect.IsEmpty && _prestigeButtonRect.Contains(pos.X, pos.Y);
        _hoveredWonder   = !_wonderButtonRect.IsEmpty   && _wonderButtonRect.Contains(pos.X, pos.Y);
        _hoveredBarracks = !_barracksToggleRect.IsEmpty && _barracksToggleRect.Contains(pos.X, pos.Y);
        _hoveredLab      = !_labToggleRect.IsEmpty      && _labToggleRect.Contains(pos.X, pos.Y);
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

        if (!_wonderButtonRect.IsEmpty && _wonderButtonRect.Contains(pos.X, pos.Y) && CanPlaceWonder() && _wonderSelectionService != null)
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

    private static bool AreAllActive<T>(Civilization civ) where T : Building
    {
        var list = civ.Cities.SelectMany(c => c.Buildings.OfType<T>()).Where(b => b.Level >= 1).ToList();
        return list.Count > 0 && list.All(b => b.ActivationStatus == ActivationStatus.ACTIVE);
    }

    private static void ToggleAll<T>(Civilization civ) where T : Building
    {
        var list = civ.Cities.SelectMany(c => c.Buildings.OfType<T>()).Where(b => b.Level >= 1).ToList();
        bool allActive = list.All(b => b.ActivationStatus == ActivationStatus.ACTIVE);
        var next = allActive ? ActivationStatus.INACTIVE : ActivationStatus.ACTIVE;
        foreach (var b in list) b.ActivationStatus = next;
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
        _toggleTextPaint.Dispose();
        _rowLabelPaint.Dispose();
        _collapseTabBgPaint.Dispose();
        _sectionFont.Dispose();
        _btnFont.Dispose();
        _toggleFont.Dispose();
        _labelFont.Dispose();
        _disposed = true;
    }
}
