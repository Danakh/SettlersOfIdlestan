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

        public bool HasCorruptionSpireBuilt()
            => _islandState?.Features.OfType<CorruptionSpire>().Any(f => f.Built) == true;

        /// <summary>Multiplicateur de la Spire de Corruption : 2 × niveau de corruption courant si construite, sinon 1.</summary>
        public int GetCorruptionSpireMultiplier()
            => HasCorruptionSpireBuilt() ? 2 * (_prestigeState?.CurrentCorruptionLevel ?? 1) : 1;

        public int GetCorruptionLevel() => _prestigeState?.CurrentCorruptionLevel ?? 1;

        public int CalculatePrestigePoints()
        {
            int subtotal = GetBuildingSubtotal() + GetDragonBonus();
            int wonderMult = GetWonderBonus(); // = level × timeFactor, 0 si pas de wonder
            double result = wonderMult > 0 ? (double)subtotal * wonderMult : subtotal;
            if (HasNoSurfaceMonsters())
                result *= 1.2;
            double gainBonus = GetPrestigeGainBonus();
            double seaportBonus = GetSeaportPrestigeBonus();
            if (gainBonus > 0 || seaportBonus > 0)
                result *= (1 + gainBonus + seaportBonus);
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
                };
                mainGameState.PrestigeState.RunHistory.Add(stats);
                while (mainGameState.PrestigeState.RunHistory.Count > 5)
                    mainGameState.PrestigeState.RunHistory.RemoveAt(0);
            }

            mainGameState.PrestigeState.PrestigePoints += points;
            mainGameState.PrestigeState.TotalPrestigePointsEarned += points;
            mainGameState.PrestigeState.WorldState = null;

            var generator = new IslandMapGenerator(mainGameState.WorldPRNG);
            var nextWorldState = generator.GenerateWorldState(
                nextIslandParameters,
                mainGameState.Clock.CurrentTick,
                startTick: mainGameState.Clock.CurrentTick,
                surfaceCorruptionLevel: mainGameState.PrestigeState.SurfaceCorruptionLevel)
                ?? throw new InvalidOperationException("Failed to generate next island.");

            mainGameState.PrestigeState.WorldState = nextWorldState;
        }
    }

    public readonly record struct PrestigePointSource(string LabelKey, int Points, string? TooltipKey = null);
}
