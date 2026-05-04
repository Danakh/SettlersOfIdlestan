using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.City;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestanSkia.Renderers;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Service qui encapsule MainGameController pour la couche View.
/// La View ne crée/modifie jamais le Model directement - elle le fait via ce service.
/// </summary>
public class GameControllerService
{
    private readonly MainGameController _controller;
    private readonly CityBuildingService _cityBuildingService;

    public MainGameState? CurrentGameState => _controller.CurrentMainState;
    public IslandState? CurrentIslandState => _controller.CurrentMainState?.CurrentIslandState;
    public CityBuildingService CityBuildingService => _cityBuildingService;

    /// <summary>
    /// Gets the player's civilization.
    /// </summary>
    public SettlersOfIdlestan.Model.Civilization.Civilization? PlayerCivilization => _controller.PlayerCivilization;

    public int? PlayerCivilizationIndex => _controller.PlayerCivilization?.Index;

    public MainGameController MainGameController => _controller;

    public GameControllerService()
    {
        _controller = new MainGameController();
        _cityBuildingService = new CityBuildingService(_controller);
    }

    /// <summary>
    /// Initialise un nouveau jeu avec une île générée.
    /// Délègue à MainGameController pour respecter l'architecture.
    /// </summary>
    public void InitializeNewGame()
    {
        // Configuration de l'île
        var tileData = new List<(TerrainType terrainType, int tileCount)>
        {
            (TerrainType.Forest, 4),
            (TerrainType.Hill, 4),
            (TerrainType.Pasture, 4),
            (TerrainType.Field, 4),
            (TerrainType.Mountain, 3),
        };

        // Crée un nouveau jeu via le controller
        _controller.CreateNewGame(tileData, civilizationCount: 1);
    }

    /// <summary>
    /// Met à jour l'état du jeu pour le frame actuel.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (_controller.CurrentMainState == null)
            return;

        _controller.CurrentMainState.Clock.Advance(TimeSpan.FromSeconds(deltaTime));
    }

    public List<Vertex> GetBuildableCityVerticesForPlayer()
    {
        var playerIndex = PlayerCivilizationIndex
            ?? throw new InvalidOperationException("La civilisation du joueur n'est pas disponible.");

        return _controller.CityBuilderController.GetBuildableVertices(playerIndex);
    }

    public List<Edge> GetBuildableRoadEdgesForPlayer()
    {
        var playerIndex = PlayerCivilizationIndex
            ?? throw new InvalidOperationException("La civilisation du joueur n'est pas disponible.");

        return _controller.RoadController
            .GetBuildableRoads(playerIndex)
            .Select(r => r.Position)
            .ToList();
    }

    public bool TryBuildCityForPlayer(Vertex vertex)
    {
        var playerIndex = PlayerCivilizationIndex
            ?? throw new InvalidOperationException("La civilisation du joueur n'est pas disponible.");

        try
        {
            _controller.CityBuilderController.BuildCity(playerIndex, vertex);
            // Réinstancie les controllers pour forcer la cohérence des caches.
            if (_controller.CurrentMainState != null)
            {
                _controller.SetGame(_controller.CurrentMainState);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool TryBuildRoadForPlayer(Edge edge)
    {
        var playerIndex = PlayerCivilizationIndex
            ?? throw new InvalidOperationException("La civilisation du joueur n'est pas disponible.");

        try
        {
            _controller.RoadController.BuildRoad(playerIndex, edge);
            if (_controller.CurrentMainState != null)
            {
                _controller.SetGame(_controller.CurrentMainState);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
