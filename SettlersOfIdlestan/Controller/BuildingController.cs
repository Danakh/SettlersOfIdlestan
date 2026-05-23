using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System;
using System.Collections.Generic;
using System.Linq;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// Contr�le la logique de construction et d'am�lioration des b�timents pour une ville donn�e.
    /// API similaire � RoadController / CityBuilderController.
    /// </summary>
    public class BuildingController
    {
        private IslandState? _state;

        internal BuildingController(IslandState? state = null)
        {
            _state = state;
        }

        /// <summary>
        /// Initialize or update the IslandState for this controller.
        /// </summary>
        internal void Initialize(IslandState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Retourne la liste des b�timents constructibles ou am�liorables pour la ville sp�cifi�e.
        /// La m�thode renvoie des instances prototypes de niveau 0 pour les b�timents non construits.
        /// </summary>
        public List<Building> GetBuildingsAndBuildables(int civilizationIndex, Vertex cityVertex)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            var city = civ.Cities.FirstOrDefault(ct => ct.Position.Equals(cityVertex))
                       ?? throw new ArgumentException("City not found at the specified vertex", nameof(cityVertex));

            var result = new List<Building>();

            foreach (BuildingType bt in Enum.GetValues(typeof(BuildingType)))
            {
                var existing = city.Buildings.FirstOrDefault(b => b.Type == bt);
                if (existing != null)
                {
                    result.Add(existing);
                }
                else
                {
                    var prototype = CreateBuilding(bt);
                    if ((prototype != null) &&
                        prototype.IsBuildingAvailableForCity(_state.Map, city))
                    {
                        result.Add(prototype);
                    }
                }
            }

            // sort the result by available level
            result.Sort((a, b) => a.AvailableAtLevel.CompareTo(b.AvailableAtLevel));

            return result;
        }

        /// <summary>
        /// Construit (ou am�liore) un b�timent dans la ville sp�cifi�e.
        /// Lance InvalidOperationException si pas assez de ressources ou si l'action n'est pas permise.
        /// </summary>
        public bool BuildBuilding(int civilizationIndex, Vertex cityVertex, BuildingType type)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            var city = civ.Cities.FirstOrDefault(ct => ct.Position.Equals(cityVertex))
                       ?? throw new ArgumentException("City not found at the specified vertex", nameof(cityVertex));

            var existing = city.Buildings.FirstOrDefault(b => b.Type == type);

            ResourceCost cost;
            Building resultBuilding;
            if (existing == null)
            {
                var prototype = CreateBuilding(type) ?? throw new ArgumentException("Unknown building type", nameof(type));

                if (!prototype.IsBuildingAvailableForCity(_state.Map, city))
                    return false;

                cost = prototype.GetBuildCost();
                resultBuilding = prototype;
            }
            else
            {
                if (existing.Level >= GetMaxLevel(existing, civilizationIndex))
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
                resultBuilding.Level = 1;
                city.Buildings.Add(resultBuilding);
            }
            else
            {
                existing.Level += 1;
            }

            return true;
        }

        public int GetMaxLevel(Building building, int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            int maxLevel = building.GetDefaultMaxLevel();
            maxLevel = civ.TechnologyTree.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, building.Type.ToString(), maxLevel);
            return maxLevel;
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
                BuildingType.Mine => new Mine(),
                BuildingType.Seaport => new Seaport(),
                BuildingType.Warehouse => new Warehouse(),
                BuildingType.Forge => new Forge(),
                BuildingType.Library => new Library(),
                BuildingType.Temple => new Temple(),
                BuildingType.BuildersGuild => new BuildersGuild(),
                BuildingType.Laboratory => new Laboratory(),
                BuildingType.Barracks => new Barracks(),
                BuildingType.GlassWorks => new GlassWorks(),
                _ => null,
            };
        }
    }
}
