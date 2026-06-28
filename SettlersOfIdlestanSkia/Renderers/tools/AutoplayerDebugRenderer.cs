using SkiaSharp;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Debug;

public class AutoplayerDebugRenderer : IGameRenderer
{
    public enum AutoplayerMode { Inactive, Active }

    private AutoplayerMode _mode = AutoplayerMode.Inactive;

    private const float ButtonWidth = 80f;
    private const float ButtonHeight = 22f;
    private const float ButtonSpacing = 3f;
    private const float MarginLeft = 10f;
    private const float MarginBottom = 10f;

    private static readonly string[] Labels = { "Inactif", "Stratégie" };

    private readonly GameControllerService _gameControllerService;
    private readonly InputHandlingService _inputService;

    private SKSize _canvasSize;
    private readonly SKRect[] _buttonRects = new SKRect[2];

    private CivilizationAutoplayer? _autoplayer;
    private PriorityAutoplayStrategy? _strategy;
    private object? _lastCivRef;
    private AutoplayerMode _lastBuiltMode = AutoplayerMode.Inactive;

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
        for (int i = 0; i < 2; i++)
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

        for (int i = 0; i < 2; i++)
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
                worldState.GetMapForZ(IslandMap.SurfaceLayer)!,
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
            _lastBuiltMode = AutoplayerMode.Inactive;
        }

        if (_mode != _lastBuiltMode)
        {
            _strategy = _mode == AutoplayerMode.Active
                ? CivilizationAutoplayerPriorities.Unified(_autoplayer!, mainController.BuildingController)
                : null;
            _lastBuiltMode = _mode;
        }

        _strategy?.TryStepOnce();
    }

    private void DrawButton(SKCanvas canvas, SKRect rect, string label, bool isActive)
    {
        canvas.DrawRoundRect(rect, 4, 4, isActive ? _activePaint : _inactivePaint);
        canvas.DrawRoundRect(rect, 4, 4, _borderPaint);
        float textY = rect.MidY + _font.Size / 2f - 2f;
        SkiaTextUtils.DrawText(canvas, label, rect.MidX, textY, SKTextAlign.Center, _font, _textPaint);
    }

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (_disposed || !DebugSettings.ShowAutoplayerCommands || e.Button != PointerButton.Left)
            return;

        for (int i = 0; i < 2; i++)
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
