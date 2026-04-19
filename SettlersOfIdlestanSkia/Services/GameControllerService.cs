using SettlersOfIdlestan.Model.Game;
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
            (TerrainType.Forest, 5),
            (TerrainType.Hill, 5),
            (TerrainType.Pasture, 5),
            (TerrainType.Field, 5),
            (TerrainType.Mountain, 5),
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
}
