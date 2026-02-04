using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.PrestigeMap;
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
        public GameClock? Clock { get; private set; }
        // Holds the currently loaded main game state when created or imported
        public SettlersOfIdlestan.Model.Game.MainGameState? CurrentMainState { get; private set; }

        public MainGameController()
        {
            // create a default empty state for controllers; callers should replace with real state when available
            var emptyMap = new IslandMap(new List<SettlersOfIdlestan.Model.IslandMap.HexTile>());
            var emptyCivs = new List<Civilization>();
            var emptyState = new IslandState(emptyMap, emptyCivs);

            RoadController = new RoadController(emptyState);
            HarvestController = new HarvestController(emptyState, new GameClock());
            TradeController = new TradeController(emptyState);
            BuildingController = new BuildingController(emptyState);
            CityBuilderController = new CityBuilderController(emptyState);
        }

        /// <summary>
        /// Export the current MainGameState as a JSON string. Uses available JSON converters for island map types.
        /// </summary>
        public string ExportMainState()
        {
            if (CurrentMainState == null) throw new InvalidOperationException("No main state available to export.");

            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            };
            // register converters for hex coord and island map types
            options.Converters.Add(new SettlersOfIdlestan.Model.HexGrid.HexCoordJsonConverter());
            options.Converters.Add(new SettlersOfIdlestan.Model.HexGrid.EdgeJsonConverter());
            // ensure Building polymorphic types are serialized
            options.Converters.Add(new SettlersOfIdlestan.Model.Buildings.BuildingJsonConverter());
            options.Converters.Add(new SettlersOfIdlestan.Model.IslandMap.IslandMapJsonConverter());
            // ensure Vertex (city positions) are properly serialized when exporting
            options.Converters.Add(new SettlersOfIdlestan.Model.HexGrid.VertexJsonConverter());

            return System.Text.Json.JsonSerializer.Serialize(CurrentMainState, options);
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

            var islandState = mainState.CurrentIslandState ?? throw new InvalidOperationException("Imported state does not contain an island state.");

            // Recreate controllers to operate on the imported island state and clock
            RoadController = new RoadController(islandState);
            HarvestController = new HarvestController(islandState, mainState.Clock);
            TradeController = new TradeController(islandState);
            BuildingController = new BuildingController(islandState);
            CityBuilderController = new CityBuilderController(islandState);

            // expose clock and keep reference to main state
            Clock = mainState.Clock;
            CurrentMainState = mainState;

            return mainState;
        }

        /// <summary>
        /// Creates a new MainGameState by generating a new island using the island generator.
        /// Returns null if island generation fails.
        /// </summary>
        /// <param name="tileData">Tile data used by the island generator (terrain type and counts).</param>
        /// <param name="civilizationCount">Number of civilizations to create (player is index 0).</param>
        public MainGameState? CreateNewGame(IEnumerable<(TerrainType terrainType, int tileCount)> tileData, int civilizationCount)
        {
            if (civilizationCount <= 0) throw new ArgumentException("civilizationCount must be >= 1", nameof(civilizationCount));

            var civs = new List<Civilization>();
            for (int i = 0; i < civilizationCount; i++)
            {
                var civ = new Civilization { Index = i };
                civs.Add(civ);
            }
            // Create a main state early so we can use its PRNG for deterministic generation
            var mainState = new MainGameState();

            // Use a generator wired with the main state's PRNG to ensure reproducible maps
            var generator = new IslandMapGenerator(mainState.PRNG);
            var map = generator.GenerateIsland(tileData, civs);
            if (map is null) return null;

            var islandState = new IslandState(map, civs);
            var prestigeState = new PrestigeState(islandState);
            var godState = new GodState(prestigeState);
            var clock = mainState.Clock;

            // populate the main state with the created sub-states
            mainState.GodState = godState;
            mainState.Clock = clock;

            SetGame(mainState);
            return mainState;
        }


        /// <summary>
        /// Uses a already created game.
        /// </summary>
        public void SetGame(MainGameState mainGame)
        {
            // keep reference to the created main state for export/import
            CurrentMainState = mainGame;
            Clock = mainGame.Clock;

            var islandState = mainGame.CurrentIslandState;

            if (islandState is not null)
            {
                // Recreate controllers to operate on the real island state and clock
                RoadController = new RoadController(islandState);
                HarvestController = new HarvestController(islandState, mainGame.Clock);
                TradeController = new TradeController(islandState);
                BuildingController = new BuildingController(islandState);
                CityBuilderController = new CityBuilderController(islandState);
            }
        }
    }
}
