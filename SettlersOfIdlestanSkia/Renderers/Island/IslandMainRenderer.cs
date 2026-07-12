using SkiaSharp;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Renderers.Overlay;

namespace SettlersOfIdlestanSkia.Renderers.Island;

/// <summary>
/// Renderer principal de l'île.
/// Il applique la transformation caméra et orchestre les renderers hexagonaux.
/// C'est aussi l'unique point de conversion écran -> Island.
/// </summary>
public class IslandMainRenderer : HexBasedRenderer, IGameRenderer
{
    private readonly GameBoardRenderer _gameBoardRenderer;
    private readonly RoadRenderer _roadRenderer;
    private readonly CityRenderer _cityRenderer;
    private readonly MaritimeBeaconRenderer _maritimeBeaconRenderer;
    private readonly MobileCampRenderer _mobileCampRenderer;
    private readonly MilitaryScoreOverlay _militaryScoreOverlay;
    private readonly HarvestRenderer _harvestRenderer;
    private readonly MonsterRenderer _banditRenderer;
    private readonly VolcanoRenderer _volcanoRenderer;
    private readonly MilitaryRenderer _militaryRenderer;
    private readonly HarvestParticleSystem _harvestParticleSystem;
    private readonly IConstructionHoverProvider _constructionHoverProvider;
    private readonly TooltipRenderer _tooltipRenderer;
    private readonly HarvestController _harvestController;
    private float _blackFadeDuration;
    private float _blackFadeElapsed;
    private bool _isBlackFadeActive;
    private SKPaint? _fadePaint;
    public bool IsVisible { get; set; } = true;
    public Func<bool>? SuppressCities { get; set; }

    public void ConnectMilitaryEvents(
        MilitaryController militaryController,
        GameControllerService gameControllerService,
        Func<bool> isPrestigeTransitionPending,
        Func<bool> isIslandTabActive)
    {
        _banditRenderer.Connect(militaryController, gameControllerService, isPrestigeTransitionPending, isIslandTabActive);
        _militaryRenderer.Connect(militaryController, gameControllerService, isPrestigeTransitionPending, isIslandTabActive);
    }

    public void ConnectVolcanoEvents(
        VolcanoController volcanoController,
        GameControllerService gameControllerService,
        Func<bool> isPrestigeTransitionPending,
        Func<bool> isIslandTabActive)
    {
        _volcanoRenderer.Connect(volcanoController, gameControllerService, isPrestigeTransitionPending, isIslandTabActive);
    }

    public void ConnectMilitaryInteractionService(MilitaryInteractionService service)
    {
        _militaryRenderer.ConnectInteractionService(service);
    }

    public IslandMainRenderer(IConstructionHoverProvider constructionHoverProvider, TooltipRenderer tooltipRenderer, LocalizationService localizationService, HarvestController harvestController, ResourceManager resourceManager, MilitaryController militaryController, Func<int> currentLayer)
    {
        _gameBoardRenderer = new GameBoardRenderer(harvestController, resourceManager);
        _roadRenderer = new RoadRenderer(tooltipRenderer);
        _militaryScoreOverlay = new MilitaryScoreOverlay(resourceManager);
        _cityRenderer = new CityRenderer(tooltipRenderer, resourceManager, militaryController, _militaryScoreOverlay);
        _maritimeBeaconRenderer = new MaritimeBeaconRenderer(tooltipRenderer, militaryController, _militaryScoreOverlay);
        _mobileCampRenderer = new MobileCampRenderer(tooltipRenderer, militaryController, _militaryScoreOverlay);
        _harvestParticleSystem = new HarvestParticleSystem();
        _harvestRenderer = new HarvestRenderer(_harvestParticleSystem, resourceManager, currentLayer);
        _banditRenderer = new MonsterRenderer(resourceManager);
        _volcanoRenderer = new VolcanoRenderer(resourceManager);
        _militaryRenderer = new MilitaryRenderer(tooltipRenderer, localizationService);
        _constructionHoverProvider = constructionHoverProvider;
        _tooltipRenderer = tooltipRenderer;
        _harvestController = harvestController;
    }

    public void ConnectMonumentService(MonumentService monumentService) => _gameBoardRenderer.ConnectMonumentService(monumentService);

    public void ConnectHarvestEvents(
        HarvestService harvestService,
        GameControllerService gameControllerService,
        Func<bool> isPrestigeTransitionPending,
        Func<bool> isIslandTabActive,
        Func<bool>? showParticles = null)
    {
        _harvestRenderer.Connect(
            harvestService,
            gameControllerService,
            hex => HexCoordToIslandPoint(hex),
            vertex => VertexToIslandPoint(vertex),
            isPrestigeTransitionPending,
            isIslandTabActive,
            showParticles);
    }

