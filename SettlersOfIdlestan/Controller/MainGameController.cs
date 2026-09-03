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
using SettlersOfIdlestan.Model.Races;
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
        public MaritimeBeaconController MaritimeBeaconController { get; private set; }
        public WarFleetController WarFleetController { get; private set; }
        public MobileCampController MobileCampController { get; private set; }
        public PrestigeController PrestigeController { get; private set; }
        public PrestigeMapController PrestigeMapController { get; private set; }
        public ResearchController ResearchController { get; private set; }
        public FeatureController FeatureController { get; private set; }
        public MonsterFeatureController MonsterFeatureController { get; private set; }
        /// <summary>Alias de compatibilité — utiliser MonsterFeatureController.</summary>
        public MonsterFeatureController BanditController => MonsterFeatureController;
        public MilitaryController MilitaryController { get; private set; }
        public WonderController WonderController { get; private set; }
        public GreatLighthouseController GreatLighthouseController { get; private set; }
        public ObservatoryController ObservatoryController { get; private set; }
        public NecropolisController NecropolisController { get; private set; }
        public DeepestMineController DeepestMineController { get; private set; }
        public SurfaceBreachController SurfaceBreachController { get; private set; }
        public CorruptionSpireController CorruptionSpireController { get; private set; }
        public CorruptionController CorruptionController { get; private set; }
        public AbyssGateController AbyssGateController { get; private set; }
        public PandemoniumGateController PandemoniumGateController { get; private set; }
        public DivineBonesController DivineBonesController { get; private set; }
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
        public VolcanoController VolcanoController { get; private set; }
        public CivilizationHistoryController CivilizationHistoryController { get; private set; }
        public TradeHistoryController TradeHistoryController { get; private set; }

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
            MaritimeBeaconController = new MaritimeBeaconController();
            WarFleetController = new WarFleetController();
            MobileCampController = new MobileCampController();
            AtlasController = new AtlasController();
            PrestigeController = new PrestigeController();
            PrestigeMapController = new PrestigeMapController();
            ResearchController = new ResearchController();
            FeatureController = new FeatureController();
            MonsterFeatureController = new MonsterFeatureController();
            MilitaryController = new MilitaryController();
            WonderController = new WonderController();
            GreatLighthouseController = new GreatLighthouseController();
            ObservatoryController = new ObservatoryController();
            NecropolisController = new NecropolisController();
            DeepestMineController = new DeepestMineController();
            SurfaceBreachController = new SurfaceBreachController();
            CorruptionSpireController = new CorruptionSpireController();
            CorruptionController = new CorruptionController();
            AbyssGateController = new AbyssGateController();
            PandemoniumGateController = new PandemoniumGateController();
            DivineBonesController = new DivineBonesController();
            MagicController = new Magic.MagicController();
            AscensionController = new AscensionController();
            TaskRecordController = new TaskRecordController();
            AchievementController = new AchievementController();
            AchievementController.Connect(TaskRecordController);
            AutoExtendController = new AutoExtendController();
            VolcanoController = new VolcanoController();
            NpcGameController = new NpcGameController();
            CivilizationHistoryController = new CivilizationHistoryController();
            TradeHistoryController = new TradeHistoryController();
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
        /// JSON UTF-8 de l'état courant, non brouillé — voir <see cref="SaveController.SerializeUtf8"/>.
        /// C'est la forme qu'utilise la sauvegarde automatique : elle seule lit le modèle, le reste
        /// du travail (brouillage, écriture, cloud) se fait ensuite hors du thread de jeu.
        /// </summary>
        public ReadOnlyMemory<byte> SerializeMainStateUtf8()
        {
            if (CurrentMainState == null) throw new InvalidOperationException("No main state available to export.");
            return _saveController.SerializeUtf8(CurrentMainState);
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

            // Nouvelle île : l'état éphémère d'automatisation (cible de raid, Héraut de Guerre,
            // Vendetta) référencerait sinon des coordonnées de l'île abandonnée — voir
            // AutomationSettings.ResetIslandEphemeralState.
            CurrentMainState.GodState.AutomationSettings.ResetIslandEphemeralState();

            CurrentMainState.PrestigeState!.WorldState = nextWorldState;
            InitializeControllersForCurrentIsland();
            PrestigeMapController.ApplyPrestigeToNewGame(nextWorldState, CurrentMainState.PrestigeState);
        }

        public void RestartIsland()
        {
            if (CurrentMainState?.PrestigeState == null) return;

            var worldId = CurrentMainState.CurrentWorldState?.WorldId ?? AtlasController.GetFirstWorldId();
            var parameters = AtlasController.GetIslandParameters(worldId);

            // Compteurs d'usage des pouvoirs divins ciblés, remis à zéro par PrestigeController.
            // PerformPrestige à chaque changement d'île — un restart repart tout autant d'une île
            // vierge, ces compteurs doivent donc suivre (voir PrestigeState.FistOfGodUsesSinceLastPrestige
            // et consorts). Contrairement à un vrai Prestige, aucune monnaie n'est gagnée ni perdue ici
            // (PrestigePoints, essence divine, recherches, carte de prestige restent inchangés — voir
            // restart_island_line2/line3).
            CurrentMainState.PrestigeState.WalkOfGodUsesSinceLastPrestige = 0;
            CurrentMainState.PrestigeState.PresenceOfGodUsesSinceLastPrestige = 0;
            CurrentMainState.PrestigeState.FistOfGodUsesSinceLastPrestige = 0;

            CurrentMainState.PrestigeState.WorldState = null;
            var generator = new Generator.IslandMapGenerator(CurrentMainState.WorldPRNG);
            var newWorldState = generator.GenerateWorldState(
                parameters,
                CurrentMainState.Clock.CurrentTick,
                startTick: CurrentMainState.Clock.CurrentTick,
                surfaceCorruptionLevel: CurrentMainState.PrestigeState.SurfaceCorruptionLevel,
                tier: CurrentMainState.PrestigeState.Tier,
                race: RaceDefinitions.Get(CurrentMainState.GodState.AscensionState.SelectedRace))
                ?? throw new InvalidOperationException("Failed to restart island.");

            // Nouvelle île : l'état éphémère d'automatisation (cible de raid, Héraut de Guerre,
            // Vendetta) référencerait sinon des coordonnées de l'île abandonnée — voir
            // AutomationSettings.ResetIslandEphemeralState.
            CurrentMainState.GodState.AutomationSettings.ResetIslandEphemeralState();

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
            TaskRecordController.RecordPrestige(PrestigeController.CalculatePrestigePoints(), corrupted);
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
            TaskRecordController.RecordPrestige(PrestigeController.CalculatePrestigePoints(), corrupted);
            PrestigeController.PerformPrestige(CurrentMainState, parameters, corrupted);
            InitializeControllersForCurrentIsland();
            PrestigeMapController.ApplyPrestigeToNewGame(CurrentMainState.CurrentWorldState!, CurrentMainState.PrestigeState);
        }

        /// <summary>
        /// Convertit l'essence divine accumulée en points divins et repart de zéro (voir
        /// AscensionController.PerformAscension) : PrestigeState et l'île en cours sont remplacés,
        /// seuls GodState.GodPoints/AscensionState (pouvoirs débloqués) survivent.
        /// </summary>
        public void PerformAscension() => PerformAscension(AscensionController.SelectedRace);

        /// <summary>
        /// Comme <see cref="PerformAscension()"/>, en choisissant la race du prochain cycle
        /// (voir AscensionController.GetSelectableRaces — Humains tant que la première rangée de
        /// pouvoirs divins n'est pas complète).
        /// </summary>
        public void PerformAscension(RaceId chosenRace)
        {
            if (CurrentMainState == null)
                throw new InvalidOperationException("No main state available.");

            var worldId = AtlasController.GetAscensionStartingWorldId(CurrentMainState.GodState.AscensionState.AscensionsPerformed);
            var parameters = AtlasController.GetIslandParameters(worldId);
            TaskRecordController.RecordAscension(AscensionController.GetGodPointsGain(CurrentMainState.GodState));
            AscensionController.PerformAscension(CurrentMainState, parameters, chosenRace);
            InitializeControllersForCurrentIsland();
            PrestigeMapController.ApplyPrestigeToNewGame(CurrentMainState.CurrentWorldState!, CurrentMainState.PrestigeState);
        }

        /// <summary>
        /// Point d'entrée UI pour demander une Ascension : seule la phase 1 s'exécute immédiatement
        /// (conversion essence -> points, archivage du cycle, destruction de l'île et du PrestigeState
        /// en cours — voir AscensionController.RequestAscension) ; l'île suivante n'est créée qu'au
        /// choix de la race (voir <see cref="ConfirmAscensionRace"/>), le jeu restant en pause
        /// entretemps — même quand le choix de race n'est pas encore débloqué (Humains alors seule
        /// option proposée), pour que le joueur valide explicitement la race avant que l'île ne soit
        /// recréée.
        /// </summary>
        public void RequestAscension()
        {
            if (CurrentMainState == null)
                throw new InvalidOperationException("No main state available.");

            TaskRecordController.RecordAscension(AscensionController.GetGodPointsGain(CurrentMainState.GodState));
            AscensionController.RequestAscension(CurrentMainState);
            // PrestigeState/WorldState viennent d'être détruits : ce passage est un no-op (voir la
            // garde `if (WorldState != null)`), mais reste la façon normale de signaler la
            // transition — cohérent avec tous les autres points d'entrée qui changent d'île.
            InitializeControllersForCurrentIsland();
            CurrentMainState.Clock.Pause();
        }

        /// <summary>
        /// Choisit la race du prochain cycle après un <see cref="RequestAscension"/> resté en
        /// attente (voir AscensionController.IsAscensionPending) : régénère l'île et reprend le jeu.
        /// </summary>
        public void ConfirmAscensionRace(RaceId chosenRace)
        {
            if (CurrentMainState == null)
                throw new InvalidOperationException("No main state available.");

            var worldId = AtlasController.GetAscensionStartingWorldId(CurrentMainState.GodState.AscensionState.AscensionsPerformed);
            var parameters = AtlasController.GetIslandParameters(worldId);
            AscensionController.ConfirmAscensionRace(CurrentMainState, parameters, chosenRace);
            InitializeControllersForCurrentIsland();
            PrestigeMapController.ApplyPrestigeToNewGame(CurrentMainState.CurrentWorldState!, CurrentMainState.PrestigeState);
            CurrentMainState.Clock.Resume();
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
            ClampDemoIslandPlaytime(mainGame);
            if (Clock.WasPausedAtSave)
                Clock.Pause();
            else
                Clock.Start();

            PruneEliminatedCivilizations(mainGame.CurrentWorldState);
            InitializeControllersForCurrentIsland();
        }

        /// <summary>
        /// Retire les civilisations PNJ à 0 ville qui traînent dans une sauvegarde antérieure à
        /// l'introduction du nettoyage automatique à l'élimination (voir
        /// <see cref="RemoveEliminatedCivilization"/>) : sans ce passage ponctuel au chargement, ces
        /// cadavres resteraient scannés indéfiniment par les boucles per-civ de chaque tick
        /// (RoadController, BuildingController, NpcGameController…) sans jamais disparaître, la
        /// sauvegarde ayant été produite avant ce correctif. Aucune civilisation PNJ n'est jamais
        /// ajoutée à <c>Civilizations</c> avant d'avoir au moins une ville (voir
        /// <see cref="AutoExtendController.SpawnAggressiveCivilization"/>) : 0 ville signifie
        /// toujours « éliminée », jamais « pas encore installée ».
        /// </summary>
        private static void PruneEliminatedCivilizations(WorldState? worldState)
            => worldState?.RemoveCivilizations(c => c.IsNpc && c.Cities.Count == 0);

        /// <summary>Ticks correspondant aux 8 h de <see cref="ClampDemoIslandPlaytime"/> (1 tick = 0.01 s).</summary>
        private const long DemoMaxIslandTicks = 8L * 60 * 60 * 100;

        /// <summary>
        /// Ramène à 8 h le temps passé sur l'île courante d'une sauvegarde issue de la version démo
        /// (<see cref="MainGameState.IsDemoSave"/>), en repoussant le StartTick de l'île. Ce temps
        /// alimente le multiplicateur de la Merveille (PrestigeController.GetWonderBonusDetails) :
        /// sans ce plafond, une démo laissée tourner des jours rapporterait un prestige sans commune
        /// mesure avec ce que la version démo est censée montrer.
        /// </summary>
        private static void ClampDemoIslandPlaytime(MainGameState mainGame)
        {
            if (!mainGame.IsDemoSave) return;

            var island = mainGame.CurrentWorldState;
            if (island == null || island.StartTick <= 0) return;

            long elapsed = mainGame.Clock.CurrentTick - island.StartTick;
            if (elapsed > DemoMaxIslandTicks)
                island.StartTick = mainGame.Clock.CurrentTick - DemoMaxIslandTicks;
        }

        /// <summary>
        /// Ordre d'exécution du tick de simulation, pour les contrôleurs abonnés à
        /// <see cref="GameClock.Advanced"/>.
        ///
        /// <para>Cet ordre n'est tenu par rien d'autre que la <b>suite des lignes</b> de
        /// <see cref="InitializeControllersForCurrentIsland"/> : un délégué multicast invoque ses
        /// abonnés dans l'ordre où ils se sont abonnés. Déplacer une ligne d'initialisation compile,
        /// passe les tests unitaires isolés, et change silencieusement le comportement du jeu — le
        /// combat résolu après le déplacement des monstres, la récolte avant la découverte des
        /// features, et le déterminisme de la partie avec, puisque plusieurs de ces contrôleurs
        /// consomment le PRNG.</para>
        ///
        /// <para>Le déclarer ici transforme cette contrainte de commentaire en invariant vérifié :
        /// <c>SimulationTickOrderTests</c> confronte cette liste à l'ordre réel des abonnés d'une
        /// partie chargée. Un contrôleur absent de la liste n'est pas contraint ; y ajouter une entrée
        /// revient à figer sa position relative.</para>
        /// </summary>
        internal static readonly Type[] SimulationTickOrder =
        {
            typeof(AutoExtendController),
            typeof(RoadController),
            typeof(CityBuilderController),
            typeof(MobileCampController),
            typeof(FeatureController),
            typeof(MilitaryController),
            typeof(MonsterFeatureController),
            typeof(VolcanoController),
            typeof(HarvestController),
            typeof(BuildingController),
            typeof(CorruptionController),
            typeof(Magic.MagicController),
            typeof(ResearchController),
            typeof(NpcGameController),
        };

        /// <summary>
        /// Route une panne attrapée par une garde de contrôleur vers le journal d'événements de l'île
        /// <b>actuellement chargée</b>. Sans ce câblage, une exception levée à chaque tick était
        /// totalement muette dans un build livré : les gardes n'écrivaient que via
        /// <c>Debug.WriteLine</c>, supprimé à la compilation en Release (voir <see cref="GameLog"/>).
        ///
        /// <para>Seule la PREMIÈRE occurrence de chaque erreur distincte arrive ici — la
        /// déduplication de <see cref="GameLog"/> évite le flot de toasts d'une panne qui se répète —
        /// et l'entrée reste ensuite consultable dans le panneau de journal.</para>
        ///
        /// <para>Passe par <see cref="CurrentMainState"/> plutôt que de capturer un WorldState :
        /// le câblage survit ainsi aux changements d'île sans être refait, et un contrôleur périmé ne
        /// retient pas en vie le monde sur lequel il avait été câblé. Sans île chargée, l'erreur est
        /// simplement déposée dans <see cref="GameLog"/> sans être signalée en jeu.</para>
        /// </summary>
        internal void ReportRuntimeError(GameLog.ErrorEntry entry)
            => CurrentMainState?.CurrentWorldState?.EventLog.Add(
                GameEventType.RuntimeError, entry.ToString(), toast: true);

        /// <summary>
        /// Les sous-contrôleurs qu'un autoplayer manipule, rassemblés pour être prêtés sans prêter
        /// tout le contrôleur principal (voir <see cref="AutoplayControllers"/>). Reconstruit à chaque
        /// appel : les contrôleurs sont fixes pour la durée de vie de ce MainGameController, mais le
        /// WorldState qu'il embarque change d'île en île.
        /// </summary>
        public AutoplayControllers CreateAutoplayControllers() => new(
            RoadController, HarvestController, BuildingController, CityBuilderController,
            TradeController, ResearchController, PrestigeController, PrestigeMapController,
            CurrentMainState?.CurrentWorldState);

        private void InitializeControllersForCurrentIsland()
        {
            var WorldState = CurrentMainState?.CurrentWorldState;

            // Emplacement unique et non événement — voir GameLog.OnFirstOccurrence : plusieurs
            // MainGameController peuvent coexister brièvement (NpcCivilizationPlacer en crée un
            // jetable) et le dernier câblage doit gagner, sans abonnement cumulé ni fuite.
            GameLog.OnFirstOccurrence = ReportRuntimeError;

            // Câblé inconditionnellement, même sans île active : c'est ce contrôleur qui expose
            // IsAscensionPending à toute l'UI (TabBarRenderer, OverlayRenderer...). Entre
            // RequestAscension et ConfirmAscensionRace, GodState.PrestigeState (et donc WorldState)
            // vaut null — sans ce câblage hors du bloc ci-dessous, recharger une sauvegarde faite
            // pendant cette attente laisserait AscensionController non initialisé et
            // IsAscensionPending retomberait à faux, perdant la trace du choix de race en cours.
            AscensionController.Initialize(WorldState, Clock, CurrentMainState!.PRNG, HarvestController, CurrentMainState!.GodState,
                CityBuilderController, MaritimeBeaconController, WarFleetController, MobileCampController);

            if (WorldState != null)
            {
                // Bind the player's TechnologyTree to the persistent prestige tree so research
                // progress survives across islands. NPC civs keep their own ephemeral empty tree.
                var prestigeState = CurrentMainState?.PrestigeState;
                if (prestigeState != null)
                    WorldState.PlayerCivilization.TechnologyTree = prestigeState.TechnologyTree;

                WorldState.Visibility.Recalculate();

                // Migration : les anciennes sauvegardes stockaient les épingles du panel de
                // civilisation par île (AutomationSettings.PinnedToCivPanel). On les reporte vers
                // les settings persistants pour qu'elles survivent aux nouvelles îles/prestiges.
                if (WorldState.AutomationSettings.PinnedToCivPanel.Count > 0)
                    CurrentMainState!.Settings.PinnedCivPanelKeys.UnionWith(WorldState.AutomationSettings.PinnedToCivPanel);

                // [Migration héritée v0.20.1] AutomationSettings vivait par île avant de devenir
                // cross-prestige ET cross-ascension (voir GodState.AutomationSettings) : la toute
                // première initialisation d'une sauvegarde antérieure reporte une seule fois les
                // interrupteurs/seuils de l'île en cours vers GodState, qui devient ensuite la seule
                // source de vérité — sans quoi le prestige ou l'ascension suivant, qui régénère un
                // WorldState.AutomationSettings vierge, écraserait silencieusement ces réglages.
                if (!CurrentMainState!.GodState.AutomationSettingsMigrated)
                {
                    CurrentMainState.GodState.AutomationSettings.MigrateFromLegacyIsland(WorldState.AutomationSettings);
                    CurrentMainState.GodState.AutomationSettingsMigrated = true;
                }

                // AutomationSettings vit désormais dans GodState (voir sa doc) : une seule instance,
                // câblée sur ce WorldState à chaque île/prestige/ascension/chargement plutôt que
                // recréée avec lui.
                WorldState.AutomationSettings = CurrentMainState.GodState.AutomationSettings;

                // Câble l'interrupteur global GameSettings.AutomationsEnabled sur les IsXActive de
                // AutomationSettings (voir AutomationSettings.Bind) — seul point de branchement du kill
                // switch, refait à chaque île/prestige/chargement bien que l'instance elle-même ne
                // change plus.
                WorldState.AutomationSettings.Bind(CurrentMainState.Settings);
                WorldState.AutomationSettings.BindPresets(CurrentMainState.GodState);

                // Migration : une sauvegarde plus ancienne peut stocker un plafond de preset
                // supérieur au niveau max théorique actuel d'un bâtiment (recherche/vertex/hexagone
                // de prestige retiré ou réduit depuis). Ramené au max courant plutôt que laissé tel
                // quel — voir AutomationPresetSettings.ClampToTheoreticalMax.
                CurrentMainState!.GodState.AutomationPresets.ClampToTheoreticalMax();

                AscensionController.ApplyPermanentUniqueBuildingToCivilization();

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
                //
                // Pour les contrôleurs qui s'abonnent à l'horloge, cet ordre d'abonnement EST l'ordre
                // d'exécution du tick : il est déclaré dans SimulationTickOrder et vérifié par test.
                RoadController.Initialize(WorldState, Clock, CurrentMainState!.PRNG, CurrentMainState?.PrestigeState);
                CityBuilderController.Initialize(WorldState, Clock, CurrentMainState!.PRNG);
                MaritimeBeaconController.Initialize(WorldState);
                WarFleetController.Initialize(WorldState);
                MobileCampController.Initialize(WorldState, CityBuilderController, Clock);
                FeatureController.Initialize(WorldState, Clock);
                MilitaryController.Initialize(WorldState, Clock, CityBuilderController, WarFleetController, MobileCampController, CurrentMainState!.PRNG);
                MonsterFeatureController.Initialize(WorldState, Clock, CurrentMainState!.PRNG, CityBuilderController, CurrentMainState?.PrestigeState, WarFleetController, MobileCampController, BuildingController);
                VolcanoController.Initialize(WorldState, Clock, CurrentMainState!.PRNG, CityBuilderController);
                TradeController.Initialize(WorldState);
                HarvestController.Initialize(WorldState, Clock, TradeController, MonsterFeatureController, CurrentMainState!.PRNG);
                BuildingController.Initialize(WorldState, Clock);
                AtlasController.Initialize(CurrentMainState!.WorldPRNG);
                PrestigeController.Initialize(WorldState.PlayerCivilization, WorldState, Clock, CurrentMainState?.PrestigeState, CurrentMainState?.GodState, BuildingController);
                WonderController.Initialize(WorldState, Clock, HarvestController);
                GreatLighthouseController.Initialize(WorldState, Clock, HarvestController);
                ObservatoryController.Initialize(WorldState, Clock, HarvestController);
                NecropolisController.Initialize(WorldState, Clock, HarvestController, CurrentMainState!.GodState);
                DeepestMineController.Initialize(WorldState, Clock, HarvestController, CurrentMainState!.GodState);
                SurfaceBreachController.Initialize(WorldState, Clock, HarvestController);
                CorruptionSpireController.Initialize(WorldState, Clock, HarvestController);
                CorruptionController.Initialize(WorldState, Clock, CurrentMainState!.PRNG, CurrentMainState?.PrestigeState);
                AbyssGateController.Initialize(WorldState, Clock, HarvestController, CurrentMainState!.GodState);
                PandemoniumGateController.Initialize(WorldState, Clock, HarvestController, CurrentMainState!.PRNG, CurrentMainState?.PrestigeState, CurrentMainState!.GodState);
                DivineBonesController.Initialize(WorldState, Clock, CurrentMainState!.GodState, CurrentMainState!.PRNG, HarvestController);
                MagicController.Initialize(WorldState, Clock, CurrentMainState!.PRNG, CityBuilderController, BuildingController, HarvestController, RoadController, CurrentMainState!.GodState);
                ResearchController.Initialize(WorldState, Clock, CurrentMainState?.PrestigeState, CurrentMainState?.Settings, CurrentMainState?.GodState);
                NpcGameController.Initialize(WorldState, Clock, MilitaryController, CreateAutoplayControllers());

                // Invalide le cache de production dès qu'un bâtiment est construit/amélioré ou une ville créée
                MagicController.OnRitualsChanged -= OnRitualsChangedInvalidateHarvestCache;
                MagicController.OnRitualsChanged += OnRitualsChangedInvalidateHarvestCache;
                BuildingController.OnBuildingBuilt -= OnBuildingChangedInvalidateHarvestCache;
                CityBuilderController.OnCityBuilt -= OnCityBuiltInvalidateHarvestCache;
                CityBuilderController.OnCityDestroyed -= OnCityDestroyedHandler;
                CityBuilderController.OnCityRelocated -= OnCityRelocatedDestroyNearbyCamps;
                RoadController.OnRoadBuilt -= OnRoadBuiltExtendMap;
                RoadController.OnAutoRoadBuilt -= OnRoadBuiltExtendMap;
                BuildingController.OnBuildingBuilt += OnBuildingChangedInvalidateHarvestCache;
                CityBuilderController.OnCityBuilt += OnCityBuiltInvalidateHarvestCache;
                CityBuilderController.OnCityDestroyed += OnCityDestroyedHandler;
                CityBuilderController.OnCityRelocated += OnCityRelocatedDestroyNearbyCamps;
                RoadController.OnRoadBuilt += OnRoadBuiltExtendMap;
                RoadController.OnAutoRoadBuilt += OnRoadBuiltExtendMap;
                FeatureController.OnFeatureDiscovered -= OnFeatureDiscovered;
                FeatureController.OnFeatureDiscovered += OnFeatureDiscovered;
                prestigeState?.TechnologyTree.RebuildModifiers();

                var gameRecord = CurrentMainState!.GameRecord;
                TaskRecordController.Initialize(gameRecord, WorldState.RunRecord, WorldState,
                    BuildingController, RoadController, CityBuilderController,
                    PrestigeMapController, ResearchController, MilitaryController, HarvestController,
                    TradeController, WonderController, CorruptionSpireController, DivineBonesController,
                    NecropolisController, CurrentMainState!.GodState, LifetimeStats);

                CivilizationHistoryController.Initialize(
                    WorldState, Clock,
                    RoadController, CityBuilderController, BuildingController, TradeController);

                TradeHistoryController.Initialize(WorldState, Clock, TradeController);

                // Doit rester le DERNIER abonné à OnCityDestroyed (souscrit après tous les autres
                // ci-dessus) : TaskRecordController.HandleCityDestroyed refait encore
                // GetCivilization(e.CivilizationIndex) pendant ce même événement pour compter les
                // civilisations détruites (bonus de prestige) — la retirer plus tôt la rendrait
                // introuvable pour lui.
                CityBuilderController.OnCityDestroyed -= RemoveEliminatedCivilization;
                CityBuilderController.OnCityDestroyed += RemoveEliminatedCivilization;

                WorldState.CivilizationRemoved -= OnCivilizationRemoved;
                WorldState.CivilizationRemoved += OnCivilizationRemoved;
            }
        }

        /// <summary>
        /// Unique abonné à <see cref="WorldState.CivilizationRemoved"/> : répercute le retrait d'une
        /// civilisation sur tous les caches indexés par <c>Civilization.Index</c>. Centralisé ici
        /// pour la même raison que <see cref="OnCityDestroyedHandler"/> — la civilisation peut être
        /// retirée depuis plusieurs endroits (élimination d'un PNJ, perte de l'Inframonde ou des
        /// Abysses, nettoyage au chargement) et tous doivent purger la même chose.
        ///
        /// <para>Ces caches ne se corrigeaient pas d'eux-mêmes : un index de civilisation n'est jamais
        /// recyclé (<c>Max(Index) + 1</c>), donc une entrée périmée n'est ni réutilisée ni écrasée,
        /// et plusieurs d'entre elles retiennent des villes, des bâtiments ou des routes d'un monde
        /// disparu jusqu'au changement d'île.</para>
        /// </summary>
        private void OnCivilizationRemoved(object? sender, int civilizationIndex)
        {
            HarvestController.PurgeCivilizationCaches(civilizationIndex);
            RoadController.PurgeCivilizationCaches(civilizationIndex);
            CityBuilderController.PurgeCivilizationCaches(civilizationIndex);
            MaritimeBeaconController.PurgeCivilizationCaches(civilizationIndex);
            BuildingController.PurgeCivilizationCaches(civilizationIndex);
            MilitaryController.PurgeCivilizationCaches(civilizationIndex);
            NpcGameController.PurgeCivilizationCaches(civilizationIndex);
        }

        private void OnFeatureDiscovered(object? sender, IslandFeature feature)
        {
            if (feature is Rats && CurrentMainState != null && !CurrentMainState.GameRecord.HasEncounteredRats)
                CurrentMainState.GameRecord.HasEncounteredRats = true;
        }

        private void OnRoadBuiltExtendMap(object? sender, RoadAutoBuiltEventArgs e)
            => AutoExtendController.TryExtendMapAfterRoad(e.CivilizationIndex, e.RoadPosition);

        /// <summary>
        /// Seule la civilisation propriétaire du bâtiment voit son cache de production invalidé. Avec
        /// une invalidation globale, chaque construction d'un PNJ — il y en a en permanence — faisait
        /// reconstruire le cache des centaines de villes du joueur au tick suivant.
        /// </summary>
        private void OnBuildingChangedInvalidateHarvestCache(object? sender, BuildingBuiltEventArgs e)
            => HarvestController.InvalidateProductionCache(e.City.CivilizationIndex);

        private void OnRitualsChangedInvalidateHarvestCache(object? sender, EventArgs e)
            => HarvestController.InvalidateProductionCache();

        private void OnCityBuiltInvalidateHarvestCache(object? sender, OutpostAutoBuiltEventArgs e)
        {
            FeatureController.RefreshContestedTerritories();
            HarvestController.InvalidateProductionCache(e.CivilizationIndex);
            MobileCampController.DestroyCampsNear(e.Position, e.CivilizationIndex);

            // Recalcule les distances des routes déjà construites de la civilisation propriétaire :
            // la nouvelle ville peut en raccourcir certaines (raccourci), ce qui les rend éligibles à
            // l'automatisation de la guilde des bâtisseurs — voir RoadController.OnCityBuilt.
            var builderCiv = CurrentMainState?.CurrentWorldState?.GetCivilization(e.CivilizationIndex);
            if (builderCiv != null)
                RoadController.OnCityBuilt(builderCiv, e.Position);

            // Une nouvelle ville — même celle d'une civilisation ennemie — peut retirer un bord
            // auparavant constructible du cache d'une AUTRE civilisation (le bord touche désormais un
            // vertex avec une ville ennemie, voir RoadController.IsEdgeBuildableByCivilization). La clé
            // de cache ne suit que le nombre de villes/balises de sa propre civilisation, donc sans cet
            // appel le cache des autres civs resterait obsolète jusqu'à leur prochaine route posée.
            RoadController.InvalidateBuildableRoadsCacheForLayer(e.Position.Z);
        }

        /// <summary>Relocating a city onto (or near) a Camp Mobile of the same civilization must destroy
        /// it, exactly like founding a new city there — see CityBuilderController.RelocateCity and
        /// MobileCampController.DestroyCampsNear.</summary>
        private void OnCityRelocatedDestroyNearbyCamps(object? sender, OutpostAutoBuiltEventArgs e)
        {
            MobileCampController.DestroyCampsNear(e.Position, e.CivilizationIndex);
        }

        /// <summary>
        /// Single subscriber to CityBuilderController.OnCityDestroyed — fires for every destruction
        /// cause (military conquest or monster attack), so road cleanup, contested-territory refresh
        /// and the underworld check all happen consistently regardless of cause.
        /// </summary>
        private void OnCityDestroyedHandler(object? sender, CityDestroyedEventArgs e)
        {
            var worldState = CurrentMainState?.CurrentWorldState;
            var civ = worldState?.GetCivilization(e.CivilizationIndex);
            if (civ != null)
                RoadController.OnCityDestroyed(civ, e.CityVertex);

            FeatureController.RefreshContestedTerritories();
            DeepestMineController.OnCityDestroyed(e.CityVertex, e.CivilizationIndex);
            SurfaceBreachController.OnCityDestroyed(e.CityVertex, e.CivilizationIndex);
            AbyssGateController.OnCityDestroyed(e.CityVertex, e.CivilizationIndex);
            if (worldState != null)
                MonumentInvestment.OnCityDestroyed(worldState, e.CityVertex, e.CivilizationIndex);
            HarvestController.InvalidateProductionCache(e.CivilizationIndex);

            if (civ != null && civ.IsNpc && civ.Cities.Count == 0)
                worldState?.EventLog.Add(GameEventType.CivilizationDestroyed, toast: true);
        }

        /// <summary>
        /// Retire de WorldState.Civilizations toute civilisation PNJ qui vient de perdre sa dernière
        /// ville. Sans ce nettoyage, une civilisation éliminée restait indéfiniment dans la liste
        /// avec 0 ville et 0 route — coût CPU inutile dans toutes les boucles per-civ exécutées
        /// chaque tick (RoadController, BuildingController, NpcGameController…), et sur une longue
        /// partie où l'Inframonde regénère régulièrement de nouvelles civs PNJ (AutoExtendController)
        /// à mesure que les précédentes sont éliminées, la liste ne fait que croître.
        /// Voir l'abonnement en fin d'InitializeControllersForCurrentIsland pour pourquoi ce
        /// gestionnaire doit rester le dernier appelé sur cet événement.
        /// </summary>
        private void RemoveEliminatedCivilization(object? sender, CityDestroyedEventArgs e)
        {
            var worldState = CurrentMainState?.CurrentWorldState;
            var civ = worldState?.GetCivilization(e.CivilizationIndex);
            if (civ != null && civ.IsNpc && civ.Cities.Count == 0)
                worldState!.RemoveCivilization(civ);
        }

        private void SetupModifierAggregators()
        {
            var prestigeState = CurrentMainState!.PrestigeState;
            var WorldState = prestigeState!.WorldState;

            var tier = prestigeState.Tier;

            // SetNpcModifiers et non AddCustomAggregator : cette méthode est rejouée à chaque SetGame,
            // et le placeur a déjà posé le jeu de la civilisation pendant la génération. Ajouter au
            // lieu de remplacer empilait les deux — Register ne dédoublonne que par instance, et ce
            // sont deux instances distinctes. Voir Civilization.SetNpcModifiers.
            foreach (var civ in WorldState!.Civilizations.Where(c => c.IsNpc))
            {
                if (civ.NpcParameters?.ExtraModifiers is { Count: > 0 } extras)
                {
                    // Civilisations de couche (Inframonde/Abysse) : leur jeu est persisté tel quel,
                    // voir AutoExtendController.BuildLayerCivModifiers.
                    civ.SetNpcModifiers(new StaticModifierProvider(extras));
                }
                else
                {
                    // Le tier retenu à la génération, pas forcément le tier courant recalculé : sur une
                    // sauvegarde antérieure à la suppression du choix de Tier de la prochaine île, le
                    // joueur pouvait viser un tier supérieur à celui calculé. Null = sauvegarde
                    // antérieure à ce champ.
                    civ.SetNpcModifiers(NpcModifierSetMaker.CreateForNpc(
                        civ.NpcParameters?.ModifierTier ?? tier,
                        civ.NpcParameters?.AggressivityLevel ?? NpcAggressivityLevel.Cautious));
                }
            }

            // Remplace en place quand la civilisation joueur est la même qu'à l'appel précédent (ex.
            // SetGame/SetGameFromSave rappelé sur le même WorldState sans régénération d'île) : un
            // simple AddCustomAggregator doublerait les modifiers de prestige, l'ancien Provider restant
            // dans la liste (Dispose() ne fait que se désabonner de VertexPurchased, GetModifiers()
            // continue de rendre son cache). Sur une nouvelle île/civilisation, Replace ne trouve pas
            // l'ancienne instance et on retombe sur Add.
            var oldPrestigeModifierProvider = _prestigeModifierProvider;
            _prestigeModifierProvider = new PrestigeModifierProvider(prestigeState, PrestigeMapController.DefaultMap);
            var playerCiv = WorldState.PlayerCivilization;
            if (oldPrestigeModifierProvider == null ||
                !playerCiv.ModifierAggregator.Replace(oldPrestigeModifierProvider, _prestigeModifierProvider))
                playerCiv.AddCustomAggregator(_prestigeModifierProvider);
            oldPrestigeModifierProvider?.Dispose();
            playerCiv.AddCustomAggregator(AscensionController);
        }
    }
}
