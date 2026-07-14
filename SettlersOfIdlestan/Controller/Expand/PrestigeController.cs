using System;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Controller.Generator;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Expand
{
    public class PrestigeController
    {
        private Civilization? _playerCivilization;
        private WorldState? _islandState;
        private GameClock? _clock;
        private PrestigeState? _prestigeState;

        internal PrestigeController()
        {
            // no op
        }

        internal void Initialize(Civilization playerCivilization, WorldState? WorldState = null, GameClock? clock = null, PrestigeState? prestigeState = null)
        {
            _playerCivilization = playerCivilization;
            _islandState = WorldState;
            _clock = clock;
            _prestigeState = prestigeState;
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

        public double GetPrestigeGainBonus()
            => _playerCivilization?.ModifierAggregator.ApplyModifiers(ECategory.PRESTIGE_GAIN, "", 0.0) ?? 0.0;

        public int GetSeaportLevel4Count()
            => _playerCivilization?.Cities.SelectMany(c => c.Buildings)
                .Count(b => b.Type == BuildingType.Seaport && b.Level >= 4) ?? 0;

        public double GetSeaportPrestigeBonus()
        {
            if (_playerCivilization == null) return 0.0;
            double perSeaport = _playerCivilization.ModifierAggregator.ApplyModifiers(ECategory.PRESTIGE_GAIN_PER_SEAPORT_LEVEL4, "", 0.0);
            if (perSeaport <= 0) return 0.0;
            return GetSeaportLevel4Count() * perSeaport;
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

        /// <summary>Multiplicateur de la Spire de Corruption : 2 × niveau de corruption courant si construite, sinon 1.</summary>
        public int GetCorruptionSpireMultiplier()
            => HasCorruptionSpireBuilt() ? 2 * (_prestigeState?.CurrentCorruptionLevel ?? 1) : 1;

        public int GetCorruptionLevel() => _prestigeState?.CurrentCorruptionLevel ?? 1;

        public int GetTier() => _prestigeState?.Tier ?? 1;

        /// <summary>+10% de gain de prestige par palier de progression (Tier) atteint.</summary>
        public double GetTierBonus() => 0.1 * GetTier();

        public int GetGreatLighthouseLevel()
            => _islandState?.Features.OfType<GreatLighthouse>().FirstOrDefault()?.Level ?? 0;

        /// <summary>+10% de prestige par niveau du Grand Phare.</summary>
        public double GetGreatLighthousePrestigeBonus() => 0.1 * GetGreatLighthouseLevel();

        // Grand Phare niveau 2 : débloque la construction de Balises Maritimes — voir
        // GreatLighthouseController.AreMaritimeBeaconsUnlocked / MaritimeBeaconController.

        /// <summary>
        /// Grand Phare niveau 3 : permet de choisir le Tier cible de la prochaine île au moment du
        /// prestige (le Tier calculé à partir des points de prestige devient un minimum).
        /// </summary>
        public bool CanChooseNextIslandTier() => GetGreatLighthouseLevel() >= 3;

        public const int MaxNextIslandTierChoiceBonus = 10;

        public int GetNextIslandTierChoice()
        {
            int minTier = GetTier();
            int chosen = _prestigeState?.SelectedNextIslandTier ?? minTier;
            return Math.Clamp(chosen, minTier, minTier + MaxNextIslandTierChoiceBonus);
        }

        public void SetNextIslandTierChoice(int tier)
        {
            if (_prestigeState == null || !CanChooseNextIslandTier()) return;
            int minTier = GetTier();
            _prestigeState.SelectedNextIslandTier = Math.Clamp(tier, minTier, minTier + MaxNextIslandTierChoiceBonus);
        }

        public int CalculatePrestigePoints()
        {
            int subtotal = GetBuildingSubtotal() + GetDragonBonus();
            int wonderMult = GetWonderBonus(); // = level × timeFactor, 0 si pas de wonder
            double result = wonderMult > 0 ? (double)subtotal * wonderMult : subtotal;
            if (HasNoSurfaceMonsters())
                result *= 1.2;
            double gainBonus = GetPrestigeGainBonus();
            double seaportBonus = GetSeaportPrestigeBonus();
            double civDestroyedBonus = GetCivilizationsDestroyedBonus();
            double tierBonus = GetTierBonus();
            double greatLighthouseBonus = GetGreatLighthousePrestigeBonus();
            result *= (1 + gainBonus + seaportBonus + civDestroyedBonus + tierBonus + greatLighthouseBonus);
            result *= GetCorruptionSpireMultiplier();
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

            return sources
                .Select(source => new PrestigePointSource(source.Key, source.Value, tooltipKeys.GetValueOrDefault(source.Key)))
                .OrderBy(source => source.LabelKey)
                .ToList();
        }

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

        public void PerformPrestige(MainGameState mainGameState, IslandParameters nextIslandParameters)
            => PerformPrestige(mainGameState, nextIslandParameters, corrupted: false);

        public void PerformPrestige(MainGameState mainGameState, IslandParameters nextIslandParameters, bool corrupted)
        {
            if (!PrestigeIsAvailable())
                throw new InvalidOperationException("Prestige is not available.");
            if (mainGameState.PrestigeState == null)
                throw new InvalidOperationException("PrestigeState is not available.");

            var points = CalculatePrestigePoints();

            if (corrupted && HasCorruptionSpireBuilt())
                mainGameState.PrestigeState.CurrentCorruptionLevel++;

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
                };
                mainGameState.PrestigeState.RunHistory.Add(stats);
                while (mainGameState.PrestigeState.RunHistory.Count > 5)
                    mainGameState.PrestigeState.RunHistory.RemoveAt(0);
            }

            mainGameState.PrestigeState.PrestigePoints += points;
            mainGameState.PrestigeState.TotalPrestigePointsEarned += points;
            mainGameState.PrestigeState.WalkOfGodUsesSinceLastPrestige = 0;
            mainGameState.PrestigeState.PresenceOfGodUsesSinceLastPrestige = 0;
            mainGameState.PrestigeState.WorldState = null;

            var generator = new IslandMapGenerator(mainGameState.WorldPRNG);
            var nextWorldState = generator.GenerateWorldState(
                nextIslandParameters,
                mainGameState.Clock.CurrentTick,
                startTick: mainGameState.Clock.CurrentTick,
                surfaceCorruptionLevel: mainGameState.PrestigeState.SurfaceCorruptionLevel,
                tier: mainGameState.PrestigeState.EffectiveNextIslandTier)
                ?? throw new InvalidOperationException("Failed to generate next island.");

            mainGameState.PrestigeState.SelectedNextIslandTier = null;
            mainGameState.PrestigeState.WorldState = nextWorldState;
        }
    }

    public readonly record struct PrestigePointSource(string LabelKey, int Points, string? TooltipKey = null);
}
