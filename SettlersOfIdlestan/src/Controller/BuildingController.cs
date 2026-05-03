using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.City;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// Contrôle la logique de construction et d'amélioration des bâtiments pour une ville donnée.
    /// API similaire à RoadController / CityBuilderController.
    /// </summary>
    public class BuildingController
    {
        private readonly IslandState _state;

        internal BuildingController(IslandState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Retourne la liste des bâtiments constructibles ou améliorables pour la ville spécifiée.
        /// La méthode renvoie des instances prototypes dont la propriété Level indique le niveau visé
        /// (1 pour construction, >1 pour amélioration).
        /// </summary>
        public List<Building> GetBuildableBuildings(int civilizationIndex, Vertex cityVertex)
        {
            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            var city = civ.Cities.FirstOrDefault(ct => ct.Position.Equals(cityVertex))
                       ?? throw new ArgumentException("City not found at the specified vertex", nameof(cityVertex));

            var result = new List<Building>();

            foreach (BuildingType bt in Enum.GetValues(typeof(BuildingType)))
            {
                var prototype = CreateBuilding(bt);
                if (prototype == null) continue;

                // Check if building is available for this city
                if (!prototype.IsBuildingAvailableForCity(_state.Map, city)) continue;

                // Check if city already has this building
                var existing = city.Buildings.FirstOrDefault(b => b.Type == bt);
                if (existing == null)
                {
                    result.Add(prototype);
                }
            }

            // sort the result by available level
            result.Sort((a, b) => a.AvailableAtLevel.CompareTo(b.AvailableAtLevel));

            return result;
        }

        /// <summary>
        /// Construit (ou améliore) un bâtiment dans la ville spécifiée.
        /// Lance InvalidOperationException si pas assez de ressources ou si l'action n'est pas permise.
        /// </summary>
        public bool BuildBuilding(int civilizationIndex, Vertex cityVertex, BuildingType type)
        {
            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            var city = civ.Cities.FirstOrDefault(ct => ct.Position.Equals(cityVertex))
                       ?? throw new ArgumentException("City not found at the specified vertex", nameof(cityVertex));

            var prototype = CreateBuilding(type) ?? throw new ArgumentException("Unknown building type", nameof(type));

            if (!prototype.IsBuildingAvailableForCity(_state.Map, city))
                return false;

            var existing = city.Buildings.FirstOrDefault(b => b.Type == type);

            Dictionary<Resource, int> cost;
            Building resultBuilding;
            if (existing == null)
            {
                cost = prototype.GetBuildCost();
                resultBuilding = prototype;
            }
            else
            {
                if (existing.Level >= existing.MaxLevel)
                    return false;

                cost = existing.GetUpgradeCost(existing.Level + 1);
                resultBuilding = existing;
            }

            // check resources
            foreach (var kv in cost)
            {
                if (civ.GetResourceQuantity(kv.Key) < kv.Value)
                {
                    return false;
                }
            }

            // consume resources
            foreach (var kv in cost)
            {
                civ.RemoveResource(kv.Key, kv.Value);
            }

            if (existing == null)
            {
                city.Buildings.Add(resultBuilding);
            }
            else
            {
                existing.Level += 1;
            }

            return true;
        }

        private static Building? CreateBuilding(BuildingType type)
        {
            return type switch
            {
                BuildingType.TownHall => new TownHall(),
                BuildingType.Market => new Market(),
                BuildingType.Sawmill => new Sawmill(),
                BuildingType.Brickworks => new Brickworks(),
                BuildingType.Mill => new Mill(),
                BuildingType.Sheepfold => new Sheepfold(),
                BuildingType.Mine => new Mine(),
                BuildingType.Seaport => new Seaport(),
                BuildingType.Warehouse => new Warehouse(),
                BuildingType.Forge => new Forge(),
                BuildingType.Library => new Library(),
                BuildingType.Temple => new Temple(),
                BuildingType.BuildersGuild => new BuildersGuild(),
                _ => null,
            };
        }
    }
}
