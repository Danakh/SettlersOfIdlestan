using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers;

public sealed class OverlayRenderer : IGameRenderer
{
    private const float TradeButtonWidth = 120;
    private const float PrestigeButtonWidth = 160;
    private const float TradeButtonHeight = 38;
    private const float TradeButtonMargin = 14;
    private const float ButtonSpacing = 10;
    private const float CityPanelReservedBottomHeight = TradeButtonHeight + TradeButtonMargin * 2;

    private const float TabWidth = 62;
    private const float TabHeight = 28;
    private const float TabMarginLeft = 8;
    private const float TabSpacing = 5;
    private const float TabsContentWidth2 = TabMarginLeft + TabWidth * 2 + TabSpacing + TabMarginLeft;
    private const float TabsContentWidth3 = TabMarginLeft + TabWidth * 3 + TabSpacing * 2 + TabMarginLeft;

    private readonly InputHandlingService _inputService;
    private readonly GameControllerService _gameControllerService;
    private readonly ILocalizationService _localization;
    private readonly PlayerResourcesOverlayRenderer _playerResourcesOverlayRenderer;
    private readonly SettingsMenu _settingsMenu;
    private readonly SelectedCityPanelRenderer _selectedCityPanelRenderer;
    private readonly TradeRenderer _tradeRenderer;
    private readonly PrestigeRenderer _prestigeRenderer;
    private readonly PrestigeMapRenderer _prestigeMapRenderer;
    private readonly PrestigeHistoryRenderer _prestigeHistoryRenderer;
    private readonly TimeControlRenderer _timeControlRenderer;

