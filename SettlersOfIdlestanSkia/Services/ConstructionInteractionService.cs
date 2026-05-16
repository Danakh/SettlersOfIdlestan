using SkiaSharp;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Renderers;

namespace SettlersOfIdlestanSkia.Services;

public interface IConstructionHoverProvider
{
    ConstructionHoverState HoverState { get; }
}

public sealed class ConstructionInteractionService : IConstructionHoverProvider
{
    private const float VertexHoverRadius = 14f;
    private const float EdgeHoverRadius = 12f;

    private readonly GameControllerService _gameControllerService;
    private readonly HarvestService _harvestService;
    private readonly InputHandlingService _inputService;
    private readonly CameraService _cameraService;
    private readonly CityBuildingService _cityBuildingService;
    private IslandMainRenderer? _renderer;

    public ConstructionHoverState HoverState { get; private set; } = ConstructionHoverState.Empty;

    public ConstructionInteractionService(
        GameControllerService gameControllerService,
        HarvestService harvestService,
        InputHandlingService inputService,
        CameraService cameraService,
        CityBuildingService cityBuildingService)
    {
        _gameControllerService = gameControllerService ?? throw new ArgumentNullException(nameof(gameControllerService));
        _harvestService = harvestService ?? throw new ArgumentNullException(nameof(harvestService));
        _inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
        _cameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));
        _cityBuildingService = cityBuildingService ?? throw new ArgumentNullException(nameof(cityBuildingService));

        _inputService.PointerMoved += OnPointerMoved;
        _inputService.PointerPressed += OnPointerPressed;
        _inputService.PointerReleased += OnPointerReleased;
    }

    public void AttachRenderer(IslandMainRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        RefreshHover(e.Position);
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        RefreshHover(e.Position);
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e)
    {
        RefreshHover(e.Position);

        if (HoverState.HoveredCityVertex != null)
        {
            SetSelectedCity(HoverState.HoveredCityVertex);
            return;
        }

        if (HoverState.HoveredVertex != null && _gameControllerService.TryBuildCityForPlayer(HoverState.HoveredVertex))
        {
            RefreshHover(e.Position);
            return;
        }

        if (HoverState.HoveredEdge != null && _gameControllerService.TryBuildRoadForPlayer(HoverState.HoveredEdge))
        {
            RefreshHover(e.Position);
            return;
        }

        if (_renderer == null)
            return;

        // Fallback: garde le comportement de récolte manuelle sur clic hex.
        var hex = _renderer.ScreenToHex(e.Position, _cameraService.CanvasSize, _cameraService.ZoomLevel, _cameraService.Position);
        _harvestService.TryManualHarvest(new HexCoord(hex.q, hex.r));
        RefreshHover(e.Position);
    }

    private void RefreshHover(SKPoint screenPoint)
    {
        if (_renderer == null)
            return;

        var buildableVertices = _gameControllerService.GetBuildableCityVerticesForPlayer();
        var buildableEdges = _gameControllerService.GetBuildableRoadEdgesForPlayer();

        var islandPoint = _renderer.ScreenToIsland(screenPoint, _cameraService.CanvasSize, _cameraService.ZoomLevel, _cameraService.Position);

        var hoveredCityVertex = GetHoveredCityVertex(islandPoint);

        Vertex? hoveredVertex = null;
        var nearestVertex = _renderer.IslandToNearestVertex(islandPoint);
        if (buildableVertices.Any(v => v.Equals(nearestVertex)))
        {
            var dist = Distance(islandPoint, _renderer.VertexToIslandPoint(nearestVertex));
            if (dist <= VertexHoverRadius)
            {
                hoveredVertex = nearestVertex;
            }
        }

        Edge? hoveredEdge = null;
        var nearestEdge = _renderer.IslandToNearestEdge(islandPoint);
        if (buildableEdges.Any(e => e.Equals(nearestEdge)))
        {
            var dist = Distance(islandPoint, _renderer.EdgeToIslandPoint(nearestEdge));
            if (dist <= EdgeHoverRadius)
            {
                hoveredEdge = nearestEdge;
            }
        }

        HoverState = new ConstructionHoverState(
            buildableVertices,
            buildableEdges,
            hoveredVertex,
            hoveredEdge,
            hoveredCityVertex,
            _cityBuildingService.SelectedCity?.Position
        );
    }

    private Vertex? GetHoveredCityVertex(SKPoint islandPoint)
    {
        if (_renderer == null)
            return null;

        var cities = _gameControllerService?.CurrentIslandState?.GetAllCities();
        if (cities == null || !cities.Any())
            return null;

        Vertex? best = null;
        var bestDistance = float.MaxValue;

        foreach (var city in cities)
        {
            var pt = _renderer.VertexToIslandPoint(city.Position);
            var dist = Distance(islandPoint, pt);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                best = city.Position;
            }
        }

        return bestDistance <= 12f ? best : null;
    }

    private void SetSelectedCity(Vertex selectedCityVertex)
    {
        _cityBuildingService.SetSelectedCity(selectedCityVertex);
        HoverState = HoverState with { SelectedCityVertex = selectedCityVertex };
    }

    private static float Distance(SKPoint a, SKPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    public void Cleanup()
    {
        _inputService.PointerMoved -= OnPointerMoved;
        _inputService.PointerPressed -= OnPointerPressed;
        _inputService.PointerReleased -= OnPointerReleased;
    }
}

public readonly record struct ConstructionHoverState(
    IReadOnlyList<Vertex> BuildableVertices,
    IReadOnlyList<Edge> BuildableEdges,
    Vertex? HoveredVertex,
    Edge? HoveredEdge,
    Vertex? HoveredCityVertex,
    Vertex? SelectedCityVertex)
{
    public static ConstructionHoverState Empty =>
        new(Array.Empty<Vertex>(), Array.Empty<Edge>(), null, null, null, null);
}

