using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Civilization;

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
    public WorldState? CurrentWorldState => _controller.CurrentMainState?.CurrentWorldState;
    /// <summary>
    /// Instance unique pour toute la durée de vie de l'écran de jeu. Elle ne dépend que du
    /// MainGameController (elle relit le WorldState courant à chaque accès), donc la recréer à
    /// chaque changement de monde ne servait qu'à remettre <c>SelectedCity</c> à null — au prix
    /// d'un dédoublement : les renderers qui l'ont reçue en constructeur (SelectedCityPanelRenderer,
    /// ConstructionInteractionService, SettingsMenu) gardaient l'ancienne instance et sa sélection
    /// périmée, pendant que ceux qui passent par cette propriété voyaient la nouvelle. Chaque
    /// recréation laissait de plus l'ancienne abonnée à CityBuilderController.OnCityDestroyed.
    /// Les changements de monde appellent maintenant <see cref="Services.CityBuildingService.ClearSelectedCity"/>.
    /// </summary>
    public CityBuildingService CityBuildingService => _cityBuildingService;

    /// <summary>
    /// Gets the player's civilization.
    /// </summary>
    public SettlersOfIdlestan.Model.Civilization.Civilization? PlayerCivilization => _controller.PlayerCivilization;

    public int? PlayerCivilizationIndex => _controller.PlayerCivilization?.Index;

    public MainGameController MainGameController => _controller;

    /// <summary>
    /// Saut de temps en cours, s'il y en a un. C'est la boucle de jeu (<c>GameScreen.Tick</c>) qui
    /// le fait avancer ; les renderers ne font que le demander.
    /// </summary>
    public TimeJumpService TimeJump { get; } = new();

    public GameControllerService()
    {
        _controller = new MainGameController();
        _cityBuildingService = new CityBuildingService(_controller);
    }

    /// <summary>
    /// Demande un saut de <paramref name="ticks"/> ticks preleves sur la banque hors-ligne. La
    /// simulation est etalee sur les ticks suivants, derriere une popup de progression : aucun
    /// appelant ne doit simuler un gros saut lui-meme, cela figerait la fenetre.
    /// </summary>
    /// <param name="reasonKey">Cle de localisation du motif, affichee dans la popup.</param>
    public bool RequestTimeJump(long ticks, string reasonKey)
    {
        var clock = _controller.CurrentMainState?.Clock;
        return clock != null && TimeJump.Request(clock, ticks, reasonKey);
    }

    /// <summary>
    /// Initialise un nouveau jeu avec une île générée.
    /// Délègue à MainGameController pour respecter l'architecture.
    /// </summary>
    public void InitializeNewGame()
    {
        _controller.CreateNewGame();
        _cityBuildingService.ClearSelectedCity();
    }

    internal void ImportMainState(string autoJson)
    {
        MainGameController.ImportMainState(autoJson);
        _cityBuildingService.ClearSelectedCity();
    }

    /// <summary>
    /// Met à jour l'état du jeu pour le frame actuel.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (_controller.CurrentMainState == null)
            return;

        _controller.CurrentMainState.Clock.Advance(DateTimeOffset.UtcNow);
    }

    public void AddOfflineSeconds(double seconds)
    {
        if (_controller.CurrentMainState == null || seconds <= 0) return;
        _controller.CurrentMainState.Clock.OfflineBankTicks += (long)(seconds * 100);
    }

    public void PerformPrestige(bool corrupted = false)
    {
        _controller.PerformPrestige(corrupted);
    }

    public void PerformPrestigeAndRestartCurrentIsland(bool corrupted = false)
    {
        _controller.PerformPrestigeAndRestartCurrentIsland(corrupted);
    }

    public void RestartIsland()
    {
        _controller.RestartIsland();
        _cityBuildingService.ClearSelectedCity();
    }

    public void PerformAscension()
    {
        _controller.PerformAscension();
        _cityBuildingService.ClearSelectedCity();
    }

    /// <summary>Ascension avec choix de la race du prochain cycle (voir AscensionController.GetSelectableRaces).</summary>
    public void PerformAscension(SettlersOfIdlestan.Model.Races.RaceId chosenRace)
    {
        _controller.PerformAscension(chosenRace);
        _cityBuildingService.ClearSelectedCity();
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

    public City? TryBuildCityForPlayer(Vertex vertex)
    {
        var playerIndex = PlayerCivilizationIndex
            ?? throw new InvalidOperationException("La civilisation du joueur n'est pas disponible.");

        return _controller.CityBuilderController.BuildCity(playerIndex, vertex);
    }

    public List<Edge> GetEnemyProtectedRoadEdgesForPlayer()
    {
        var playerIndex = PlayerCivilizationIndex
            ?? throw new InvalidOperationException("La civilisation du joueur n'est pas disponible.");

        return _controller.RoadController.GetEnemyProtectedRoadEdges(playerIndex);
    }

    public Road? TryBuildRoadForPlayer(Edge edge)
    {
        var playerIndex = PlayerCivilizationIndex
            ?? throw new InvalidOperationException("La civilisation du joueur n'est pas disponible.");

        return _controller.RoadController.BuildRoad(playerIndex, edge);
    }

    public List<Vertex> GetBuildableMaritimeBeaconVerticesForPlayer()
    {
        var playerIndex = PlayerCivilizationIndex
            ?? throw new InvalidOperationException("La civilisation du joueur n'est pas disponible.");

        return _controller.MaritimeBeaconController.GetBuildableVertices(playerIndex);
    }

    public MaritimeBeacon? TryBuildMaritimeBeaconForPlayer(Vertex vertex)
    {
        var playerIndex = PlayerCivilizationIndex
            ?? throw new InvalidOperationException("La civilisation du joueur n'est pas disponible.");

        return _controller.MaritimeBeaconController.BuildMaritimeBeacon(playerIndex, vertex);
    }

    public bool IsWarFleetUnlockedForPlayer()
    {
        var civ = PlayerCivilization;
        return civ != null && _controller.WarFleetController.IsWarFleetUnlocked(civ);
    }

    public List<Vertex> GetPotentialWarFleetVerticesForPlayer()
    {
        var playerIndex = PlayerCivilizationIndex
            ?? throw new InvalidOperationException("La civilisation du joueur n'est pas disponible.");

        return _controller.WarFleetController.GetPotentialVertices(playerIndex);
    }

    public WarFleet? TryBuildWarFleetForPlayer(Vertex vertex)
    {
        var playerIndex = PlayerCivilizationIndex
            ?? throw new InvalidOperationException("La civilisation du joueur n'est pas disponible.");

        return _controller.WarFleetController.BuildWarFleet(playerIndex, vertex);
    }

    public bool IsMobileCampUnlockedForPlayer()
    {
        var civ = PlayerCivilization;
        return civ != null && _controller.MobileCampController.IsMobileCampUnlocked(civ);
    }

    public List<Vertex> GetPotentialMobileCampVerticesForPlayer()
    {
        if (!IsMobileCampUnlockedForPlayer())
            return new List<Vertex>();

        var playerIndex = PlayerCivilizationIndex
            ?? throw new InvalidOperationException("La civilisation du joueur n'est pas disponible.");

        return _controller.MobileCampController.GetPotentialVertices(playerIndex);
    }

    public MobileCamp? TryBuildMobileCampForPlayer(Vertex vertex)
    {
        var playerIndex = PlayerCivilizationIndex
            ?? throw new InvalidOperationException("La civilisation du joueur n'est pas disponible.");

        return _controller.MobileCampController.BuildMobileCamp(playerIndex, vertex);
    }
}
