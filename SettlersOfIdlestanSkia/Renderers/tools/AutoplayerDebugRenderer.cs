using SkiaSharp;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Debug;

public class AutoplayerDebugRenderer : IGameRenderer
{
    public enum AutoplayerMode { Inactive, Step1, Step2, Step3, Military }

    private AutoplayerMode _mode = AutoplayerMode.Inactive;

    private const float ButtonWidth = 58f;
    private const float ButtonHeight = 22f;
    private const float ButtonSpacing = 3f;
    private const float MarginLeft = 10f;
    private const float MarginBottom = 10f;

    private static readonly string[] Labels = { "Inactif", "Step 1", "Step 2", "Step 3", "Armée" };

    private readonly GameControllerService _gameControllerService;
    private readonly InputHandlingService _inputService;

    private SKSize _canvasSize;
    private readonly SKRect[] _buttonRects = new SKRect[5];

    private CivilizationAutoplayer? _autoplayer;
    private object? _lastCivRef;

    private readonly SKPaint _activePaint   = new() { Color = new SKColor(60, 180, 80),  Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _inactivePaint = new() { Color = new SKColor(35, 35, 50),   Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _borderPaint   = new() { Color = new SKColor(100, 100, 130), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _textPaint     = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKFont  _font          = new() { Size = 11, Typeface = SkiaFonts.Bold };

    private bool _disposed;

    public AutoplayerDebugRenderer(GameControllerService gameControllerService, InputHandlingService inputService)
    {
        _gameControllerService = gameControllerService;
        _inputService = inputService;
        _inputService.PointerPressed += HandlePointerPressed;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        RecalcRects();
    }

    private void RecalcRects()
    {
        float y = _canvasSize.Height - MarginBottom - ButtonHeight;
        for (int i = 0; i < 5; i++)
        {
            float x = MarginLeft + i * (ButtonWidth + ButtonSpacing);
            _buttonRects[i] = new SKRect(x, y, x + ButtonWidth, y + ButtonHeight);
        }
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed || !DebugSettings.ShowAutoplayerCommands)
            return;

        RunAutoplayerStep();

        for (int i = 0; i < 5; i++)
            DrawButton(canvas, _buttonRects[i], Labels[i], (int)_mode == i);
    }

    private void RunAutoplayerStep()
    {
        var playerCiv = _gameControllerService.PlayerCivilization;
        var worldState = _gameControllerService.CurrentWorldState;
        var mainController = _gameControllerService.MainGameController;

        if (playerCiv == null || worldState == null)
            return;

        if (!ReferenceEquals(playerCiv, _lastCivRef))
        {
            _autoplayer = new CivilizationAutoplayer(
                playerCiv,
                worldState.GetMapForZ(IslandMap.SurfaceLayer),
                mainController.RoadController,
                mainController.HarvestController,
                mainController.BuildingController,
                mainController.CityBuilderController,
                mainController.TradeController,
                mainController.ResearchController,
                mainController.PrestigeController,
                mainController.PrestigeMapController,
                worldState,
                mainController.CurrentMainState?.PrestigeState,
                mainController.PerformPrestige);
            _lastCivRef = playerCiv;
        }

        switch (_mode)
        {
            case AutoplayerMode.Step1:    _autoplayer!.TryStep1Once(); break;
            case AutoplayerMode.Step2:    _autoplayer!.TryStep2Once(); break;
            case AutoplayerMode.Step3:    _autoplayer!.TryStep3Once(); break;
            case AutoplayerMode.Military: _autoplayer!.TryMilitaryStepOnce(); break;
        }
    }

    private void DrawButton(SKCanvas canvas, SKRect rect, string label, bool isActive)
    {
        canvas.DrawRoundRect(rect, 4, 4, isActive ? _activePaint : _inactivePaint);
        canvas.DrawRoundRect(rect, 4, 4, _borderPaint);
        float textY = rect.MidY + _font.Size / 2f - 2f;
        canvas.DrawText(label, rect.MidX, textY, SKTextAlign.Center, _font, _textPaint);
    }

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (_disposed || !DebugSettings.ShowAutoplayerCommands || e.Button != PointerButton.Left)
            return;

        for (int i = 0; i < 5; i++)
        {
            if (_buttonRects[i].Contains(e.Position.X, e.Position.Y))
            {
                _mode = (AutoplayerMode)i;
                return;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _inputService.PointerPressed -= HandlePointerPressed;
        _activePaint.Dispose();
        _inactivePaint.Dispose();
        _borderPaint.Dispose();
        _textPaint.Dispose();
        _font.Dispose();
        _disposed = true;
    }
}
