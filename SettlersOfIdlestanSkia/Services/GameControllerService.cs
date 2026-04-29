using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Controller;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Service qui encapsule MainGameController pour la couche View.
/// La View ne crée/modifie jamais le Model directement - elle le fait via ce service.
/// </summary>
public class GameControllerService
{
    private readonly MainGameController _controller;

    public MainGameState? CurrentGameState => _controller.CurrentMainState;

    /// <summary>
    /// Gets the player's civilization.
    /// </summary>
    public SettlersOfIdlestan.Model.Civilization.Civilization? PlayerCivilization => _controller.PlayerCivilization;

    public int? PlayerCivilizationIndex => _controller.PlayerCivilization?.Index;

    public GameControllerService()
    {
        _controller = new MainGameController();
    }

    /// <summary>
    /// Initialise un nouveau jeu avec une île générée.
    /// Délègue à MainGameController pour respecter l'architecture.
    /// </summary>
    public MainGameState? InitializeNewGame()
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
        var gameState = _controller.CreateNewGame(tileData, civilizationCount: 1);

        if (gameState != null)
        {
            gameState.Clock.Start();
        }

        return gameState;
    }

    /// <summary>
    /// Charge un jeu existant.
    /// </summary>
    public void LoadGame(MainGameState gameState)
    {
        if (gameState == null)
            throw new ArgumentNullException(nameof(gameState));

        _controller.SetGame(gameState);
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

    /// <summary>
    /// Obtient le GameState actuel ou initialise un nouveau jeu.
    /// </summary>
    public MainGameState GetOrCreateGameState()
    {
        if (_controller.CurrentMainState == null)
        {
            var gameState = InitializeNewGame();
            if (gameState == null)
                throw new InvalidOperationException("Impossible de créer un nouveau jeu.");
            return gameState;
        }

        return _controller.CurrentMainState;
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
