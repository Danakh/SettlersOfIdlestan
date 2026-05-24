using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Services;
using System;
using System.Collections.Generic;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// Controls creation and management of the main game state.
    /// </summary>
    public class MainGameController
    {
        // Controllers created and exposed as read-only properties
        public RoadController RoadController { get; private set; }
        public HarvestController HarvestController { get; private set; }
        public TradeController TradeController { get; private set; }
        public BuildingController BuildingController { get; private set; }
        public CityBuilderController CityBuilderController { get; private set; }
        public PrestigeController PrestigeController { get; private set; }
        public PrestigeMapController PrestigeMapController { get; private set; }
        public GameClock? Clock { get; private set; }
        // Holds the currently loaded main game state when created or imported
        public SettlersOfIdlestan.Model.Game.MainGameState? CurrentMainState { get; private set; }
        public AtlasController AtlasController { get; private set; }

        /// <summary>
        /// Gets the player's civilization (always at index 0).
        /// </summary>
        public SettlersOfIdlestan.Model.Civilization.Civilization? PlayerCivilization 
            => CurrentMainState?.CurrentIslandState?.PlayerCivilization;

        public MainGameController()
        {
            // Create controllers with null state; Initialize will be called with the real state when available
            RoadController = new RoadController();
            HarvestController = new HarvestController();
            TradeController = new TradeController();
            BuildingController = new BuildingController();
            CityBuilderController = new CityBuilderController();
            AtlasController = new AtlasController();
            PrestigeController = new PrestigeController();
            PrestigeMapController = new PrestigeMapController();
        }

        /// <summary>
        /// Export the current MainGameState as a JSON string. Uses available JSON converters for island map types.
        /// </summary>
        public string ExportMainState()
        {
            if (CurrentMainState == null) throw new InvalidOperationException("No main state available to export.");

            CurrentMainState.Clock.LastSaveTime = DateTimeOffset.UtcNow;
            return System.Text.Json.JsonSerializer.Serialize(CurrentMainState, SerializationService.SerializationOptions());
        }

        /// <summary>
        /// Import a MainGameState from JSON and wire controllers to operate on it.
        /// Returns the deserialized MainGameState.
        /// </summary>
        public SettlersOfIdlestan.Model.Game.MainGameState ImportMainState(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("json cannot be empty", nameof(json));

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new SettlersOfIdlestan.Model.HexGrid.HexCoordJsonConverter());
            options.Converters.Add(new SettlersOfIdlestan.Model.HexGrid.EdgeJsonConverter());
            // ensure Building polymorphic types are deserialized
            options.Converters.Add(new SettlersOfIdlestan.Model.Buildings.BuildingJsonConverter());
            options.Converters.Add(new SettlersOfIdlestan.Model.IslandMap.IslandMapJsonConverter());
            // ensure Vertex (city positions) are properly deserialized when importing
            options.Converters.Add(new SettlersOfIdlestan.Model.HexGrid.VertexJsonConverter());

            var mainState = System.Text.Json.JsonSerializer.Deserialize<SettlersOfIdlestan.Model.Game.MainGameState>(json, options)
                            ?? throw new InvalidOperationException("Failed to deserialize MainGameState.");

            SetGameFromSave(mainState);

            return mainState;
        }

        /// <summary>
        /// Creates a new MainGameState by generating a new island using the island generator.
        /// Returns null if island generation fails.
        /// </summary>
        public MainGameState? CreateNewGame(IslandParameters parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            if (parameters.CivilizationCount <= 0) throw new ArgumentException("civilizationCount must be >= 1", nameof(parameters.CivilizationCount));

            var civs = new List<Civilization>();
            for (int i = 0; i < parameters.CivilizationCount; i++)
            {
                var civ = new Civilization { Index = i };
                civs.Add(civ);
            }
            // Create a main state early so we can use its PRNG for deterministic generation
            var mainState = new MainGameState();

            // Use a generator wired with the main state's PRNG to ensure reproducible maps
            var generator = new Generator.IslandMapGenerator(mainState.PRNG);
            var map = generator.GenerateIsland(parameters.TileData, civs);
            if (map is null) return null;

            var islandState = new IslandState(map, civs, parameters.IslandID);
            var prestigeState = new PrestigeState(islandState);
            var godState = new GodState(prestigeState);

            // populate the main state with the created sub-states
            mainState.GodState = godState;

            SetGame(mainState);
            PrestigeMapController.ApplyPrestigeToNewGame(islandState, mainState.PrestigeState);
            return mainState;
        }

        public void PerformPrestige()
        {
            if (CurrentMainState == null)
                throw new InvalidOperationException("No main state available.");

            var nextIslandId = AtlasController.GetNextIslandID(CurrentMainState);
            var parameters = AtlasController.GetIslandParameters(nextIslandId);
            PrestigeController.PerformPrestige(CurrentMainState, parameters);
            InitializeControllersForCurrentIsland();
            PrestigeMapController.ApplyPrestigeToNewGame(CurrentMainState.CurrentIslandState!, CurrentMainState.PrestigeState);
        }

        public MainGameState? CreateNewGame()
        {
            int islandId = AtlasController.GetFirstIslandID();
            var parameters = AtlasController.GetIslandParameters(islandId);
            return CreateNewGame(parameters);
        }

        /// <summary>
        /// Uses a already created game.
        /// </summary>
        public void SetGame(MainGameState mainGame)
        {
            CurrentMainState = mainGame;
            Clock = mainGame.Clock;
            Clock.Start();

            InitializeControllersForCurrentIsland();
        }

        /// <summary>
        /// Uses a saved game and credits offline time into the bank.
        /// </summary>
        public void SetGameFromSave(MainGameState mainGame)
        {
            CurrentMainState = mainGame;
            Clock = mainGame.Clock;
            Clock.ResumeAfterOffline(DateTimeOffset.UtcNow);
            Clock.Start();

            InitializeControllersForCurrentIsland();
        }

        private void InitializeControllersForCurrentIsland()
        {
            var islandState = CurrentMainState?.CurrentIslandState;

            if (islandState != null)
            {
                islandState.RecalculateVisibleIslandMaps();

                // Initialize controllers to operate on the real island state and clock
                RoadController.Initialize(islandState);
                HarvestController.Initialize(islandState, Clock);
                TradeController.Initialize(islandState);
                BuildingController.Initialize(islandState);
                CityBuilderController.Initialize(islandState);
                PrestigeController.Initialize(islandState.PlayerCivilization);
            }
        }
    }
}
