using System;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Buildings;

namespace SettlersOfIdlestan.Controller
{
    public class PrestigeController
    {
        private Civilization _playerCivilization;

        internal PrestigeController()
        {
            // no op
        }

        internal void Initialize(Civilization playerCivilization)
        {
            _playerCivilization = playerCivilization;
        }

        public int CalculatePrestigePoints()
        {
            // Simple prestige calculation based on the number of cities and buildings
            int points = 0;

            // Each building contributes points based on its type and level
            foreach (var city in _playerCivilization.Cities)
            {
                foreach (var building in city.Buildings)
                {
                    points += GetBuildingPrestigePoints(building);
                }
            }
            return points;
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
    }
}
