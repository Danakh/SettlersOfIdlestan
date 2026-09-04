using System;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Races;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Expand
{
    public class PrestigeController
    {
        private Civilization? _playerCivilization;
        private WorldState? _islandState;
        private GameClock? _clock;
        private PrestigeState? _prestigeState;
        private GodState? _godState;
        private BuildingController? _buildingController;

        internal PrestigeController()
        {
            // no op
        }

        internal void Initialize(Civilization playerCivilization, WorldState? WorldState = null, GameClock? clock = null, PrestigeState? prestigeState = null, GodState? godState = null, BuildingController? buildingController = null)
        {
            _playerCivilization = playerCivilization;
            _islandState = WorldState;
            _clock = clock;
            _prestigeState = prestigeState;
            _godState = godState;
            _buildingController = buildingController;
        }

        private long GetCurrentTick() => _clock?.CurrentTick ?? 0;

        public const int PrestigeVisiblePoints = 10;
        public const int PrestigeRequiredPoints = 20;

        public bool PrestigeIsVisible() => (CalculatePrestigePoints() >= PrestigeVisiblePoints) || HasImperialPort();

        public bool HasImperialPort() =>
            _playerCivilization?.UniqueBuildings.Contains(BuildingType.ImperialPort) == true;

        public bool HasEnoughPrestigePoints() =>
            CalculatePrestigePoints() >= PrestigeRequiredPoints;

        public bool PrestigeIsAvailable() =>
             HasEnoughPrestigePoints() && HasImperialPort();

        public int GetBuildingSubtotal() => GetPrestigePointSources().Sum(source => source.Points);

        public bool WondersUnlocked()
            => _playerCivilization?.ModifierAggregator.ApplyModifiers(ECategory.UNLOCK_WONDERS, "", 0) > 0;

        public (int Level, int TimeFactor, long RunTicks) GetWonderBonusDetails()
        {
            if (_islandState == null) return (0, 1, 0);
            var wonder = _islandState.Features.OfType<Wonder>().FirstOrDefault();
            long runTicks = _islandState.StartTick > 0
                ? Math.Max(0, GetCurrentTick() - _islandState.StartTick)
                : 0;
            int hoursPlayed = (int)Math.Ceiling(runTicks / 360000.0);
            return (wonder?.Level ?? 0, 1 + hoursPlayed, runTicks);
        }

        public int GetWonderBonus()
        {
            var (level, timeFactor, _) = GetWonderBonusDetails();
            return level * timeFactor;
        }

        /// <summary>Ticks nécessaires pour atteindre le prochain incrément du multiplicateur de temps de la Merveille (arrondi supérieur des heures jouées).</summary>
        public long GetTicksUntilNextWonderMultiplier()
        {
            var (_, _, runTicks) = GetWonderBonusDetails();
            long hoursPlayed = (long)Math.Ceiling(runTicks / 360000.0);
            long nextThreshold = hoursPlayed * 360000 + 1;
            return Math.Max(0, nextThreshold - runTicks);
        }

        public bool CanSkipToNextWonderMultiplier()
        {
            if (_clock == null) return false;
            long needed = GetTicksUntilNextWonderMultiplier();
            return needed > 0 && _clock.OfflineBankTicks >= needed;
        }

        private bool HasNoSurfaceMonsters() => !HasSurfaceMonsters();

        public bool HasSurfaceMonsters() =>
            _islandState != null && _islandState.Features
                .OfType<MonsterFeature>()
                .Any(m => m.Position.Z == IslandMap.SurfaceLayer);

        public int GetMonsterBonus()
        {
            if (!HasNoSurfaceMonsters())
                return 0;
            return GetBuildingSubtotal() / 5;
        }

        public int GetDragonBonus()
        {
            if (_islandState == null) return 0;
            return _islandState.RunRecord.DragonsDefeated * 5;
        }

        public int GetTreasureTroveBonus()
        {
            if (_islandState == null) return 0;
            return _islandState.RunRecord.TreasuresTroveClaimed;
        }

        /// <summary>
        /// Jalon Ascension Prestigieuse (voir AscensionController.IsMilestoneUnlocked) : dérivé
        /// directement de GodState.AscensionState — ce contrôleur ne détient pas de référence vers
        /// AscensionController — avec le même seuil (AscensionMilestoneDefinitions).
        /// </summary>
        public bool HasPrestigiousAscension()
        {
            var ascensionState = _godState?.AscensionState;
            if (ascensionState == null || ascensionState.AscensionsPerformed <= 0) return false;

            int threshold = AscensionMilestoneDefinitions.Get(AscensionMilestoneId.PrestigiousAscension)!.RequiredAscendedRaceCount;
            return ascensionState.AscendedRaces.Count >= threshold;
        }

        /// <summary>Ascension Prestigieuse : 1 point de prestige par point divin gagné depuis le début de la partie, versé à chaque prestige (voir aussi AscensionController.GrantPrestigiousAscensionPoints qui amorce chaque nouveau cycle d'Ascension avec la même règle).</summary>
        public int GetDivinePointsBonus()
            => HasPrestigiousAscension() ? (_godState?.TotalGodPointsEarned ?? 0) : 0;

        public double GetPrestigeGainBonus()
            => _playerCivilization?.ModifierAggregator.ApplyModifiers(ECategory.PRESTIGE_GAIN, "", 0.0) ?? 0.0;

        /// <summary>Bonus (ou malus) additif de prestige propre à la race choisie à l'Ascension (ex : -25% pour les Gobelins), distinct du bonus Prestige/Recherche.</summary>
        public double GetRaceGainBonus()
            => _playerCivilization?.ModifierAggregator.ApplyModifiers(ECategory.PRESTIGE_GAIN_RACE, "", 0.0) ?? 0.0;

        /// <summary>Niveau max effectif du Port maritime pour la civilisation courante (dépend de la
        /// race choisie, ex. 3 pour les Gobelins au lieu de 4 de base) — voir
        /// BuildingController.GetMaxLevel. Sans BuildingController câblé (tests unitaires legers),
        /// retombe sur le défaut de base (Seaport.GetDefaultMaxLevel == 4).</summary>
        public int GetSeaportMaxLevel()
        {
            if (_playerCivilization == null) return 0;
            if (_buildingController == null) return new Seaport().GetDefaultMaxLevel();
            return _buildingController.GetMaxLevel(new Seaport(), _playerCivilization);
        }

        public int GetSeaportMaxLevelCount()
        {
            int maxLevel = GetSeaportMaxLevel();
            if (maxLevel <= 0) return 0;
            return _playerCivilization?.Cities.SelectMany(c => c.Buildings)
                .Count(b => b.Type == BuildingType.Seaport && b.Level >= maxLevel) ?? 0;
        }

        public double GetSeaportPrestigeBonus()
        {
            if (_playerCivilization == null) return 0.0;
            double perSeaport = _playerCivilization.ModifierAggregator.ApplyModifiers(ECategory.PRESTIGE_GAIN_PER_SEAPORT_LEVEL4, "", 0.0);
            if (perSeaport <= 0) return 0.0;
            return GetSeaportMaxLevelCount() * perSeaport;
        }

        public int GetTempleCount()
            => _playerCivilization?.Cities.SelectMany(c => c.Buildings)
                .Count(b => b.Type == BuildingType.Temple) ?? 0;

        /// <summary>Bonus de prestige additif accordé par le Grand Temple : PRESTIGE_GAIN_PER_TEMPLE × nombre de Temples construits dans la civilisation.</summary>
        public double GetTemplePrestigeBonus()
        {
            if (_playerCivilization == null) return 0.0;
            double perTemple = _playerCivilization.ModifierAggregator.ApplyModifiers(ECategory.PRESTIGE_GAIN_PER_TEMPLE, "", 0.0);
            if (perTemple <= 0) return 0.0;
            return GetTempleCount() * perTemple;
        }

        public const double PrestigeGainPerCivilizationDestroyed = 0.2;

        public int GetCivilizationsDestroyedCount() => _islandState?.RunRecord.CivilizationsDestroyed ?? 0;

        /// <summary>+20% de points de prestige par civilisation ennemie entièrement éliminée ce run.</summary>
        public double GetCivilizationsDestroyedBonus() => GetCivilizationsDestroyedCount() * PrestigeGainPerCivilizationDestroyed;

        /// <summary>
        /// True si la Spire de Corruption est bâtie, ou si elle a évolué en Faille des Abysses
        /// (la Faille reprend pour l'instant le même bonus de prestige que la Spire).
        /// </summary>
        public bool HasCorruptionSpireBuilt()
            => _islandState?.Features.OfType<CorruptionSpire>().Any(f => f.Built) == true
               || _islandState?.Features.OfType<AbyssGate>().Any(f => f.Built) == true;

        /// <summary>
        /// Vrai une fois les 3 vertex de prestige de l'Abysse achetés (Porte Planaire, Faille des
        /// Abysses, Rituel de l'Éclipse Noire — voir CorruptionSpireController.AbyssUnlockThreshold),
        /// même si la Spire de Corruption n'a pas encore été construite. Pilote la visibilité du
        /// bouton de prestige corrompu (voir PrestigeRenderer) : le bouton reste affiché dès ce
        /// déblocage, avec un message expliquant comment construire la Spire tant qu'elle n'est pas
        /// bâtie (voir HasCorruptionSpireBuilt, qui reste la condition réelle d'activation).
        /// </summary>
        public bool IsCorruptedPrestigeUnlocked()
            => _playerCivilization?.ModifierAggregator.ApplyModifiers(ECategory.UNLOCK_ABYSS, "", 0)
               >= CorruptionSpireController.AbyssUnlockThreshold;

        /// <summary>
        /// Étape restante avant que le Prestige Corrompu ne soit disponible, une fois débloqué (voir
        /// <see cref="IsCorruptedPrestigeUnlocked"/>) mais tant que la Spire de Corruption n'est pas
        /// bâtie (voir <see cref="HasCorruptionSpireBuilt"/>) — pilote le message affiché sous le
        /// bouton de Prestige Corrompu (voir PrestigeRenderer). La Spire ne pouvant être placée que
        /// sur une Source de Corruption (voir <see cref="IslandFeatures.CorruptionSource"/>, semée
        /// aléatoirement par AutoExtendController.TrySpawnUnderworldDenizen), et sa construction la
        /// détruisant, trois étapes distinctes précèdent <see cref="Available"/> selon ce qui existe
        /// déjà sur la carte.
        /// </summary>
        public enum CorruptedPrestigeStep
        {
            /// <summary>La Spire est bâtie (ou a évolué en Faille des Abysses) : le Prestige Corrompu est disponible.</summary>
            Available,
            /// <summary>Une Spire est placée mais pas encore bâtie : il reste à investir des ressources pour l'achever.</summary>
            SpireUnderConstruction,
            /// <summary>Une Source de Corruption existe sur la carte, mais aucune Spire n'y a encore été placée.</summary>
            SourceAwaitingSpire,
            /// <summary>Aucune Source de Corruption n'existe encore sur la carte (tirage aléatoire non encore obtenu).</summary>
            NoSourceYet,
        }

        public CorruptedPrestigeStep GetCorruptedPrestigeStep()
        {
            if (HasCorruptionSpireBuilt()) return CorruptedPrestigeStep.Available;
            if (_islandState?.Features.OfType<CorruptionSpire>().Any() == true) return CorruptedPrestigeStep.SpireUnderConstruction;
            if (_islandState?.Features.OfType<CorruptionSource>().Any() == true) return CorruptedPrestigeStep.SourceAwaitingSpire;
            return CorruptedPrestigeStep.NoSourceYet;
        }

        public int GetMaxCorruptionLevelCleared() => _prestigeState?.MaxCorruptionLevelCleared ?? 0;

        /// <summary>
        /// Bonus de prestige lié au nettoyage de la Corruption : 2 × le niveau de la plus haute Source
        /// de Corruption jamais détruite (voir PrestigeState.MaxCorruptionLevelCleared), au minimum ×1.
        /// Une Source n'est détruite que par l'achèvement d'une Spire de Corruption posée dessus (voir
        /// CorruptionSpireController.OnInvestmentCycleCompleted) : c'est là tout l'intérêt de la Spire,
        /// dont le niveau n'est par ailleurs pas améliorable. Dissiper une zone de Corruption au Temple
        /// ou au Dominion ne donne plus ce bonus — cela ne sert plus qu'à ouvrir la Faille des Abysses.
        /// </summary>
        public int GetCorruptionClearBonusMultiplier()
            => Math.Max(1, 2 * GetMaxCorruptionLevelCleared());

        public int GetCorruptionLevel() => _prestigeState?.CurrentCorruptionLevel ?? 1;

        /// <summary>
        /// Niveau de corruption au-delà duquel un prestige corrompu demande confirmation tant qu'aucune
        /// Ascension n'a été faite : au-delà de 3, la Corruption déborde en surface (voir
        /// PrestigeState.SurfaceCorruptionLevel) et les monstres montent d'un cran par niveau (voir
        /// MonsterLeveling), alors que les pouvoirs divins qui aident à tenir restent hors de portée.
        /// </summary>
        public const int CorruptionWarningLevelWithoutAscension = 4;

        /// <summary>
        /// Vrai si un prestige corrompu porterait la corruption au-delà de
        /// <see cref="CorruptionWarningLevelWithoutAscension"/> alors qu'aucune Ascension n'a jamais été
        /// faite. L'UI ouvre alors une confirmation (voir PrestigeRenderer.TryPrestige) : le niveau de
        /// corruption ne redescend jamais hors Ascension, un joueur qui monte trop haut trop tôt se
        /// retrouve sur des îles qu'il ne peut plus tenir.
        /// </summary>
        public bool CorruptedPrestigeNeedsAscensionWarning(GodState godState)
            => HasCorruptionSpireBuilt()
               && GetCorruptionLevel() >= CorruptionWarningLevelWithoutAscension
               && godState.AscensionState.AscensionsPerformed == 0;

        public int GetTier() => _prestigeState?.Tier ?? 1;

        /// <summary>+20% de gain de prestige par palier de progression (Tier) au-delà du premier.</summary>
        public double GetTierBonus() => 0.2 * (GetTier() - 1);

        public int GetGreatLighthouseLevel()
            => _islandState?.Features.OfType<GreatLighthouse>().FirstOrDefault()?.Level ?? 0;

        /// <summary>+10% de prestige par niveau du Grand Phare.</summary>
        public double GetGreatLighthousePrestigeBonus() => 0.1 * GetGreatLighthouseLevel();

        // Grand Phare niveau 2 : débloque la construction de Balises Maritimes — voir
        // GreatLighthouseController.AreMaritimeBeaconsUnlocked / MaritimeBeaconController.
        // Grand Phare niveau 3 : débloque la construction de Flottes de Guerre — voir
        // WarFleetController.IsWarFleetUnlocked.

        public int CalculatePrestigePoints()
        {
            int subtotal = GetBuildingSubtotal() + GetDragonBonus();
            int wonderMult = GetWonderBonus(); // = level × timeFactor, 0 si pas de wonder
            double result = wonderMult > 0 ? (double)subtotal * wonderMult : subtotal;
            if (HasNoSurfaceMonsters())
                result *= 1.2;
            double gainBonus = GetPrestigeGainBonus();
            double raceBonus = GetRaceGainBonus();
            double seaportBonus = GetSeaportPrestigeBonus();
            double templeBonus = GetTemplePrestigeBonus();
            double civDestroyedBonus = GetCivilizationsDestroyedBonus();
            double tierBonus = GetTierBonus();
            double greatLighthouseBonus = GetGreatLighthousePrestigeBonus();
            result *= (1 + gainBonus + raceBonus + seaportBonus + templeBonus + civDestroyedBonus + tierBonus + greatLighthouseBonus);
            result *= GetCorruptionClearBonusMultiplier();
            return (int)result;
        }

        public IReadOnlyList<PrestigePointSource> GetPrestigePointSources()
        {
            if (_playerCivilization == null)
                return Array.Empty<PrestigePointSource>();

            var sources = new Dictionary<string, int>();
            var tooltipKeys = new Dictionary<string, string>();
            foreach (var city in _playerCivilization.Cities)
            {
                foreach (var building in city.Buildings)
                {
                    var points = GetBuildingPrestigePoints(building);
                    if (points > 0)
                    {
                        if (!sources.TryAdd(building.NameKey, points))
                            sources[building.NameKey] += points;
                        tooltipKeys.TryAdd(building.NameKey, $"prestige_source_tooltip_{building.Type.ToString().ToLower()}");
                    }
                }
            }

            int dragonBonus = GetDragonBonus();
            if (dragonBonus > 0)
            {
                sources["prestige_dragon_bonus"] = dragonBonus;
                tooltipKeys["prestige_dragon_bonus"] = "prestige_tooltip_dragon_bonus";
            }

            int troveBonus = GetTreasureTroveBonus();
            if (troveBonus > 0)
            {
                sources["prestige_treasure_trove_bonus"] = troveBonus;
                tooltipKeys["prestige_treasure_trove_bonus"] = "prestige_tooltip_treasure_trove_bonus";
            }

            int divinePointsBonus = GetDivinePointsBonus();
            if (divinePointsBonus > 0)
            {
                sources["prestige_divine_points_bonus"] = divinePointsBonus;
                tooltipKeys["prestige_divine_points_bonus"] = "prestige_tooltip_divine_points_bonus";
            }

            return sources
                .Select(source => new PrestigePointSource(source.Key, source.Value, tooltipKeys.GetValueOrDefault(source.Key)))
                .OrderBy(source => SourceSortKey(source.LabelKey))
                .ToList();
        }

        // Hôtel de ville avant Temple dans l'affichage ; les autres sources gardent l'ordre alphabétique.
        private static string SourceSortKey(string labelKey) => labelKey switch
        {
            "building_townhall_name" => "building_0",
            "building_temple_name"   => "building_1",
            _ => labelKey
        };

        public int GetBuildingPrestigePoints(Building building)
        {
            return building.Type switch
            {
                BuildingType.Temple => 1,
                BuildingType.TownHall => (building.Level > 2 ? 2 : 1),
                _ => 0
            };
        }

        public int GetBuildingPrestigePointsAtNextLevel(Building building)
        {
            return building.Type switch
            {
                BuildingType.Temple => 1,
                BuildingType.TownHall => (building.Level + 1 > 2 ? 2 : 1),
                _ => 0
            };
        }

        /// <summary>Nombre d'essences divines qui seraient perdues par un prestige immédiat (voir clamp appliqué dans PerformPrestige).</summary>
        public int GetDivineEssenceLoss(GodState godState)
            => Math.Max(0, godState.DivineEssence + godState.DivineEssenceReliquaryFloor - GetDivineEssenceReliquaryCapacity(godState));

        /// <summary>
        /// Capacité du Reliquaire (Civilization.DivineEssenceKeptOnPrestige), doublée sous Purification
        /// Supérieure — voir AscensionState.ApplyReliquaryCapacityBonus.
        /// </summary>
        private int GetDivineEssenceReliquaryCapacity(GodState godState)
            => godState.AscensionState.ApplyReliquaryCapacityBonus(_playerCivilization?.DivineEssenceKeptOnPrestige ?? 0);

        public void PerformPrestige(MainGameState mainGameState, IslandParameters nextIslandParameters)
            => PerformPrestige(mainGameState, nextIslandParameters, corrupted: false);

        public void PerformPrestige(MainGameState mainGameState, IslandParameters nextIslandParameters, bool corrupted)
        {
            if (!PrestigeIsAvailable())
                throw new InvalidOperationException("Prestige is not available.");
            if (mainGameState.PrestigeState == null)
                throw new InvalidOperationException("PrestigeState is not available.");

            var points = CalculatePrestigePoints();

            // Démo : le prestige accumulé est plafonné (PrestigeState.DemoMaxTotalPrestigePointsEarned).
            // Plafonné ici plutôt que dans CalculatePrestigePoints pour que le prestige reste
            // déclenchable une fois le plafond atteint ; les stats de la partie enregistrées plus bas
            // reprennent donc bien ce qui a été effectivement versé.
            points = mainGameState.PrestigeState.ClampDemoPrestigeGain(points, mainGameState.Settings.DemoMode);

            if (corrupted && HasCorruptionSpireBuilt())
                mainGameState.PrestigeState.CurrentCorruptionLevel++;

            if (mainGameState.PrestigeState.CurrentCorruptionLevel > mainGameState.GameRecord.MaxCorruptionLevelReached)
                mainGameState.GameRecord.MaxCorruptionLevelReached = mainGameState.PrestigeState.CurrentCorruptionLevel;

            var currentIsland = mainGameState.CurrentWorldState;
            if (currentIsland != null)
            {
                var civ = currentIsland.PlayerCivilization;
                var allBuildings = civ.Cities.SelectMany(c => c.Buildings).ToList();
                var stats = new PrestigeRunStats
                {
                    WorldId = currentIsland.WorldId,
                    TickDuration = mainGameState.Clock.CurrentTick - currentIsland.StartTick,
                    CityCount = civ.Cities.Count,
                    BuildingCount = allBuildings.Count,
                    TotalBuildingLevels = allBuildings.Sum(b => b.Level),
                    PrestigePoints = points,
                    ResearchCompleted = currentIsland.RunRecord?.ResearchCompleted ?? 0,
                    UniqueBuildings = allBuildings.Count(b => b.IsUnique),
                    WonderLevel = currentIsland.Features.OfType<Wonder>().FirstOrDefault()?.Level ?? 0,
                    HasDeepestMine = currentIsland.Features.OfType<SettlersOfIdlestan.Model.IslandFeatures.DeepestMine>().Any(m => m.Dug),
                    HasCorruptionSpire = currentIsland.Features.OfType<CorruptionSpire>().Any(s => s.Built),
                    HasAbyssGate = currentIsland.Features.OfType<AbyssGate>().Any(g => g.Built),
                    Tier = mainGameState.PrestigeState.Tier,
                    Corruption = mainGameState.PrestigeState.CurrentCorruptionLevel,
                };
                mainGameState.PrestigeState.RunHistory.Add(stats);
                while (mainGameState.PrestigeState.RunHistory.Count > 5)
                    mainGameState.PrestigeState.RunHistory.RemoveAt(0);

                var gameRecord = mainGameState.GameRecord;
                gameRecord.MaxCitiesInSingleRun = Math.Max(gameRecord.MaxCitiesInSingleRun, stats.CityCount);
                gameRecord.MaxBuildingsInSingleRun = Math.Max(gameRecord.MaxBuildingsInSingleRun, stats.BuildingCount);
                gameRecord.MaxTotalBuildingLevelsInSingleRun = Math.Max(gameRecord.MaxTotalBuildingLevelsInSingleRun, stats.TotalBuildingLevels);
                gameRecord.MaxUniqueBuildingsInSingleRun = Math.Max(gameRecord.MaxUniqueBuildingsInSingleRun, stats.UniqueBuildings);
                gameRecord.MaxResearchInSingleRun = Math.Max(gameRecord.MaxResearchInSingleRun, stats.ResearchCompleted);
                gameRecord.MaxPlaytimeInSingleRun = Math.Max(gameRecord.MaxPlaytimeInSingleRun, stats.TickDuration);
                if (stats.HasDeepestMine) gameRecord.HasDugDeepestMine = true;
                if (stats.HasAbyssGate) gameRecord.HasBuiltAbyssGate = true;
            }

            // Reliquaire Sacré / Reliquaire Renforcé (DIVINE_ESSENCE_KEPT_ON_PRESTIGE), doublé sous
            // Purification Supérieure (voir GetDivineEssenceReliquaryCapacity) : jusqu'à N essences
            // divines (parmi celles du run qui s'achève + celles déjà dans le Reliquaire) survivent au
            // prestige dans le Reliquaire — voir GodState.DivineEssenceReliquaryFloor. DivineEssence
            // (les essences du run, hors Reliquaire) repart, elle, toujours de zéro.
            int totalEssenceBeforePrestige = mainGameState.GodState.DivineEssence + mainGameState.GodState.DivineEssenceReliquaryFloor;
            mainGameState.GodState.DivineEssenceReliquaryFloor = Math.Min(
                totalEssenceBeforePrestige, GetDivineEssenceReliquaryCapacity(mainGameState.GodState));
            mainGameState.GodState.DivineEssence = 0;

            mainGameState.PrestigeState.PrestigePoints += points;
            mainGameState.PrestigeState.TotalPrestigePointsEarned += points;
            mainGameState.PrestigeState.ResetTargetedDivinePowerUses();
            mainGameState.PrestigeState.WorldState = null;

            var generator = new IslandMapGenerator(mainGameState.WorldPRNG);
            var nextWorldState = generator.GenerateWorldState(
                nextIslandParameters,
                mainGameState.Clock.CurrentTick,
                startTick: mainGameState.Clock.CurrentTick,
                surfaceCorruptionLevel: mainGameState.PrestigeState.SurfaceCorruptionLevel,
                tier: mainGameState.PrestigeState.Tier,
                race: RaceDefinitions.Get(mainGameState.GodState.AscensionState.SelectedRace))
                ?? throw new InvalidOperationException("Failed to generate next island.");

            mainGameState.PrestigeState.WorldState = nextWorldState;

            // Magie Divine : chaque prestige recrédite 1 charge de lancement par sort — voir
            // MagicState.GrantInitialSpellCharges.
            if (mainGameState.GodState.AscensionState.IsDivineMagicActive)
                nextWorldState.Magic.GrantInitialSpellCharges();

            // Nouvelle île : l'état éphémère d'automatisation (cible de raid, Héraut de Guerre,
            // Vendetta) référencerait sinon des coordonnées de l'île abandonnée — voir
            // AutomationSettings.ResetIslandEphemeralState. Les interrupteurs/seuils, eux, survivent
            // sans rien à faire ici : ils vivent désormais dans GodState.AutomationSettings, câblé sur
            // nextWorldState par MainGameController.InitializeControllersForCurrentIsland.
            mainGameState.GodState.AutomationSettings.ResetIslandEphemeralState();
        }
    }

    public readonly record struct PrestigePointSource(string LabelKey, int Points, string? TooltipKey = null);
}