    private readonly SKPaint _buttonPaint = new() { Color = new SKColor(46, 125, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _disabledButtonPaint = new() { Color = new SKColor(90, 90, 96), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _buttonTextPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _disabledTextPaint = new() { Color = new SKColor(180, 180, 185), IsAntialias = true };
    private readonly SKFont _buttonFont = new() { Size = 14, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };

    private readonly SKPaint _activeTabPaint = new() { Color = new SKColor(60, 100, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _inactiveTabPaint = new() { Color = new SKColor(35, 35, 45), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _activeTabBorderPaint = new() { Color = SKColors.Gold, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKFont _tabFont = new() { Size = 12, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };

    private SKSize _canvasSize;
    private SKRect _tradeButtonRect = SKRect.Empty;
    private SKRect _prestigeButtonRect = SKRect.Empty;
    private SKRect _tab1Rect = SKRect.Empty;
    private SKRect _tab2Rect = SKRect.Empty;
    private SKRect _tab3Rect = SKRect.Empty;
    private int _activeTab = 0;
    private bool _disposed;
    private bool _isVisible = true;

    public OverlayRenderer(
        InputHandlingService inputService,
        GameControllerService gameControllerService,
        ILocalizationService localization,
        PlayerResourcesOverlayRenderer playerResourcesOverlayRenderer,
        SettingsMenu settingsMenu,
        SelectedCityPanelRenderer selectedCityPanelRenderer,
        TradeRenderer tradeRenderer,
        PrestigeRenderer prestigeRenderer,
        PrestigeMapRenderer prestigeMapRenderer,
        PrestigeHistoryRenderer prestigeHistoryRenderer,
        TimeControlRenderer timeControlRenderer)
    {
        _inputService = inputService;
        _gameControllerService = gameControllerService;
        _localization = localization;
        _playerResourcesOverlayRenderer = playerResourcesOverlayRenderer;
        _settingsMenu = settingsMenu;
        _selectedCityPanelRenderer = selectedCityPanelRenderer;
        _tradeRenderer = tradeRenderer;
        _prestigeRenderer = prestigeRenderer;
        _prestigeMapRenderer = prestigeMapRenderer;
        _prestigeHistoryRenderer = prestigeHistoryRenderer;
        _timeControlRenderer = timeControlRenderer;
        _inputService.PointerPressed += HandlePointerPressed;
        _inputService.PointerMoved += HandlePointerMoved;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        _playerResourcesOverlayRenderer.Initialize(canvasSize);
        _selectedCityPanelRenderer.Initialize(canvasSize);
        _selectedCityPanelRenderer.ReservedBottomHeight = CityPanelReservedBottomHeight;
        _tradeRenderer.Initialize(canvasSize);
        _prestigeRenderer.Initialize(canvasSize);
        _prestigeMapRenderer.Initialize(canvasSize);
        _prestigeHistoryRenderer.Initialize(canvasSize);

        // Positionné juste à gauche de l'engrenage (gear)
        float gearX = canvasSize.Width - PlayerResourcesOverlayRenderer.Padding - PlayerResourcesOverlayRenderer.IconSize;
        float timeControlRight = gearX - 8f;
        _timeControlRenderer.Initialize(canvasSize, timeControlRight);
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed)
            return;

        if (!_isVisible)
            return;

        bool showTabs = HasPrestigePoints(context);
        _tab1Rect = SKRect.Empty;
        _tab2Rect = SKRect.Empty;
        _tab3Rect = SKRect.Empty;

        if (showTabs)
        {
            float tabY = (PlayerResourcesOverlayRenderer.BarHeight - TabHeight) / 2;
            _tab1Rect = new SKRect(TabMarginLeft, tabY, TabMarginLeft + TabWidth, tabY + TabHeight);
            _tab2Rect = new SKRect(_tab1Rect.Right + TabSpacing, tabY, _tab1Rect.Right + TabSpacing + TabWidth, tabY + TabHeight);
            _tab3Rect = new SKRect(_tab2Rect.Right + TabSpacing, tabY, _tab2Rect.Right + TabSpacing + TabWidth, tabY + TabHeight);
            _playerResourcesOverlayRenderer.ResourceStartX = TabsContentWidth3;
        }
        else
        {
            _activeTab = 0;
            _playerResourcesOverlayRenderer.ResourceStartX = PlayerResourcesOverlayRenderer.Padding;
        }

        _playerResourcesOverlayRenderer.Mode = _activeTab == 1 ? BarDisplayMode.Prestige : BarDisplayMode.Island;

        bool onPrestigeTab = _activeTab == 1;
        bool onHistoryTab = _activeTab == 2;
        _selectedCityPanelRenderer.IsInputEnabled = !onPrestigeTab && !onHistoryTab && !_tradeRenderer.IsOpen && !_prestigeRenderer.IsOpen;
        _playerResourcesOverlayRenderer.Render(canvas, context);

        if (showTabs)
            DrawTabButtons(canvas);

        if (onPrestigeTab)
        {
            _prestigeMapRenderer.RenderPrestigeMap(canvas, context);
        }
        else if (onHistoryTab)
        {
            _prestigeHistoryRenderer.RenderHistory(canvas, context);
        }
        else
        {
            _selectedCityPanelRenderer.Render(canvas, context);
            DrawActionButtons(canvas, context);
        }

        float gearX = _canvasSize.Width - PlayerResourcesOverlayRenderer.Padding - PlayerResourcesOverlayRenderer.IconSize;
        _timeControlRenderer.Render(canvas, context);
        _settingsMenu.Draw(canvas, gearX, PlayerResourcesOverlayRenderer.BarHeight);

        _tradeRenderer.Render(canvas);
        _prestigeRenderer.Render(canvas);
    }

    private bool HasPrestigePoints(GameRenderContext context)
    {
        if (context.GameState is not SettlersOfIdlestan.Model.Game.MainGameState mainGameState)
            return false;
        return (mainGameState.PrestigeState?.PrestigePoints ?? 0) > 0;
    }

    private void DrawTabButtons(SKCanvas canvas)
    {
        DrawTab(canvas, _tab1Rect, _localization.Get("tab_island"), _activeTab == 0);
        DrawTab(canvas, _tab2Rect, _localization.Get("tab_prestige_map"), _activeTab == 1);
        DrawTab(canvas, _tab3Rect, _localization.Get("tab_stats"), _activeTab == 2);
    }

    private void DrawTab(SKCanvas canvas, SKRect rect, string label, bool isActive)
    {
        canvas.DrawRoundRect(rect, 5, 5, isActive ? _activeTabPaint : _inactiveTabPaint);
        if (isActive)
            canvas.DrawRoundRect(rect, 5, 5, _activeTabBorderPaint);
        var textPaint = isActive ? _buttonTextPaint : _disabledTextPaint;
        canvas.DrawText(label, rect.MidX, rect.MidY + 5, SKTextAlign.Center, _tabFont, textPaint);
    }

    private void DrawActionButtons(SKCanvas canvas, GameRenderContext context)
    {
        _tradeButtonRect = SKRect.Empty;
        _prestigeButtonRect = SKRect.Empty;

        bool isTradeVisible = IsTradeAvailable();
        var prestigeController = _gameControllerService.MainGameController.PrestigeController;
        bool isPrestigeVisible = prestigeController.PrestigeIsVisible();

        float right = _canvasSize.Width - TradeButtonMargin;

        if (isTradeVisible)
        {
            _tradeButtonRect = new SKRect(
                right - TradeButtonWidth,
                _canvasSize.Height - TradeButtonMargin - TradeButtonHeight,
                right,
                _canvasSize.Height - TradeButtonMargin);
            canvas.DrawRoundRect(_tradeButtonRect, 7, 7, _buttonPaint);
            canvas.DrawText(_localization.Get("trade_action"), _tradeButtonRect.MidX, _tradeButtonRect.MidY + 6, SKTextAlign.Center, _buttonFont, _buttonTextPaint);
            right = _tradeButtonRect.Left - ButtonSpacing;
        }

        if (isPrestigeVisible)
        {
            _prestigeButtonRect = new SKRect(
                right - PrestigeButtonWidth,
                _canvasSize.Height - TradeButtonMargin - TradeButtonHeight,
                right,
                _canvasSize.Height - TradeButtonMargin);

            bool isAvailable = prestigeController.PrestigeIsAvailable();
            int currentPoints = prestigeController.CalculatePrestigePoints();
            string label = isAvailable
                ? $"{_localization.Get("prestige_action")} ({currentPoints})"
                : $"{_localization.Get("prestige_action")} ({currentPoints}/{SettlersOfIdlestan.Controller.PrestigeController.PrestigeRequiredPoints})";
            canvas.DrawRoundRect(_prestigeButtonRect, 7, 7, isAvailable ? _buttonPaint : _disabledButtonPaint);
            canvas.DrawText(label, _prestigeButtonRect.MidX, _prestigeButtonRect.MidY + 6, SKTextAlign.Center, _buttonFont, isAvailable ? _buttonTextPaint : _disabledTextPaint);
        }
    }

    private bool IsTradeAvailable(GameRenderContext? context = null)
    {
        if (_gameControllerService.PlayerCivilization == null)
            return false;

        try
        {
            return _gameControllerService.MainGameController.TradeController.IsTradeAvailable(_gameControllerService.PlayerCivilization.Index);
        }
        catch
        {
            return false;
        }
    }

    public bool IsAnyOverlayOpen => _tradeRenderer.IsOpen || _prestigeRenderer.IsOpen || _settingsMenu.IsOpen;

    private void HandlePointerMoved(object? sender, SettlersOfIdlestanSkia.Services.PointerEventArgs e)
    {
        if (!_isVisible) return;
        if (_tradeRenderer.IsOpen)
            _tradeRenderer.HandlePointerMoved(e.Position);
        if (_activeTab == 1)
            _prestigeMapRenderer.HandlePointerMoved(e.Position);
    }

    private void HandlePointerPressed(object? sender, SettlersOfIdlestanSkia.Services.PointerEventArgs e)
    {
        if (!_isVisible)
            return;

        if (_prestigeRenderer.HandlePointerPressed(e.Position, e.Button))
            return;

        if (_tradeRenderer.HandlePointerPressed(e.Position, e.Button))
            return;

        if (e.Button != PointerButton.Left)
            return;

        if (!_tab1Rect.IsEmpty && _tab1Rect.Contains(e.Position.X, e.Position.Y))
        {
            _activeTab = 0;
            return;
        }

        if (!_tab2Rect.IsEmpty && _tab2Rect.Contains(e.Position.X, e.Position.Y))
        {
            _activeTab = 1;
            return;
        }

        if (!_tab3Rect.IsEmpty && _tab3Rect.Contains(e.Position.X, e.Position.Y))
        {
            _activeTab = 2;
            return;
        }

        if (_activeTab == 1)
        {
            _prestigeMapRenderer.HandlePointerPressed(e.Position);
            return;
        }

        if (_activeTab == 2)
            return;

        if (_playerResourcesOverlayRenderer.GearRect.Contains(e.Position.X, e.Position.Y))
        {
            _settingsMenu.HandleGearClick();
            return;
        }

        if (_tradeButtonRect.Contains(e.Position.X, e.Position.Y) && IsTradeAvailable())
        {
            _settingsMenu.Close();
            _prestigeRenderer.Close();
            _tradeRenderer.Open();
        }

        if (!_prestigeButtonRect.IsEmpty && _prestigeButtonRect.Contains(e.Position.X, e.Position.Y) && _gameControllerService.MainGameController.PrestigeController.PrestigeIsAvailable())
        {
            _settingsMenu.Close();
            _tradeRenderer.Close();
            _prestigeRenderer.Open();
        }
    }

    public void CloseAll()
    {
        _settingsMenu.Close();
        _tradeRenderer.Close();
        _prestigeRenderer.Close();
        _selectedCityPanelRenderer.Close();
        _selectedCityPanelRenderer.IsInputEnabled = false;
    }

    public void Hide()
    {
        CloseAll();
        _isVisible = false;
    }

    public void Show()
    {
        _isVisible = true;
        _selectedCityPanelRenderer.IsInputEnabled = true;
    }

    public void SwitchToPrestigeTab()
    {
        _activeTab = 1;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _inputService.PointerPressed -= HandlePointerPressed;
        _inputService.PointerMoved -= HandlePointerMoved;
        _playerResourcesOverlayRenderer.Dispose();
        _selectedCityPanelRenderer.Dispose();
        _settingsMenu.Dispose();
        _tradeRenderer.Dispose();
        _prestigeRenderer.Dispose();
        _buttonPaint.Dispose();
        _disabledButtonPaint.Dispose();
        _buttonTextPaint.Dispose();
        _disabledTextPaint.Dispose();
        _buttonFont.Dispose();
        _activeTabPaint.Dispose();
        _inactiveTabPaint.Dispose();
        _activeTabBorderPaint.Dispose();
        _tabFont.Dispose();
        _prestigeMapRenderer.Dispose();
        _prestigeHistoryRenderer.Dispose();
        _timeControlRenderer.Dispose();
        _disposed = true;
    }
}
