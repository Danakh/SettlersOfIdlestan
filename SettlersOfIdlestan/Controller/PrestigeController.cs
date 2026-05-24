using System;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.PrestigeMap;
using SettlersOfIdlestan.Controller.Generator;

namespace SettlersOfIdlestan.Controller
{
    public class PrestigeController
    {
        private Civilization? _playerCivilization;

        internal PrestigeController()
        {
            // no op
        }

        internal void Initialize(Civilization playerCivilization)
        {
            _playerCivilization = playerCivilization;
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

            var civilizations = new List<Civilization>();
            for (int i = 0; i < nextIslandParameters.CivilizationCount; i++)
            {
                civilizations.Add(new Civilization { Index = i });
            }

            var generator = new IslandMapGenerator(mainGameState.PRNG);
            var map = generator.GenerateIsland(nextIslandParameters.TileData, civilizations)
                ?? throw new InvalidOperationException("Failed to generate next island.");

            mainGameState.PrestigeState.IslandState = new IslandState(map, civilizations, nextIslandParameters.IslandID)
            {
                StartTick = mainGameState.Clock.CurrentTick
            };
        }
    }

    public readonly record struct PrestigePointSource(string LabelKey, int Points);
}
