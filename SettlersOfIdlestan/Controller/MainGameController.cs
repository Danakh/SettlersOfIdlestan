using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Controller.Tasks;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Services;
using System;
using System.Collections.Generic;
using System.Linq;

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
        public ResearchController ResearchController { get; private set; }
        public FeatureController FeatureController { get; private set; }
        public BanditController BanditController { get; private set; }
        public MilitaryController MilitaryController { get; private set; }
        public WonderController WonderController { get; private set; }
        public NpcGameController NpcGameController { get; private set; }
        public GameClock? Clock { get; private set; }
        // Holds the currently loaded main game state when created or imported
        public SettlersOfIdlestan.Model.Game.MainGameState? CurrentMainState { get; private set; }
        private PrestigeModifierProvider? _prestigeModifierProvider;
        public AtlasController AtlasController { get; private set; }
        public TaskRecordController TaskRecordController { get; private set; }

        /// <summary>
        /// Gets the player's civilization (always at index 0).
        /// </summary>
        public SettlersOfIdlestan.Model.Civilization.Civilization? PlayerCivilization 
            => CurrentMainState?.CurrentWorldState?.PlayerCivilization;

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
            ResearchController = new ResearchController();
            FeatureController = new FeatureController();
            BanditController = new BanditController();
            MilitaryController = new MilitaryController();
            WonderController = new WonderController();
            TaskRecordController = new TaskRecordController();
            NpcGameController = new NpcGameController();
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

            // Create a main state early so we can use its PRNG for deterministic generation
            var mainState = new MainGameState();

            var generator = new Generator.IslandMapGenerator(mainState.PRNG);
            var WorldState = generator.GenerateWorldState(parameters, mainState.Clock.CurrentTick);
            if (WorldState is null) return null;

            var prestigeState = new PrestigeState(WorldState);
            var godState = new GodState(prestigeState);

            // populate the main state with the created sub-states
            mainState.GodState = godState;

            SetGame(mainState);
            PrestigeMapController.ApplyPrestigeToNewGame(WorldState, mainState.PrestigeState);
            return mainState;
        }

        public void PerformPrestige()
        {
            if (CurrentMainState == null)
                throw new InvalidOperationException("No main state available.");

            var nextIslandId = AtlasController.GetNextWorldId(CurrentMainState);
            var parameters = AtlasController.GetIslandParameters(nextIslandId);
            TaskRecordController.RecordPrestige();
            PrestigeController.PerformPrestige(CurrentMainState, parameters);
            InitializeControllersForCurrentIsland();
            PrestigeMapController.ApplyPrestigeToNewGame(CurrentMainState.CurrentWorldState!, CurrentMainState.PrestigeState);
        }

        public MainGameState? CreateNewGame()
        {
            int WorldId = AtlasController.GetFirstWorldId();
            var parameters = AtlasController.GetIslandParameters(WorldId);
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
            var WorldState = CurrentMainState?.CurrentWorldState;

            if (WorldState != null)
            {
                // Bind the player's TechnologyTree to the persistent prestige tree so research
                // progress survives across islands. NPC civs keep their own ephemeral empty tree.
                var prestigeState = CurrentMainState?.PrestigeState;
                if (prestigeState != null)
                    WorldState.PlayerCivilization.TechnologyTree = prestigeState.TechnologyTree;

                WorldState.RecalculateVisibleIslandMaps();

                SetupModifierAggregators();

                // Initialize controllers to operate on the real island state and clock
                RoadController.Initialize(WorldState, Clock, CurrentMainState!.PRNG);
                // FeatureController discovers features before any combat or movement runs.
                // MilitaryController must subscribe before BanditController so combat resolves before movement.
                FeatureController.Initialize(WorldState, Clock);
                MilitaryController.Initialize(WorldState, Clock);
                BanditController.Initialize(WorldState, Clock, CurrentMainState!.PRNG);
                HarvestController.Initialize(WorldState, Clock, TradeController, BanditController, CurrentMainState!.PRNG);
                TradeController.Initialize(WorldState);
                BuildingController.Initialize(WorldState, Clock);
                CityBuilderController.Initialize(WorldState, Clock, CurrentMainState!.PRNG);
                PrestigeController.Initialize(WorldState.PlayerCivilization, WorldState, Clock);
                WonderController.Initialize(WorldState, Clock);
                ResearchController.Initialize(WorldState, Clock, CurrentMainState?.PrestigeState);
                NpcGameController.Initialize(WorldState, Clock, MilitaryController, this);

                // Invalide le cache de production dès qu'un bâtiment est construit/amélioré ou une ville créée
                BuildingController.OnBuildingBuilt -= OnBuildingChangedInvalidateHarvestCache;
                CityBuilderController.OnCityBuilt -= OnCityBuiltInvalidateHarvestCache;
                MilitaryController.CityDestroyed -= OnCityDestroyedRefreshContested;
                BuildingController.OnBuildingBuilt += OnBuildingChangedInvalidateHarvestCache;
                CityBuilderController.OnCityBuilt += OnCityBuiltInvalidateHarvestCache;
                MilitaryController.CityDestroyed += OnCityDestroyedRefreshContested;
                prestigeState?.TechnologyTree.RebuildModifiers();

                var gameRecord = CurrentMainState!.GameRecord;
                TaskRecordController.Initialize(gameRecord, WorldState.RunRecord, WorldState,
                    BuildingController, RoadController, CityBuilderController,
                    PrestigeMapController, ResearchController, MilitaryController, HarvestController,
                    TradeController);
            }
        }

        private void OnBuildingChangedInvalidateHarvestCache(object? sender, BuildingBuiltEventArgs e)
            => HarvestController.InvalidateProductionCache();

        private void OnCityBuiltInvalidateHarvestCache(object? sender, OutpostAutoBuiltEventArgs e)
        {
            FeatureController.RefreshContestedTerritories();
            HarvestController.InvalidateProductionCache();
        }

        private void OnCityDestroyedRefreshContested(object? sender, CityDestroyedEventArgs e)
            => FeatureController.RefreshContestedTerritories();

        private void SetupModifierAggregators()
        {
            var prestigeState = CurrentMainState!.PrestigeState;
            var WorldState = prestigeState!.WorldState;

            var npcModifiers = NpcModifierSetMaker.Create(maxTechTier: 3, maxPrestigeDistance: 2);

            foreach (var civ in WorldState!.Civilizations.Where(c => c.IsNpc))
                civ.SetupModifierAggregator(civ.TechnologyTree, npcModifiers, new UniqueBuildingsModifierProvider(civ));

            _prestigeModifierProvider?.Dispose();
            _prestigeModifierProvider = new PrestigeModifierProvider(prestigeState, PrestigeMapController.DefaultMap);
            var playerCiv = WorldState.PlayerCivilization;
            playerCiv.SetupModifierAggregator(
                playerCiv.TechnologyTree,
                _prestigeModifierProvider,
                new UniqueBuildingsModifierProvider(playerCiv));
        }
    }
}
