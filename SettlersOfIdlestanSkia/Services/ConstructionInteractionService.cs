using SkiaSharp;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Renderers.Island;

namespace SettlersOfIdlestanSkia.Services;

public interface IConstructionHoverProvider
{
    ConstructionHoverState HoverState { get; }
}

public sealed class ConstructionInteractionService : IConstructionHoverProvider
{
    private const float VertexHoverRadius = 14f;
    private const float EdgeHoverRadius = 12f;
    private const double DoubleClickMaxMs = 400.0;
    private const float DoubleClickMaxDist = 20f;

    private readonly GameControllerService _gameControllerService;
    private readonly HarvestService _harvestService;
    private readonly InputHandlingService _inputService;
    private readonly CameraService _cameraService;
    private readonly CityBuildingService _cityBuildingService;
    private WonderService? _wonderService;
    private IslandMainRenderer? _renderer;

    private DateTime _lastClickTime = DateTime.MinValue;
    private SKPoint _lastClickPosition = SKPoint.Empty;

    public ConstructionHoverState HoverState { get; private set; } = ConstructionHoverState.Empty;

    public Func<SKPoint, bool>? ShouldSuppressHover { get; set; }

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

    public void AttachWonderService(WonderService wonderService)
    {
        _wonderService = wonderService;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (ShouldSuppressHover?.Invoke(e.Position) == true)
        {
            ClearHover();
            return;
        }
        RefreshHover(e.Position);
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if (e.Button != PointerButton.Left)
            return;

        if (ShouldSuppressHover?.Invoke(e.Position) == true)
            return;

        RefreshHover(e.Position);
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e)
    {
        if (e.Button != PointerButton.Left)
            return;

        if (ShouldSuppressHover?.Invoke(e.Position) == true)
            return;

        RefreshHover(e.Position);

        var now = e.Timestamp;
        var elapsed = (now - _lastClickTime).TotalMilliseconds;
        var dist = SKPoint.Distance(e.Position, _lastClickPosition);
        var isDoubleClick = elapsed <= DoubleClickMaxMs && dist <= DoubleClickMaxDist;

        if (isDoubleClick)
        {
            _lastClickTime = DateTime.MinValue;

            if (HoverState.HoveredVertex != null)
            {
                var city = _gameControllerService.TryBuildCityForPlayer(HoverState.HoveredVertex);
                if (city != null)
                    SetSelectedCity(city.Position);
                RefreshHover(e.Position);
                return;
            }

            if (HoverState.HoveredEdge != null && _gameControllerService.TryBuildRoadForPlayer(HoverState.HoveredEdge) != null)
            {
                RefreshHover(e.Position);
                return;
            }
        }
        else
        {
            _lastClickTime = now;
            _lastClickPosition = e.Position;
        }

        if (HoverState.HoveredCityVertex != null)
        {
            SetSelectedCity(HoverState.HoveredCityVertex);
            return;
        }

        if (_renderer == null)
            return;

        // Fallback: clic sur un hex — vérifie d'abord si c'est une wonder.
        var WorldState = _gameControllerService.CurrentWorldState;
        int currentZ = WorldState?.CurrentViewedLayer ?? IslandMap.SurfaceLayer;
        var hex = _renderer.ScreenToHex(e.Position, _cameraService.CanvasSize, _cameraService.ZoomLevel, _cameraService.Position);
        var hexCoord = new HexCoord(hex.q, hex.r, currentZ);
        var playerIndex = WorldState?.PlayerCivilization.Index ?? 0;

        var clickedWonder = WorldState?.Features.OfType<Wonder>().FirstOrDefault(w => w.Position.Equals(hexCoord));
        if (clickedWonder != null)
        {
            _cityBuildingService.ClearSelectedCity();
            _wonderService?.SetSelectedWonder(clickedWonder);
            RefreshHover(e.Position);
            return;
        }

        if (WorldState?.Visibility.GetForZ(hexCoord.Z).TryGetValue(playerIndex, out var visibleMap) == true &&
            visibleMap.HasTile(hexCoord))
        {
            _harvestService.TryManualHarvest(hexCoord);
        }
        RefreshHover(e.Position);
    }

