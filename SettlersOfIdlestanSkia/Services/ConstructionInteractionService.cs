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
    private MonumentService? _monumentService;
    private IslandMainRenderer? _renderer;

    private DateTime _lastClickTime = DateTime.MinValue;
    private SKPoint _lastClickPosition = SKPoint.Empty;

    private List<Vertex>? _cachedBuildableVertices;
    private List<Edge>?   _cachedBuildableEdges;
    private List<Edge>?   _cachedEnemyProtectedEdges;
    private List<Vertex>? _cachedBuildableBeaconVertices;
    private List<Vertex>? _cachedPotentialFleetVertices;
    private List<Vertex>? _cachedPotentialMobileCampVertices;
    private int _cachedCityCount = -1;
    private int _cachedRoadCount = -1;
    private int _cachedBeaconCount = -1;
    private int _cachedGreatLighthouseLevel = -1;
    private int _cachedFleetAndCampCount = -1;
    private bool _cachedMobileCampUnlocked;

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

    public void AttachMonumentService(MonumentService monumentService)
    {
        _monumentService = monumentService;
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

            if (HoverState.HoveredBeaconVertex != null)
            {
                _gameControllerService.TryBuildMaritimeBeaconForPlayer(HoverState.HoveredBeaconVertex);
                RefreshHover(e.Position);
                return;
            }

            if (HoverState.HoveredFleetVertex != null)
            {
                _gameControllerService.TryBuildWarFleetForPlayer(HoverState.HoveredFleetVertex);
                RefreshHover(e.Position);
                return;
            }

            if (HoverState.HoveredMobileCampVertex != null)
            {
                _gameControllerService.TryBuildMobileCampForPlayer(HoverState.HoveredMobileCampVertex);
                RefreshHover(e.Position);
                return;
            }

            if (HoverState.HoveredOwnMobileCampVertex != null)
            {
                _gameControllerService.TryDestroyMobileCampForPlayer(HoverState.HoveredOwnMobileCampVertex);
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

        // Fallback: clic sur un hex — vérifie d'abord si c'est un Monument (Merveille, Mine Profonde…).
        var WorldState = _gameControllerService.CurrentWorldState;
        int currentZ = WorldState?.CurrentViewedLayer ?? IslandMap.SurfaceLayer;
        var hex = _renderer.ScreenToHex(e.Position, _cameraService.CanvasSize, _cameraService.ZoomLevel, _cameraService.Position);
        var hexCoord = new HexCoord(hex.q, hex.r, currentZ);
        var playerIndex = WorldState?.PlayerCivilization.Index ?? 0;

        // Les Os Divins restent non sélectionnables tant que Boussole du Vide n'est pas acquise
        // (ils sont générés dès la création de l'île mais révélés seulement par la recherche).
        var clickedMonument = WorldState?.Features.OfType<Monument>()
            .FirstOrDefault(w => w.Position.Equals(hexCoord)
                && (w is not DivineBones db || db.ShouldRenderIconFor(WorldState.PlayerCivilization)));
        if (clickedMonument != null)
        {
            _cityBuildingService.ClearSelectedCity();
            _monumentService?.SetSelectedInvestable(clickedMonument);
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

    private void RefreshBuildableCache()
    {
        var playerCiv = _gameControllerService.CurrentWorldState?.PlayerCivilization;
        int cityCount = playerCiv?.Cities.Count ?? 0;
        int roadCount = playerCiv?.Roads.Count ?? 0;
        int beaconCount = playerCiv?.MaritimeBeacons.Count ?? 0;
        int greatLighthouseLevel = _gameControllerService.MainGameController.GreatLighthouseController.GetGreatLighthouseLevel();
        int fleetAndCampCount = (playerCiv?.Fleets.Count ?? 0) + (playerCiv?.MobileCamps.Count ?? 0);
        bool mobileCampUnlocked = _gameControllerService.IsMobileCampUnlockedForPlayer();
        if (_cachedBuildableVertices != null && cityCount == _cachedCityCount && roadCount == _cachedRoadCount
            && beaconCount == _cachedBeaconCount && greatLighthouseLevel == _cachedGreatLighthouseLevel
            && fleetAndCampCount == _cachedFleetAndCampCount && mobileCampUnlocked == _cachedMobileCampUnlocked)
            return;
        _cachedBuildableVertices       = _gameControllerService.GetBuildableCityVerticesForPlayer();
        _cachedBuildableEdges          = _gameControllerService.GetBuildableRoadEdgesForPlayer();
        _cachedEnemyProtectedEdges     = _gameControllerService.GetEnemyProtectedRoadEdgesForPlayer();
        _cachedBuildableBeaconVertices = _gameControllerService.GetBuildableMaritimeBeaconVerticesForPlayer();
        _cachedPotentialFleetVertices  = _gameControllerService.GetPotentialWarFleetVerticesForPlayer();
        _cachedPotentialMobileCampVertices = _gameControllerService.GetPotentialMobileCampVerticesForPlayer();
        _cachedCityCount = cityCount;
        _cachedRoadCount = roadCount;
        _cachedBeaconCount = beaconCount;
        _cachedGreatLighthouseLevel = greatLighthouseLevel;
        _cachedFleetAndCampCount = fleetAndCampCount;
        _cachedMobileCampUnlocked = mobileCampUnlocked;
    }

    private void RefreshHover(SKPoint screenPoint)
    {
        if (_renderer == null)
            return;

        int currentZ = _gameControllerService.CurrentWorldState?.CurrentViewedLayer ?? IslandMap.SurfaceLayer;

        RefreshBuildableCache();
        var buildableVertices  = _cachedBuildableVertices!;
        var buildableEdges     = _cachedBuildableEdges!;
        var enemyProtectedEdges = _cachedEnemyProtectedEdges!;
        var buildableBeaconVertices = _cachedBuildableBeaconVertices!;
        var potentialFleetVertices = _cachedPotentialFleetVertices!;
        var potentialMobileCampVertices = _cachedPotentialMobileCampVertices!;

        var islandPoint = _renderer.ScreenToIsland(screenPoint, _cameraService.CanvasSize, _cameraService.ZoomLevel, _cameraService.Position);

        var hoveredCityVertex = GetHoveredCityVertex(islandPoint);
        var hoveredEnemyCityVertex = hoveredCityVertex == null ? GetHoveredEnemyCityVertex(islandPoint) : null;
        var hoveredOwnFleet = hoveredCityVertex == null && hoveredEnemyCityVertex == null ? GetHoveredOwnFleetVertex(islandPoint) : null;
        var hoveredEnemyFleet = hoveredOwnFleet == null && hoveredCityVertex == null && hoveredEnemyCityVertex == null ? GetHoveredEnemyFleetVertex(islandPoint) : null;
        bool noPriorMilitaryHover = hoveredCityVertex == null && hoveredEnemyCityVertex == null && hoveredOwnFleet == null && hoveredEnemyFleet == null;
        var hoveredOwnMobileCamp = noPriorMilitaryHover ? GetHoveredOwnMobileCampVertex(islandPoint) : null;
        var hoveredEnemyMobileCamp = noPriorMilitaryHover && hoveredOwnMobileCamp == null ? GetHoveredEnemyMobileCampVertex(islandPoint) : null;

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

        // Balises maritimes : uniquement en surface (voir MaritimeBeaconController).
        Vertex? hoveredBeaconVertex = null;
        if (hoveredVertex == null && currentZ == IslandMap.SurfaceLayer && buildableBeaconVertices.Any(v => v.Equals(nearestVertex)))
        {
            var dist = SKPoint.Distance(islandPoint, _renderer.VertexToIslandPoint(nearestVertex));
            if (dist <= VertexHoverRadius)
            {
                hoveredBeaconVertex = nearestVertex;
            }
        }

        // Flottes de guerre : sur une balise déjà posée, uniquement en surface (voir WarFleetController).
        // Toujours survolable même sans le Port Impérial, pour afficher l'infobulle du prérequis manquant.
        Vertex? hoveredFleetVertex = null;
        if (hoveredVertex == null && hoveredBeaconVertex == null && currentZ == IslandMap.SurfaceLayer && potentialFleetVertices.Any(v => v.Equals(nearestVertex)))
        {
            var dist = SKPoint.Distance(islandPoint, _renderer.VertexToIslandPoint(nearestVertex));
            if (dist <= VertexHoverRadius)
            {
                hoveredFleetVertex = nearestVertex;
            }
        }

        // Camps mobiles : sur le réseau routier, là où un avant-poste ne peut pas être bâti (voir
        // MobileCampController). Non proposé tant que la recherche MobileCampConstruction n'est pas
        // active (potentialMobileCampVertices est vide dans ce cas — voir GetPotentialMobileCampVerticesForPlayer).
        Vertex? hoveredMobileCampVertex = null;
        if (hoveredVertex == null && hoveredBeaconVertex == null && hoveredFleetVertex == null && potentialMobileCampVertices.Any(v => v.Equals(nearestVertex)))
        {
            var dist = SKPoint.Distance(islandPoint, _renderer.VertexToIslandPoint(nearestVertex));
            if (dist <= VertexHoverRadius)
            {
                hoveredMobileCampVertex = nearestVertex;
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
        if (hoveredVertex == null && hoveredBeaconVertex == null && hoveredFleetVertex == null && hoveredMobileCampVertex == null
            && hoveredEdge == null && hoveredEnemyProtectedEdge == null
            && hoveredCityVertex == null && hoveredEnemyCityVertex == null && hoveredOwnFleet == null && hoveredEnemyFleet == null
            && hoveredOwnMobileCamp == null && hoveredEnemyMobileCamp == null)
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
            hoveredEnemyCityVertex,
            buildableBeaconVertices,
            hoveredBeaconVertex,
            potentialFleetVertices,
            hoveredFleetVertex,
            hoveredOwnFleet,
            hoveredEnemyFleet,
            potentialMobileCampVertices,
            hoveredMobileCampVertex,
            hoveredOwnMobileCamp,
            hoveredEnemyMobileCamp
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

    private Vertex? GetHoveredOwnFleetVertex(SKPoint islandPoint)
    {
        if (_renderer == null) return null;

        var worldState = _gameControllerService.CurrentWorldState;
        if (worldState == null) return null;

        var playerIndex = worldState.PlayerCivilization.Index;
        Vertex? best = null;
        var bestDistance = float.MaxValue;

        foreach (var fleet in worldState.GetAllFleets())
        {
            if (fleet.CivilizationIndex != playerIndex) continue;

            if (worldState.Visibility.GetForZ(worldState.CurrentViewedLayer).TryGetValue(playerIndex, out var visibleMap) &&
                (fleet.Position.Z != visibleMap.Z || !fleet.Position.GetHexes().Any(visibleMap.HasTile)))
                continue;

            var pt = _renderer.VertexToIslandPoint(fleet.Position);
            var dist = SKPoint.Distance(islandPoint, pt);
            if (dist < bestDistance) { bestDistance = dist; best = fleet.Position; }
        }

        return bestDistance <= 12f ? best : null;
    }

    private Vertex? GetHoveredEnemyFleetVertex(SKPoint islandPoint)
    {
        if (_renderer == null) return null;

        var worldState = _gameControllerService.CurrentWorldState;
        if (worldState == null) return null;

        var playerIndex = worldState.PlayerCivilization.Index;
        Vertex? best = null;
        var bestDistance = float.MaxValue;

        if (!worldState.Visibility.GetForZ(worldState.CurrentViewedLayer).TryGetValue(playerIndex, out var visibleMap))
            return null;

        foreach (var fleet in worldState.GetAllFleets())
        {
            if (fleet.CivilizationIndex == playerIndex) continue;
            if (fleet.Position.Z != visibleMap.Z || !fleet.Position.GetHexes().Any(visibleMap.HasTile)) continue;

            var pt = _renderer.VertexToIslandPoint(fleet.Position);
            var dist = SKPoint.Distance(islandPoint, pt);
            if (dist < bestDistance) { bestDistance = dist; best = fleet.Position; }
        }

        return bestDistance <= 12f ? best : null;
    }

    private Vertex? GetHoveredOwnMobileCampVertex(SKPoint islandPoint)
    {
        if (_renderer == null) return null;

        var worldState = _gameControllerService.CurrentWorldState;
        if (worldState == null) return null;

        var playerIndex = worldState.PlayerCivilization.Index;
        Vertex? best = null;
        var bestDistance = float.MaxValue;

        foreach (var camp in worldState.GetAllMobileCamps())
        {
            if (camp.CivilizationIndex != playerIndex) continue;

            if (worldState.Visibility.GetForZ(worldState.CurrentViewedLayer).TryGetValue(playerIndex, out var visibleMap) &&
                (camp.Position.Z != visibleMap.Z || !camp.Position.GetHexes().Any(visibleMap.HasTile)))
                continue;

            var pt = _renderer.VertexToIslandPoint(camp.Position);
            var dist = SKPoint.Distance(islandPoint, pt);
            if (dist < bestDistance) { bestDistance = dist; best = camp.Position; }
        }

        return bestDistance <= 12f ? best : null;
    }

    private Vertex? GetHoveredEnemyMobileCampVertex(SKPoint islandPoint)
    {
        if (_renderer == null) return null;

        var worldState = _gameControllerService.CurrentWorldState;
        if (worldState == null) return null;

        var playerIndex = worldState.PlayerCivilization.Index;
        Vertex? best = null;
        var bestDistance = float.MaxValue;

        if (!worldState.Visibility.GetForZ(worldState.CurrentViewedLayer).TryGetValue(playerIndex, out var visibleMap))
            return null;

        foreach (var camp in worldState.GetAllMobileCamps())
        {
            if (camp.CivilizationIndex == playerIndex) continue;
            if (camp.Position.Z != visibleMap.Z || !camp.Position.GetHexes().Any(visibleMap.HasTile)) continue;

            var pt = _renderer.VertexToIslandPoint(camp.Position);
            var dist = SKPoint.Distance(islandPoint, pt);
            if (dist < bestDistance) { bestDistance = dist; best = camp.Position; }
        }

        return bestDistance <= 12f ? best : null;
    }

    private void SetSelectedCity(Vertex selectedCityVertex)
    {
        _monumentService?.ClearSelectedInvestable();
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
        _cachedBuildableVertices = null;
        _cachedBuildableBeaconVertices = null;
        _cachedPotentialFleetVertices = null;
        _cachedPotentialMobileCampVertices = null;
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
    Vertex? HoveredEnemyCityVertex,
    IReadOnlyList<Vertex> BuildableBeaconVertices,
    Vertex? HoveredBeaconVertex,
    IReadOnlyList<Vertex> BuildableFleetVertices,
    Vertex? HoveredFleetVertex,
    Vertex? HoveredOwnFleetVertex,
    Vertex? HoveredEnemyFleetVertex,
    IReadOnlyList<Vertex> BuildableMobileCampVertices,
    Vertex? HoveredMobileCampVertex,
    Vertex? HoveredOwnMobileCampVertex,
    Vertex? HoveredEnemyMobileCampVertex)
{
    public static ConstructionHoverState Empty =>
        new(Array.Empty<Vertex>(), Array.Empty<Edge>(), null, null, null, null, null, null, null, Array.Empty<Vertex>(), null, Array.Empty<Vertex>(), null, null, null, Array.Empty<Vertex>(), null, null, null);
}

