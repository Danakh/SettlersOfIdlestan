using SettlersOfIdlestan.Controller.Achievements;
using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Controller.Tasks;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige;
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
        public MonsterFeatureController MonsterFeatureController { get; private set; }
        /// <summary>Alias de compatibilité — utiliser MonsterFeatureController.</summary>
        public MonsterFeatureController BanditController => MonsterFeatureController;
        public MilitaryController MilitaryController { get; private set; }
        public WonderController WonderController { get; private set; }
        public DeepestMineController DeepestMineController { get; private set; }
        public CorruptionSpireController CorruptionSpireController { get; private set; }
        public Magic.MagicController MagicController { get; private set; }
        public AscensionController AscensionController { get; private set; }
        public NpcGameController NpcGameController { get; private set; }
        public GameClock? Clock { get; private set; }
        // Holds the currently loaded main game state when created or imported
        public SettlersOfIdlestan.Model.Game.MainGameState? CurrentMainState { get; private set; }
        private PrestigeModifierProvider? _prestigeModifierProvider;
        public AtlasController AtlasController { get; private set; }
        public TaskRecordController TaskRecordController { get; private set; }
        public AchievementController AchievementController { get; private set; }
        public AutoExtendController AutoExtendController { get; private set; }

        /// <summary>
        /// Statistiques cumulatives à vie (cross-sauvegarde) — chargées/sauvegardées par la couche
        /// hôte (Skia) indépendamment de MainGameState, pour survivre à "Nouvelle partie".
        /// </summary>
        public PlayerLifetimeStats LifetimeStats { get; set; } = new();

        /// <summary>
        /// Gets the player's civilization (always at index 0).
        /// </summary>
        public SettlersOfIdlestan.Model.Civilization.Civilization? PlayerCivilization 
            => CurrentMainState?.CurrentWorldState?.PlayerCivilization;

        private readonly SaveController _saveController = new();

        public MainGameController()
        {
            // Initialize() sera appelé avec le vrai état plus tard
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
            MonsterFeatureController = new MonsterFeatureController();
            MilitaryController = new MilitaryController();
            WonderController = new WonderController();
            DeepestMineController = new DeepestMineController();
            CorruptionSpireController = new CorruptionSpireController();
            MagicController = new Magic.MagicController();
            AscensionController = new AscensionController();
            TaskRecordController = new TaskRecordController();
            AchievementController = new AchievementController();
            AchievementController.Connect(TaskRecordController);
            AutoExtendController = new AutoExtendController();
            NpcGameController = new NpcGameController();
        }

        /// <summary>
        /// Exporte le MainGameState courant via le SaveController (JSON → Base64 → AES chiffré).
        /// </summary>
        public string ExportMainState()
        {
            if (CurrentMainState == null) throw new InvalidOperationException("No main state available to export.");
            return _saveController.Export(CurrentMainState);
        }

        /// <summary>
        /// Importe un MainGameState depuis une sauvegarde chiffrée (ou JSON brut pour les anciennes sauvegardes).
        /// Retourne le MainGameState désérialisé et connecte les contrôleurs.
        /// </summary>
        public SettlersOfIdlestan.Model.Game.MainGameState ImportMainState(string data)
        {
            if (string.IsNullOrWhiteSpace(data)) throw new ArgumentException("data cannot be empty", nameof(data));
            var mainState = _saveController.Import(data);
            SetGameFromSave(mainState);
            return mainState;
        }

        /// <summary>
        /// Creates a new MainGameState by generating a new island using the island generator.
        /// Returns null if island generation fails.
        /// Pass <paramref name="prngSeed"/> to get a deterministic game (e.g. in tests).
        /// </summary>
        public MainGameState? CreateNewGame(IslandParameters parameters, int? prngSeed = null)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            var mainState = new MainGameState(prngSeed);

            var generator = new Generator.IslandMapGenerator(mainState.WorldPRNG);
            var WorldState = generator.GenerateWorldState(parameters, mainState.Clock.CurrentTick);
            if (WorldState is null) return null;

            var prestigeState = new PrestigeState(WorldState);
            var godState = new GodState(prestigeState);

            mainState.GodState = godState;

            SetGame(mainState);
            PrestigeMapController.ApplyPrestigeToNewGame(WorldState, mainState.PrestigeState);
            return mainState;
        }

        /// <summary>
        /// Transporte la civilisation du joueur dans une carte de débogage compacte (7 hexagones,
        /// 1 NPC Strong/Aggressive avec 1 seule ville), sans conditions de prestige.
        /// </summary>
        public void GoToDebugMap()
        {
            if (CurrentMainState == null) return;

            var parameters = Generator.DebugMapGenerator.CreateParameters();
            var generator = new Generator.DebugMapGenerator(CurrentMainState.WorldPRNG);
            var nextWorldState = generator.GenerateWorldState(
                parameters,
                CurrentMainState.Clock.CurrentTick,
                startTick: CurrentMainState.Clock.CurrentTick)
                ?? throw new InvalidOperationException("Failed to generate debug map.");

            CurrentMainState.PrestigeState!.WorldState = nextWorldState;
            InitializeControllersForCurrentIsland();
            PrestigeMapController.ApplyPrestigeToNewGame(nextWorldState, CurrentMainState.PrestigeState);
        }

        public void RestartIsland()
        {
            if (CurrentMainState?.PrestigeState == null) return;

            var worldId = CurrentMainState.CurrentWorldState?.WorldId ?? AtlasController.GetFirstWorldId();
            var parameters = AtlasController.GetIslandParameters(worldId);

            CurrentMainState.PrestigeState.WorldState = null;
            var generator = new Generator.IslandMapGenerator(CurrentMainState.WorldPRNG);
            var newWorldState = generator.GenerateWorldState(
                parameters,
                CurrentMainState.Clock.CurrentTick,
                startTick: CurrentMainState.Clock.CurrentTick,
                surfaceCorruptionLevel: CurrentMainState.PrestigeState.SurfaceCorruptionLevel)
                ?? throw new InvalidOperationException("Failed to restart island.");

            CurrentMainState.PrestigeState.WorldState = newWorldState;
            InitializeControllersForCurrentIsland();
            PrestigeMapController.ApplyPrestigeToNewGame(newWorldState, CurrentMainState.PrestigeState);
        }

        public void PerformPrestige() => PerformPrestige(corrupted: false);

        public void PerformPrestige(bool corrupted)
        {
            if (CurrentMainState == null)
                throw new InvalidOperationException("No main state available.");

            var nextIslandId = AtlasController.GetNextWorldId(CurrentMainState);
            var parameters = AtlasController.GetIslandParameters(nextIslandId);
            TaskRecordController.RecordPrestige(PrestigeController.CalculatePrestigePoints());
            PrestigeController.PerformPrestige(CurrentMainState, parameters, corrupted);
            InitializeControllersForCurrentIsland();
            PrestigeMapController.ApplyPrestigeToNewGame(CurrentMainState.CurrentWorldState!, CurrentMainState.PrestigeState);
        }

        /// <summary>
        /// Comme PerformPrestige, mais régénère la même île (mode démo : rester sur l'île 3).
        /// </summary>
        public void PerformPrestigeAndRestartCurrentIsland() => PerformPrestigeAndRestartCurrentIsland(corrupted: false);

        public void PerformPrestigeAndRestartCurrentIsland(bool corrupted)
        {
            if (CurrentMainState == null)
                throw new InvalidOperationException("No main state available.");

            var currentIslandId = CurrentMainState.CurrentWorldState?.WorldId ?? AtlasController.GetFirstWorldId();
            var parameters = AtlasController.GetIslandParameters(currentIslandId);
            TaskRecordController.RecordPrestige(PrestigeController.CalculatePrestigePoints());
            PrestigeController.PerformPrestige(CurrentMainState, parameters, corrupted);
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
            if (Clock.WasPausedAtSave)
                Clock.Pause();
            else
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

                WorldState.Visibility.Recalculate();

                // Initialisé avant SetupModifierAggregators() : ce contrôleur sert lui-même de
                // IModifierProvider et doit avoir purgé ses anciens abonnés avant d'être ré-enregistré.
                AscensionController.Initialize(WorldState, Clock, CurrentMainState!.PRNG, HarvestController, CurrentMainState!.GodState.AscensionState);

                SetupModifierAggregators();

                AutoExtendController.Initialize(WorldState, CurrentMainState!.WorldPRNG, Clock, CurrentMainState?.PrestigeState);

                // Ordre d'initialisation contraint — ne pas modifier sans vérifier les dépendances :
                // 1. RoadController  — nettoyage des routes après la destruction d'une ville
                // 2. CityBuilderController — requis par MilitaryController/MonsterFeatureController
                //    (point d'entrée unique de destruction de ville, cf. CityBuilderController.DestroyCity)
                // 3. FeatureController — doit découvrir les features avant tout combat/mouvement
                // 4. MilitaryController — doit s'abonner à l'horloge AVANT MonsterFeatureController
                //    pour que le combat soit résolu avant le déplacement des monstres
                // 5. MonsterFeatureController — dépend de CityBuilderController
                // 6. TradeController — requis par HarvestController (auto-vente en cas de débordement)
                // 7. HarvestController — dépend de TradeController et MonsterFeatureController
                // 8. Reste des controllers (BuildingController, etc.) — indépendants
                RoadController.Initialize(WorldState, Clock, CurrentMainState!.PRNG);
                CityBuilderController.Initialize(WorldState, Clock, CurrentMainState!.PRNG);
                FeatureController.Initialize(WorldState, Clock);
                MilitaryController.Initialize(WorldState, Clock, CityBuilderController, CurrentMainState!.PRNG);
                MonsterFeatureController.Initialize(WorldState, Clock, CurrentMainState!.PRNG, CityBuilderController);
                TradeController.Initialize(WorldState);
                HarvestController.Initialize(WorldState, Clock, TradeController, MonsterFeatureController, CurrentMainState!.PRNG);
                BuildingController.Initialize(WorldState, Clock);
                AtlasController.Initialize(CurrentMainState!.WorldPRNG);
                PrestigeController.Initialize(WorldState.PlayerCivilization, WorldState, Clock, CurrentMainState?.PrestigeState);
                WonderController.Initialize(WorldState, Clock);
                DeepestMineController.Initialize(WorldState, Clock);
                CorruptionSpireController.Initialize(WorldState, Clock);
                MagicController.Initialize(WorldState, Clock, CurrentMainState!.PRNG, CityBuilderController, BuildingController, HarvestController);
                ResearchController.Initialize(WorldState, Clock, CurrentMainState?.PrestigeState, CurrentMainState?.Settings);
                NpcGameController.Initialize(WorldState, Clock, MilitaryController, this);

                // Invalide le cache de production dès qu'un bâtiment est construit/amélioré ou une ville créée
                MagicController.OnRitualsChanged -= OnRitualsChangedInvalidateHarvestCache;
                MagicController.OnRitualsChanged += OnRitualsChangedInvalidateHarvestCache;
                BuildingController.OnBuildingBuilt -= OnBuildingChangedInvalidateHarvestCache;
                CityBuilderController.OnCityBuilt -= OnCityBuiltInvalidateHarvestCache;
                CityBuilderController.OnCityDestroyed -= OnCityDestroyedHandler;
                RoadController.OnRoadBuilt -= OnRoadBuiltExtendMap;
                RoadController.OnAutoRoadBuilt -= OnRoadBuiltExtendMap;
                BuildingController.OnBuildingBuilt += OnBuildingChangedInvalidateHarvestCache;
                CityBuilderController.OnCityBuilt += OnCityBuiltInvalidateHarvestCache;
                CityBuilderController.OnCityDestroyed += OnCityDestroyedHandler;
                RoadController.OnRoadBuilt += OnRoadBuiltExtendMap;
                RoadController.OnAutoRoadBuilt += OnRoadBuiltExtendMap;
                FeatureController.OnFeatureDiscovered -= OnFeatureDiscovered;
                FeatureController.OnFeatureDiscovered += OnFeatureDiscovered;
                prestigeState?.TechnologyTree.RebuildModifiers();

                var gameRecord = CurrentMainState!.GameRecord;
                TaskRecordController.Initialize(gameRecord, WorldState.RunRecord, WorldState,
                    BuildingController, RoadController, CityBuilderController,
                    PrestigeMapController, ResearchController, MilitaryController, HarvestController,
                    TradeController, WonderController, CorruptionSpireController,
                    CurrentMainState!.GodState, LifetimeStats);
            }
        }

        private void OnFeatureDiscovered(object? sender, IslandFeature feature)
        {
            if (feature is Rats && CurrentMainState != null && !CurrentMainState.GameRecord.HasEncounteredRats)
                CurrentMainState.GameRecord.HasEncounteredRats = true;
        }

        private void OnRoadBuiltExtendMap(object? sender, RoadAutoBuiltEventArgs e)
            => AutoExtendController.TryExtendMapAfterRoad(e.CivilizationIndex, e.RoadPosition);

        private void OnBuildingChangedInvalidateHarvestCache(object? sender, BuildingBuiltEventArgs e)
            => HarvestController.InvalidateProductionCache();

        private void OnRitualsChangedInvalidateHarvestCache(object? sender, EventArgs e)
            => HarvestController.InvalidateProductionCache();


        private void OnCityBuiltInvalidateHarvestCache(object? sender, OutpostAutoBuiltEventArgs e)
        {
            FeatureController.RefreshContestedTerritories();
            HarvestController.InvalidateProductionCache();
        }

        /// <summary>
        /// Single subscriber to CityBuilderController.OnCityDestroyed — fires for every destruction
        /// cause (military conquest or monster attack), so road cleanup, contested-territory refresh
        /// and the underworld check all happen consistently regardless of cause.
        /// </summary>
        private void OnCityDestroyedHandler(object? sender, CityDestroyedEventArgs e)
        {
            var worldState = CurrentMainState?.CurrentWorldState;
            var civ = worldState?.Civilizations.FirstOrDefault(c => c.Index == e.CivilizationIndex);
            if (civ != null)
                RoadController.OnCityDestroyed(civ, e.CityVertex);

            FeatureController.RefreshContestedTerritories();
            DeepestMineController.OnCityDestroyed(e.CityVertex, e.CivilizationIndex);
        }

        private void SetupModifierAggregators()
        {
            var prestigeState = CurrentMainState!.PrestigeState;
            var WorldState = prestigeState!.WorldState;

            var npcModifiers = NpcModifierSetMaker.Create(maxTechTier: 3, maxPrestigeDistance: 2);

            foreach (var civ in WorldState!.Civilizations.Where(c => c.IsNpc))
            {
                if (civ.NpcParameters?.ExtraModifiers is { Count: > 0 } extras)
                    civ.AddCustomAggregator(new StaticModifierProvider(extras));
                else
                    civ.AddCustomAggregator(npcModifiers);
            }

            _prestigeModifierProvider?.Dispose();
            _prestigeModifierProvider = new PrestigeModifierProvider(prestigeState, PrestigeMapController.DefaultMap);
            var playerCiv = WorldState.PlayerCivilization;
            playerCiv.AddCustomAggregator(_prestigeModifierProvider);
            playerCiv.AddCustomAggregator(AscensionController);
        }
    }
}