    public void Initialize(SKSize canvasSize)
    {
        _gameBoardRenderer.Initialize(canvasSize);
        _roadRenderer.Initialize(canvasSize);
        _militaryScoreOverlay.Initialize();
        _cityRenderer.Initialize(canvasSize);
        _maritimeBeaconRenderer.Initialize(canvasSize);
        _mobileCampRenderer.Initialize(canvasSize);
        _harvestRenderer.Initialize(canvasSize);
        _banditRenderer.Initialize(canvasSize);
        _volcanoRenderer.Initialize(canvasSize);
        _militaryRenderer.Initialize(canvasSize);
        _fadePaint = new SKPaint { Style = SKPaintStyle.Fill };
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (!IsVisible) return;

        if (_isBlackFadeActive)
        {
            _blackFadeElapsed = Math.Min(_blackFadeElapsed + context.DeltaTime, _blackFadeDuration);
            var progress = _blackFadeDuration <= 0 ? 1f : _blackFadeElapsed / _blackFadeDuration;
            _fadePaint!.Color = new SKColor(0, 0, 0, (byte)(255 * progress));
            canvas.DrawRect(new SKRect(0, 0, context.CanvasSize.Width, context.CanvasSize.Height), _fadePaint);
            return;
        }

        var state = _constructionHoverProvider!.HoverState;

        _tooltipRenderer.SetIslandRenderContext(this, context);

        if (state.HoveredHex != null &&
            state.HoveredHex.Z == context.CurrentLayer &&
            context.GameState is MainGameState mgs &&
            mgs.CurrentWorldState != null)
        {
            _tooltipRenderer.SetHexHarvestTooltip(
                state.HoveredHex,
                _harvestController,
                mgs.CurrentWorldState,
                mgs.Clock.CurrentTick);
        }

        using (ApplyCameraTransform(canvas, context))
        {
            _gameBoardRenderer.Render(canvas, context);
            _banditRenderer.Render(canvas, context);
            _volcanoRenderer.Render(canvas, context);
            _roadRenderer.Render(canvas, context);
            bool skipCities = SuppressCities?.Invoke() == true;
            if (!skipCities)
            {
                _cityRenderer.Render(canvas, context);
                _maritimeBeaconRenderer.Render(canvas, context);
                _mobileCampRenderer.Render(canvas, context);
            }
            _harvestRenderer.Render(canvas, context);

            _roadRenderer.RenderConstructionHighlights(canvas, state, context);
            if (!skipCities)
            {
                _cityRenderer.RenderConstructionHighlights(canvas, state, context);
                _maritimeBeaconRenderer.RenderConstructionHighlights(canvas, state, context);
                _mobileCampRenderer.RenderConstructionHighlights(canvas, state, context);
            }
            _militaryRenderer.Render(canvas, context);
        }
    }

    public SKPoint ScreenToIsland(SKPoint screenPoint, SKSize canvasSize, float zoomLevel, SKPoint cameraPos)
    {
        return new SKPoint(
            screenPoint.X / zoomLevel + cameraPos.X,
            screenPoint.Y / zoomLevel + cameraPos.Y);
    }

    public SKPoint IslandToScreen(SKPoint islandPoint, float zoomLevel, SKPoint cameraPos)
    {
        return new SKPoint(
            (islandPoint.X - cameraPos.X) * zoomLevel,
            (islandPoint.Y - cameraPos.Y) * zoomLevel);
    }

    public (int q, int r) ScreenToHex(SKPoint screenPoint, SKSize canvasSize, float zoomLevel, SKPoint cameraPos)
    {
        var islandPoint = ScreenToIsland(screenPoint, canvasSize, zoomLevel, cameraPos);
        var hex = IslandToHexCoord(islandPoint);
        return (hex.Q, hex.R);
    }

    public SKPoint HexCoordToIslandPoint(HexCoord coord)
    {
        var (x, y) = AxialToIsland(coord.Q, coord.R);
        return new SKPoint(x, y);
    }

    public SKPoint VertexToIslandPoint(Vertex vertex) => VertexToIsland(vertex);

    public SKPoint EdgeToIslandPoint(Edge edge)
    {
        var (h1, h2) = edge.GetHexes();
        return EdgeToIsland(h1.Q, h1.R, h2.Q, h2.R);
    }

    public void BeginBlackFade(float durationSeconds)
    {
        _harvestParticleSystem.Clear();
        _blackFadeDuration = Math.Max(0.01f, durationSeconds);
        _blackFadeElapsed = 0;
        _isBlackFadeActive = true;
    }

    public bool IsBlackFadeComplete => _isBlackFadeActive && _blackFadeElapsed >= _blackFadeDuration;

    public void EndBlackFade()
    {
        _isBlackFadeActive = false;
        _blackFadeElapsed = 0;
    }

    public void Dispose()
    {
        _gameBoardRenderer.Dispose();
        _roadRenderer.Dispose();
        _militaryScoreOverlay.Dispose();
        _cityRenderer.Dispose();
        _maritimeBeaconRenderer.Dispose();
        _mobileCampRenderer.Dispose();
        _harvestRenderer.Dispose();
        _banditRenderer.Dispose();
        _militaryRenderer.Dispose();
        _tooltipRenderer.Dispose();
        _fadePaint?.Dispose();
    }

    private CameraTransformScope ApplyCameraTransform(SKCanvas canvas, GameRenderContext context)
    {
        return new CameraTransformScope(canvas, context.ZoomLevel, context.CameraPosition);
    }

    private sealed class CameraTransformScope : IDisposable
    {
        private readonly SKCanvas _canvas;
        private bool _disposed;

        public CameraTransformScope(SKCanvas canvas, float zoomLevel, SKPoint cameraPos)
        {
            _canvas = canvas;
            _canvas.Save();
            _canvas.Translate(-cameraPos.X * zoomLevel, -cameraPos.Y * zoomLevel);
            _canvas.Scale(zoomLevel, zoomLevel);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _canvas.Restore();
            _disposed = true;
        }
    }
}