    private void RefreshHover(SKPoint screenPoint)
    {
        if (_renderer == null)
            return;

        int currentZ = _gameControllerService.CurrentWorldState?.CurrentViewedLayer ?? IslandMap.SurfaceLayer;

        var buildableVertices = _gameControllerService.GetBuildableCityVerticesForPlayer();
        var buildableEdges = _gameControllerService.GetBuildableRoadEdgesForPlayer();
        var enemyProtectedEdges = _gameControllerService.GetEnemyProtectedRoadEdgesForPlayer();

        var islandPoint = _renderer.ScreenToIsland(screenPoint, _cameraService.CanvasSize, _cameraService.ZoomLevel, _cameraService.Position);

        var hoveredCityVertex = GetHoveredCityVertex(islandPoint);
        var hoveredEnemyCityVertex = hoveredCityVertex == null ? GetHoveredEnemyCityVertex(islandPoint) : null;

        Vertex? hoveredVertex = null;
        var nearestVertex = _renderer.IslandToNearestVertex(islandPoint, currentZ);
        if (buildableVertices.Any(v => v.Equals(nearestVertex)))
        {
            var dist = SKPoint.Distance(islandPoint, _renderer.VertexToIslandPoint(nearestVertex));
            if (dist <= VertexHoverRadius)
            {
                hoveredVertex = nearestVertex;
            }
        }

        Edge? hoveredEdge = null;
        Edge? hoveredEnemyProtectedEdge = null;
        var nearestEdge = _renderer.IslandToNearestEdge(islandPoint, currentZ);
        var edgeDist = SKPoint.Distance(islandPoint, _renderer.EdgeToIslandPoint(nearestEdge));
        if (edgeDist <= EdgeHoverRadius)
        {
            if (buildableEdges.Any(e => e.Equals(nearestEdge)))
                hoveredEdge = nearestEdge;
            else if (enemyProtectedEdges.Any(e => e.Equals(nearestEdge)))
                hoveredEnemyProtectedEdge = nearestEdge;
        }

        HexCoord? hoveredHex = null;
        if (hoveredVertex == null && hoveredEdge == null && hoveredEnemyProtectedEdge == null && hoveredCityVertex == null && hoveredEnemyCityVertex == null)
        {
            var hexCoord = _renderer.IslandToHexCoord(islandPoint, currentZ);
            var (hx, hy) = _renderer.AxialToIsland(hexCoord.Q, hexCoord.R);
            if (_renderer.IsPointInHexagon(islandPoint.X, islandPoint.Y, hx, hy))
            {
                var WorldState = _gameControllerService.CurrentWorldState;
                var playerIndex = WorldState?.PlayerCivilization.Index ?? 0;
                if (WorldState?.Visibility.GetForZ(hexCoord.Z).TryGetValue(playerIndex, out var visibleMap) == true &&
                    visibleMap.HasTile(hexCoord))
                {
                    hoveredHex = hexCoord;
                }
            }
        }

        HoverState = new ConstructionHoverState(
            buildableVertices,
            buildableEdges,
            hoveredVertex,
            hoveredEdge,
            hoveredCityVertex,
            _cityBuildingService.SelectedCity?.Position,
            hoveredHex,
            hoveredEnemyProtectedEdge,
            hoveredEnemyCityVertex
        );
    }

    private Vertex? GetHoveredCityVertex(SKPoint islandPoint)
    {
        if (_renderer == null)
            return null;

        var WorldState = _gameControllerService.CurrentWorldState;
        if (WorldState == null)
            return null;

        var cities = WorldState.GetAllCities();
        if (cities == null || !cities.Any())
            return null;

        Vertex? best = null;
        var bestDistance = float.MaxValue;

        var playerIndex = WorldState.PlayerCivilization.Index;
        foreach (var city in cities)
        {
            if (city.CivilizationIndex != playerIndex)
                continue;

            if (WorldState.Visibility.GetForZ(WorldState.CurrentViewedLayer).TryGetValue(playerIndex, out var visibleMap) &&
                (city.Position.Z != visibleMap.Z ||
                !city.Position.GetHexes().Any(visibleMap.HasTile)))
            {
                continue;
            }

            var pt = _renderer.VertexToIslandPoint(city.Position);
            var dist = SKPoint.Distance(islandPoint, pt);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                best = city.Position;
            }
        }

        return bestDistance <= 12f ? best : null;
    }

    private Vertex? GetHoveredEnemyCityVertex(SKPoint islandPoint)
    {
        if (_renderer == null)
            return null;

        var worldState = _gameControllerService.CurrentWorldState;
        if (worldState == null)
            return null;

        var playerIndex = worldState.PlayerCivilization.Index;
        Vertex? best = null;
        var bestDistance = float.MaxValue;

        if (!worldState.Visibility.GetForZ(worldState.CurrentViewedLayer).TryGetValue(playerIndex, out var visibleMap))
            return null;

        foreach (var city in worldState.GetAllCities())
        {
            if (city.CivilizationIndex == playerIndex)
                continue;
            if (city.Position.Z != visibleMap.Z || !city.Position.GetHexes().Any(visibleMap.HasTile))
                continue;

            var pt = _renderer.VertexToIslandPoint(city.Position);
            var dist = SKPoint.Distance(islandPoint, pt);
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
        _wonderService?.ClearSelectedWonder();
        _cityBuildingService.SetSelectedCity(selectedCityVertex);
        HoverState = HoverState with { SelectedCityVertex = selectedCityVertex };
    }

    public void Cleanup()
    {
        _inputService.PointerMoved -= OnPointerMoved;
        _inputService.PointerPressed -= OnPointerPressed;
        _inputService.PointerReleased -= OnPointerReleased;
    }

    public void ClearHover()
    {
        HoverState = ConstructionHoverState.Empty;
    }
}

public readonly record struct ConstructionHoverState(
    IReadOnlyList<Vertex> BuildableVertices,
    IReadOnlyList<Edge> BuildableEdges,
    Vertex? HoveredVertex,
    Edge? HoveredEdge,
    Vertex? HoveredCityVertex,
    Vertex? SelectedCityVertex,
    HexCoord? HoveredHex,
    Edge? HoveredEnemyProtectedEdge,
    Vertex? HoveredEnemyCityVertex)
{
    public static ConstructionHoverState Empty =>
        new(Array.Empty<Vertex>(), Array.Empty<Edge>(), null, null, null, null, null, null, null);
}

