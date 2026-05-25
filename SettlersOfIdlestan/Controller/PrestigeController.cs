using System;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Controller.Generator;

namespace SettlersOfIdlestan.Controller
{
    public class PrestigeController
    {
        private Civilization? _playerCivilization;
        private IslandState? _islandState;

        internal PrestigeController()
        {
            // no op
        }

        internal void Initialize(Civilization playerCivilization, IslandState? islandState = null)
        {
            _playerCivilization = playerCivilization;
            _islandState = islandState;
        }

        public const int PrestigeVisiblePoints = 10;
        public const int PrestigeRequiredPoints = 20;

        public bool PrestigeIsVisible() => CalculatePrestigePoints() >= PrestigeVisiblePoints;

        public bool PrestigeIsAvailable() => CalculatePrestigePoints() >= PrestigeRequiredPoints;

        public int CalculatePrestigePoints() => GetPrestigePointSources().Sum(source => source.Points);

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
            if (_islandState != null
                && _islandState.Bandits.Count == 0
                && _islandState.Map.Tiles.Values.Any(t => t.TerrainType == TerrainType.Desert))
            {
                sources["prestige_no_bandits"] = 2;
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
