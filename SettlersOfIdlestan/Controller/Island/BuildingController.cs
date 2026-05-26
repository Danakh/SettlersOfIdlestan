using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System;
using System.Collections.Generic;
using System.Linq;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Island
{
    /// <summary>
    /// Contr�le la logique de construction et d'am�lioration des b�timents pour une ville donn�e.
    /// API similaire � RoadController / CityBuilderController.
    /// </summary>
    public class BuildingController
    {
        private IslandState? _state;
        private GameClock? _clock;

        internal BuildingController(IslandState? state = null)
        {
            _state = state;
        }

        /// <summary>
        /// Initialize or update the IslandState for this controller.
        /// </summary>
        internal void Initialize(IslandState state, GameClock? clock = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state ?? throw new ArgumentNullException(nameof(state));
            _clock = clock;

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { PerformHarvestersGuildProductionAutomation(); }
            catch { }
        }

        private void PerformHarvestersGuildProductionAutomation()
        {
            if (_state == null || _clock == null) return;

            long now = _clock.CurrentTick;

            foreach (var civ in _state.Civilizations)
            {
                HarvestersGuild? guild = null;
                foreach (var city in civ.Cities)
                {
                    guild = city.Buildings.OfType<HarvestersGuild>().FirstOrDefault();
                    if (guild != null) break;
                }

                if (guild == null || guild.Level == 0) continue;

                bool isPlayerCiv = civ.Index == _state.PlayerCivilization.Index;
                if (isPlayerCiv && !_state.AutomationSettings.ProductionBuildingAutomationEnabled)
                {
                    guild.LastProductionBuildTick = now;
                    continue;
                }

                if (guild.LastProductionBuildTick == 0)
                {
                    guild.LastProductionBuildTick = now;
                    continue;
                }

                if (now - guild.LastProductionBuildTick < guild.GetAutoProductionCooldownTicks()) continue;

                guild.LastProductionBuildTick = now;

                BuildingType[] productionTypes = [BuildingType.Sawmill, BuildingType.Brickworks, BuildingType.Quarry, BuildingType.Mill];

                // Prioritise upgrades of existing buildings, then new builds
                foreach (var city in civ.Cities)
                    foreach (var type in productionTypes)
                        if (city.Buildings.Any(b => b.Type == type) && BuildBuilding(civ.Index, city.Position, type))
                            goto NextCiv;

                foreach (var city in civ.Cities)
                    foreach (var type in productionTypes)
                        if (!city.Buildings.Any(b => b.Type == type) && BuildBuilding(civ.Index, city.Position, type))
                            goto NextCiv;

                NextCiv:;
            }
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
                var prototype = CreateBuilding(bt);
                if (prototype == null || prototype.IsUnique)
                    continue;

                var existing = city.Buildings.FirstOrDefault(b => b.Type == bt);
                if (existing != null)
                {
                    result.Add(existing);
                }
                else
                {
                    if ((GetMaxLevel(prototype, civilizationIndex) > 0) &&
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

            ResourceSet cost;
            Building resultBuilding;
            if (existing == null)
            {
                var prototype = CreateBuilding(type) ?? throw new ArgumentException("Unknown building type", nameof(type));

                if (!prototype.IsBuildingAvailableForCity(_state.Map, city))
                    return false;

                if (prototype.IsUnique &&
                    (civ.UniqueBuildings.Contains(type) || civ.Cities.Any(c => c.Buildings.Any(b => b.Type == type))))
                    return false;

                if (prototype.IsUnique && city.Buildings.Any(b => b.IsUnique))
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
                if (resultBuilding.IsUnique && !civ.UniqueBuildings.Contains(resultBuilding.Type))
                    civ.UniqueBuildings.Add(resultBuilding.Type);
            }
            else
            {
                existing.Level += 1;
            }

            return true;
        }

        /// <summary>
        /// Retourne la liste des bâtiments uniques disponibles ou déjà construits pour la ville spécifiée.
        /// Les bâtiments déjà construits (dans n'importe quelle ville de la civ) sont toujours inclus.
        /// Les bâtiments non construits sont inclus uniquement si la ville sélectionnée est niveau 4.
        /// </summary>
        public List<Building> GetUniqueBuildingsAndBuildables(int civilizationIndex, Vertex cityVertex)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            var city = civ.Cities.FirstOrDefault(ct => ct.Position.Equals(cityVertex))
                       ?? throw new ArgumentException("City not found at the specified vertex", nameof(cityVertex));

            var result = new List<Building>();

            foreach (BuildingType bt in Enum.GetValues(typeof(BuildingType)))
            {
                var prototype = CreateBuilding(bt);
                if (prototype == null || !prototype.IsUnique) continue;

                bool isBuilt = civ.UniqueBuildings.Contains(bt)
                               || civ.Cities.Any(c => c.Buildings.Any(b => b.Type == bt));

                if (isBuilt)
                {
                    var instance = civ.Cities.SelectMany(c => c.Buildings).FirstOrDefault(b => b.Type == bt)
                                   ?? prototype;
                    result.Add(instance);
                }
                else if (prototype.IsBuildingAvailableForCity(_state.Map, city))
                {
                    result.Add(prototype);
                }
            }

            return result;
        }

        public int GetMaxLevel(Building building, int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            int maxLevel = building.GetDefaultMaxLevel();
            maxLevel = civ.ModifierAggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, building.Type.ToString(), maxLevel);
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
                BuildingType.Quarry => new Quarry(),
                BuildingType.Seaport => new Seaport(),
                BuildingType.Warehouse => new Warehouse(),
                BuildingType.Forge => new Forge(),
                BuildingType.Library => new Library(),
                BuildingType.Temple => new Temple(),
                BuildingType.BuildersGuild => new BuildersGuild(),
                BuildingType.Laboratory => new Laboratory(),
                BuildingType.Barracks => new Barracks(),
                BuildingType.GlassWorks => new GlassWorks(),
                BuildingType.Palisade => new Palisade(),
                BuildingType.ImperialPort => new ImperialPort(),
                BuildingType.HarvestersGuild => new HarvestersGuild(),
                _ => null,
            };
        }
    }
}
