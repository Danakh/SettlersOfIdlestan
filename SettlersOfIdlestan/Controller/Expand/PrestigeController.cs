using System;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Controller.Generator;

namespace SettlersOfIdlestan.Controller.Expand
{
    public class PrestigeController
    {
        private Civilization? _playerCivilization;
        private IslandState? _islandState;
        private GameClock? _clock;

        internal PrestigeController()
        {
            // no op
        }

        internal void Initialize(Civilization playerCivilization, IslandState? islandState = null, GameClock? clock = null)
        {
            _playerCivilization = playerCivilization;
            _islandState = islandState;
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

        public int GetBanditBonus()
        {
            if (_islandState == null || _islandState.RunRecord.BanditsDefeated == 0)
                return 0;
            return GetBuildingSubtotal() / 5;
        }

        public int CalculatePrestigePoints() => GetBuildingSubtotal() + GetBanditBonus();

        public IReadOnlyList<PrestigePointSource> GetPrestigePointSources()
        {
            if (_playerCivilization == null)
                return Array.Empty<PrestigePointSource>();

            var sources = new Dictionary<string, int>();
            foreach (var city in _playerCivilization.Cities)
            {
                foreach (var building in city.Buildings)
                {
                    var points = GetBuildingPrestigePoints(building);
                    if (points > 0)
                    {
                        if (!sources.TryAdd(building.NameKey, points))
                            sources[building.NameKey] += points;
                    }
                }
            }

            if (_islandState != null)
            {
                var wonder = _islandState.Features.OfType<Wonder>().FirstOrDefault();
                if (wonder != null)
                {
                    int buildingPoints = sources.Values.Sum();
                    long runTicks = (_islandState.StartTick > 0 && _playerCivilization != null)
                        ? Math.Max(0, GetCurrentTick() - _islandState.StartTick)
                        : 0;
                    int hoursPlayed = (int)(runTicks / 360000);
                    int fromBuildings = buildingPoints / 10;
                    int wonderPoints = wonder.Level + hoursPlayed + fromBuildings;
                    if (wonderPoints > 0)
                        sources["prestige_wonder"] = wonderPoints;
                }
            }

            return sources
                .Select(source => new PrestigePointSource(source.Key, source.Value))
                .OrderBy(source => source.LabelKey)
                .ToList();
        }

        public int GetBuildingPrestigePoints(Building building)
        {
            return building.Type switch
            {
                BuildingType.Temple => 1,
                BuildingType.Library => 1,
                BuildingType.TownHall => (building.Level > 2 ? 2 : 1),
                _ => 0
            };
        }

        public int GetBuildingPrestigePointsAtNextLevel(Building building)
        {
            return building.Type switch
            {
                BuildingType.Temple => 1,
                BuildingType.Library => 1,
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

            var currentIsland = mainGameState.CurrentIslandState;
            if (currentIsland != null)
            {
                var civ = currentIsland.PlayerCivilization;
                var allBuildings = civ.Cities.SelectMany(c => c.Buildings).ToList();
                var stats = new PrestigeRunStats
                {
                    IslandId = currentIsland.IslandID,
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
            mainGameState.PrestigeState.IslandState = null;

            var generator = new IslandMapGenerator(mainGameState.PRNG);
            var nextIslandState = generator.GenerateIslandState(
                nextIslandParameters,
                mainGameState.Clock.CurrentTick,
                startTick: mainGameState.Clock.CurrentTick)
                ?? throw new InvalidOperationException("Failed to generate next island.");

            mainGameState.PrestigeState.IslandState = nextIslandState;
        }
    }

    public readonly record struct PrestigePointSource(string LabelKey, int Points);
}
