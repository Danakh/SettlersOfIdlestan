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

        internal PrestigeController()
        {
            // no op
        }

        internal void Initialize(Civilization playerCivilization, WorldState? WorldState = null, GameClock? clock = null)
        {
            _playerCivilization = playerCivilization;
            _islandState = WorldState;
            _clock = clock;
        }

        private long GetCurrentTick() => _clock?.CurrentTick ?? 0;

        public const int PrestigeVisiblePoints = 10;
        public const int PrestigeRequiredPoints = 20;

        public bool PrestigeIsVisible() => CalculatePrestigePoints() >= PrestigeVisiblePoints;

        public bool HasImperialPort() =>
            _playerCivilization?.UniqueBuildings.Contains(BuildingType.ImperialPort) == true;

        public bool PrestigeIsAvailable() =>
            CalculatePrestigePoints() >= PrestigeRequiredPoints && HasImperialPort();

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

        public int CalculatePrestigePoints()
        {
            int subtotal = GetBuildingSubtotal() + GetDragonBonus();
            int wonderMult = GetWonderBonus(); // = level × timeFactor, 0 si pas de wonder
            double result = wonderMult > 0 ? (double)subtotal * wonderMult : subtotal;
            if (HasNoSurfaceMonsters())
                result *= 1.2;
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
        {
            if (!PrestigeIsAvailable())
                throw new InvalidOperationException("Prestige is not available.");
            if (mainGameState.PrestigeState == null)
                throw new InvalidOperationException("PrestigeState is not available.");

            var points = CalculatePrestigePoints();

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

            var generator = new IslandMapGenerator(mainGameState.PRNG);
            var nextWorldState = generator.GenerateWorldState(
                nextIslandParameters,
                mainGameState.Clock.CurrentTick,
                startTick: mainGameState.Clock.CurrentTick)
                ?? throw new InvalidOperationException("Failed to generate next island.");

            mainGameState.PrestigeState.WorldState = nextWorldState;
        }
    }

    public readonly record struct PrestigePointSource(string LabelKey, int Points, string? TooltipKey = null);
}
