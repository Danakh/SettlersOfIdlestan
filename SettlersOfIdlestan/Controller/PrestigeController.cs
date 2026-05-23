using System;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Buildings;
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

        public bool PrestigeIsVisible() => CalculatePrestigePoints() >= 10;

        public bool PrestigeIsAvailable() => CalculatePrestigePoints() >= 20;

        public int CalculatePrestigePoints() => GetPrestigePointSources().Sum(source => source.Points);

        public IReadOnlyList<PrestigePointSource> GetPrestigePointSources()
        {
            if (_playerCivilization == null)
                return Array.Empty<PrestigePointSource>();

            var sources = new List<PrestigePointSource>();
            foreach (var city in _playerCivilization.Cities)
            {
                foreach (var building in city.Buildings)
                {
                    var points = GetBuildingPrestigePoints(building);
                    if (points > 0)
                        sources.Add(new PrestigePointSource(building.NameKey, points));
                }
            }
            return sources;
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

        public void PerformPrestige(MainGameState mainGameState, IslandParameters nextIslandParameters)
        {
            if (!PrestigeIsAvailable())
                throw new InvalidOperationException("Prestige is not available.");
            if (mainGameState.PrestigeState == null)
                throw new InvalidOperationException("PrestigeState is not available.");

            var points = CalculatePrestigePoints();
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

            mainGameState.PrestigeState.IslandState = new IslandState(map, civilizations, nextIslandParameters.IslandID);
        }
    }

    public readonly record struct PrestigePointSource(string LabelKey, int Points);
}
