using SkiaSharp;
using SettlersOfIdlestan.Model.HexGrid;
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
                    _cityBuildingService.SetSelectedCity(city.Position);
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

        // Fallback: garde le comportement de récolte manuelle sur clic hex.
        var hex = _renderer.ScreenToHex(e.Position, _cameraService.CanvasSize, _cameraService.ZoomLevel, _cameraService.Position);
        var hexCoord = new HexCoord(hex.q, hex.r);
        var islandState = _gameControllerService.CurrentIslandState;
        var playerIndex = islandState?.PlayerCivilization.Index ?? 0;
        if (islandState?.VisibleIslandMaps.TryGetValue(playerIndex, out var visibleMap) == true &&
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

        var buildableVertices = _gameControllerService.GetBuildableCityVerticesForPlayer();
        var buildableEdges = _gameControllerService.GetBuildableRoadEdgesForPlayer();

        var islandPoint = _renderer.ScreenToIsland(screenPoint, _cameraService.CanvasSize, _cameraService.ZoomLevel, _cameraService.Position);

        var hoveredCityVertex = GetHoveredCityVertex(islandPoint);

        Vertex? hoveredVertex = null;
        var nearestVertex = _renderer.IslandToNearestVertex(islandPoint);
        if (buildableVertices.Any(v => v.Equals(nearestVertex)))
        {
            var dist = SKPoint.Distance(islandPoint, _renderer.VertexToIslandPoint(nearestVertex));
            if (dist <= VertexHoverRadius)
            {
                hoveredVertex = nearestVertex;
            }
        }

        Edge? hoveredEdge = null;
        var nearestEdge = _renderer.IslandToNearestEdge(islandPoint);
        if (buildableEdges.Any(e => e.Equals(nearestEdge)))
        {
            var dist = SKPoint.Distance(islandPoint, _renderer.EdgeToIslandPoint(nearestEdge));
            if (dist <= EdgeHoverRadius)
            {
                hoveredEdge = nearestEdge;
            }
        }

        HexCoord? hoveredHex = null;
        if (hoveredVertex == null && hoveredEdge == null && hoveredCityVertex == null)
        {
            var hexCoord = _renderer.IslandToHexCoord(islandPoint);
            var (hx, hy) = _renderer.AxialToIsland(hexCoord.Q, hexCoord.R);
            if (_renderer.IsPointInHexagon(islandPoint.X, islandPoint.Y, hx, hy))
            {
                var islandState = _gameControllerService.CurrentIslandState;
                var playerIndex = islandState?.PlayerCivilization.Index ?? 0;
                if (islandState?.VisibleIslandMaps.TryGetValue(playerIndex, out var visibleMap) == true &&
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
            hoveredHex
        );
    }

    private Vertex? GetHoveredCityVertex(SKPoint islandPoint)
    {
        if (_renderer == null)
            return null;

        var islandState = _gameControllerService.CurrentIslandState;
        if (islandState == null)
            return null;

        var cities = islandState.GetAllCities();
        if (cities == null || !cities.Any())
            return null;

        Vertex? best = null;
        var bestDistance = float.MaxValue;

        var playerIndex = islandState.PlayerCivilization.Index;
        foreach (var city in cities)
        {
            if (city.CivilizationIndex != playerIndex)
                continue;

            if (islandState.VisibleIslandMaps.TryGetValue(playerIndex, out var visibleMap) &&
                !city.Position.GetHexes().Any(visibleMap.HasTile))
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

    private void SetSelectedCity(Vertex selectedCityVertex)
    {
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
    HexCoord? HoveredHex)
{
    public static ConstructionHoverState Empty =>
        new(Array.Empty<Vertex>(), Array.Empty<Edge>(), null, null, null, null, null);
}

