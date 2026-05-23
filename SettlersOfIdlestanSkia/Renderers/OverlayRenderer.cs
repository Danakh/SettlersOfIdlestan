using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers;

public sealed class OverlayRenderer : IGameRenderer
{
    private const float TradeButtonWidth = 120;
    private const float PrestigeButtonWidth = 120;
    private const float TradeButtonHeight = 38;
    private const float TradeButtonMargin = 14;
    private const float ButtonSpacing = 10;
    private const float CityPanelReservedBottomHeight = TradeButtonHeight + TradeButtonMargin * 2;

    private readonly InputHandlingService _inputService;
    private readonly GameControllerService _gameControllerService;
    private readonly ILocalizationService _localization;
    private readonly PlayerResourcesOverlayRenderer _playerResourcesOverlayRenderer;
    private readonly SettingsMenu _settingsMenu;
    private readonly SelectedCityPanelRenderer _selectedCityPanelRenderer;
    private readonly TradeRenderer _tradeRenderer;
    private readonly PrestigeRenderer _prestigeRenderer;

    private readonly SKPaint _buttonPaint = new() { Color = new SKColor(46, 125, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _disabledButtonPaint = new() { Color = new SKColor(90, 90, 96), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _buttonTextPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _disabledTextPaint = new() { Color = new SKColor(180, 180, 185), IsAntialias = true };
    private readonly SKFont _buttonFont = new() { Size = 14, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };

    private SKSize _canvasSize;
    private SKRect _tradeButtonRect = SKRect.Empty;
    private SKRect _prestigeButtonRect = SKRect.Empty;
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
        PrestigeRenderer prestigeRenderer)
    {
        _inputService = inputService;
        _gameControllerService = gameControllerService;
        _localization = localization;
        _playerResourcesOverlayRenderer = playerResourcesOverlayRenderer;
        _settingsMenu = settingsMenu;
        _selectedCityPanelRenderer = selectedCityPanelRenderer;
        _tradeRenderer = tradeRenderer;
        _prestigeRenderer = prestigeRenderer;
        _inputService.PointerPressed += HandlePointerPressed;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        _playerResourcesOverlayRenderer.Initialize(canvasSize);
        _selectedCityPanelRenderer.Initialize(canvasSize);
        _selectedCityPanelRenderer.ReservedBottomHeight = CityPanelReservedBottomHeight;
        _tradeRenderer.Initialize(canvasSize);
        _prestigeRenderer.Initialize(canvasSize);
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed)
            return;

        if (!_isVisible)
            return;

        _selectedCityPanelRenderer.IsInputEnabled = !_tradeRenderer.IsOpen && !_prestigeRenderer.IsOpen;
        _playerResourcesOverlayRenderer.Render(canvas, context);
        _selectedCityPanelRenderer.Render(canvas, context);
        DrawActionButtons(canvas);

        float gearX = _canvasSize.Width - PlayerResourcesOverlayRenderer.Padding - PlayerResourcesOverlayRenderer.IconSize;
        _settingsMenu.Draw(canvas, gearX, PlayerResourcesOverlayRenderer.BarHeight);

        _tradeRenderer.Render(canvas);
        _prestigeRenderer.Render(canvas);
    }

    private void DrawActionButtons(SKCanvas canvas)
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
            canvas.DrawRoundRect(_prestigeButtonRect, 7, 7, isAvailable ? _buttonPaint : _disabledButtonPaint);
            canvas.DrawText(_localization.Get("prestige_action"), _prestigeButtonRect.MidX, _prestigeButtonRect.MidY + 6, SKTextAlign.Center, _buttonFont, isAvailable ? _buttonTextPaint : _disabledTextPaint);
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

    public void Dispose()
    {
        if (_disposed)
            return;

        _inputService.PointerPressed -= HandlePointerPressed;
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
        _disposed = true;
    }
}
