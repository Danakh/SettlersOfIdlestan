using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System;
using System.Collections.Generic;
using System.Linq;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Island
{
    public class BuildingBuiltEventArgs : EventArgs
    {
        public City City { get; }
        public BuildingType BuildingType { get; }
        public int Level { get; }
        public bool IsNewBuilding { get; }

        public BuildingBuiltEventArgs(City city, BuildingType type, int level, bool isNewBuilding)
        {
            City = city;
            BuildingType = type;
            Level = level;
            IsNewBuilding = isNewBuilding;
        }
    }

    /// <summary>
    /// Contr�le la logique de construction et d'am�lioration des b�timents pour une ville donn�e.
    /// API similaire � RoadController / CityBuilderController.
    /// </summary>
    public class BuildingController
    {
        private WorldState? _state;
        private GameClock? _clock;

        public event EventHandler<BuildingBuiltEventArgs>? OnBuildingBuilt;

        internal BuildingController(WorldState? state = null)
        {
            _state = state;
        }

        /// <summary>
        /// Initialize or update the WorldState for this controller.
        /// </summary>
        internal void Initialize(WorldState state, GameClock? clock = null)
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
            try { PerformArtisansGuildAutomation(); }
            catch { }
            try { PerformAcademyAutomation(); }
            catch { }
            try { PerformTraderGuildAutomation(); }
            catch { }
            try { TryInitializeUnderworld(); }
            catch { }
        }

        private void TryInitializeUnderworld()
        {
            if (_state == null || _state.Layers.ContainsKey(LayerState.UnderworldZ)) return;

            var playerCiv = _state.PlayerCivilization;
            bool hasDeepestMine = false;
            foreach (var city in playerCiv.Cities)
                foreach (var building in city.Buildings)
                    if (building.Type == BuildingType.DeepestMine && building.Level > 0)
                    { hasDeepestMine = true; break; }

            if (!hasDeepestMine) return;

            var underworldLayer = LayerState.EstablishOupostInNewAutoExpandLayer(playerCiv);
            _state.AddLayer(LayerState.UnderworldZ, underworldLayer);
            _state.Visibility.RecalculateFor(playerCiv.Index);
        }

        private void PerformHarvestersGuildProductionAutomation()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;
            BuildingType[] targets = [BuildingType.Sawmill, BuildingType.Brickworks, BuildingType.Quarry, BuildingType.Mill];

            foreach (var civ in _state.Civilizations)
            {
                var guild = civ.Cities.SelectMany(c => c.Buildings).OfType<HarvestersGuild>().FirstOrDefault();
                if (guild == null || guild.Level == 0) continue;

                bool isPlayer = civ.Index == _state.PlayerCivilization.Index;
                bool enabled = !isPlayer || _state.AutomationSettings.ProductionBuildingAutomationEnabled;
                long tick = guild.LastProductionBuildTick;
                TickGuildAutomation(civ, ref tick, guild.GetAutoProductionCooldownTicks(), enabled, targets, now);
                guild.LastProductionBuildTick = tick;
            }
        }

        private void PerformArtisansGuildAutomation()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;
            BuildingType[] targets = [BuildingType.Forge, BuildingType.Warehouse];

            foreach (var civ in _state.Civilizations)
            {
                var guild = civ.Cities.SelectMany(c => c.Buildings).OfType<ArtisansGuild>().FirstOrDefault();
                if (guild == null || guild.Level == 0) continue;

                bool isPlayer = civ.Index == _state.PlayerCivilization.Index;
                bool enabled = !isPlayer || _state.AutomationSettings.ArtisanBuildingAutomationEnabled;
                long tick = guild.LastArtisanBuildTick;
                TickGuildAutomation(civ, ref tick, guild.GetAutoArtisanCooldownTicks(), enabled, targets, now);
                guild.LastArtisanBuildTick = tick;
            }
        }

        private void PerformAcademyAutomation()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;
            BuildingType[] targets = [BuildingType.Library];

            foreach (var civ in _state.Civilizations)
            {
                var academy = civ.Cities.SelectMany(c => c.Buildings).OfType<Academy>().FirstOrDefault();
                if (academy == null || academy.Level == 0) continue;

                bool isPlayer = civ.Index == _state.PlayerCivilization.Index;
                bool enabled = !isPlayer || _state.AutomationSettings.LibraryBuildingAutomationEnabled;
                long tick = academy.LastLibraryBuildTick;
                TickGuildAutomation(civ, ref tick, academy.GetAutoLibraryCooldownTicks(), enabled, targets, now);
                academy.LastLibraryBuildTick = tick;
            }
        }

        private void PerformTraderGuildAutomation()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;
            BuildingType[] targets = [BuildingType.Market];

            foreach (var civ in _state.Civilizations)
            {
                var guild = civ.Cities.SelectMany(c => c.Buildings).OfType<TraderGuild>().FirstOrDefault();
                if (guild == null || guild.Level == 0) continue;

                bool isPlayer = civ.Index == _state.PlayerCivilization.Index;
                bool enabled = !isPlayer || _state.AutomationSettings.MarketBuildingAutomationEnabled;
                long tick = guild.LastMarketBuildTick;
                TickGuildAutomation(civ, ref tick, guild.GetAutoMarketCooldownTicks(), enabled, targets, now);
                guild.LastMarketBuildTick = tick;
            }
        }

        private void TickGuildAutomation(
            Model.Civilization.Civilization civ,
            ref long lastTick,
            long cooldown,
            bool enabled,
            BuildingType[] targets,
            long now)
        {
            if (!enabled) { lastTick = now; return; }
            if (lastTick == 0) { lastTick = now; return; }
            if (now - lastTick < cooldown) return;

            lastTick = now;

            // New builds first, then upgrade lowest-level existing buildings
            foreach (var city in civ.Cities)
                foreach (var type in targets)
                    if (!city.Buildings.Any(b => b.Type == type) && BuildBuilding(city, type))
                        return;

            foreach (int level in new[] { 1, 2, 3, 4, 5 })
                foreach (var city in civ.Cities)
                    foreach (var type in targets)
                        if (city.Buildings.Any(b => b.Type == type && b.Level == level) && BuildBuilding(city, type))
                            return;
        }

        /// <summary>
        /// Retourne la liste des b�timents constructibles ou am�liorables pour la ville sp�cifi�e.
        /// La m�thode renvoie des instances prototypes de niveau 0 pour les b�timents non construits.
        /// </summary>
        public List<Building> GetBuildingsAndBuildables(City city)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == city.CivilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(city.CivilizationIndex));

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
                    if ((GetMaxLevel(prototype, city.CivilizationIndex) > 0) &&
                        prototype.IsBuildingAvailableForCity(_state.GetMapFor(city.Position), city))
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
        public bool BuildBuilding(City city, BuildingType type)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == city.CivilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(city.CivilizationIndex));

            var existing = city.Buildings.FirstOrDefault(b => b.Type == type);

            ResourceSet cost;
            Building resultBuilding;
            if (existing == null)
            {
                var prototype = CreateBuilding(type) ?? throw new ArgumentException("Unknown building type", nameof(type));

                if (!prototype.IsBuildingAvailableForCity(_state.GetMapFor(city.Position), city))
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
                if (existing.Level >= GetMaxLevel(existing, city.CivilizationIndex))
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
                if (type == BuildingType.TownHall) city.InvalidateLevelCache();
                int defBonus = resultBuilding.GetDefenseBonus();
                if (defBonus > 0 && civ.ModifierAggregator.HasModifier(ECategory.BUILDING_DEFENSE_ON_CONSTRUCT))
                    city.CurrentDefense += defBonus;
                if (resultBuilding.IsUnique && !civ.UniqueBuildings.Contains(resultBuilding.Type))
                    civ.AddUniqueBuilding(resultBuilding.Type);
                if (resultBuilding is IUniqueBuilding)
                    civ.RebuildUniqueBuildingsModifiers();
            }
            else
            {
                int oldDefBonus = existing.GetDefenseBonus();
                existing.Level += 1;
                int defDelta = existing.GetDefenseBonus() - oldDefBonus;
                if (defDelta > 0 && civ.ModifierAggregator.HasModifier(ECategory.BUILDING_DEFENSE_ON_CONSTRUCT))
                    city.CurrentDefense += defDelta;
                if (existing is IUniqueBuilding)
                    civ.RebuildUniqueBuildingsModifiers();
            }

            if (type == BuildingType.Watchtower)
                _state.Visibility.RecalculateFor(city.CivilizationIndex);

            OnBuildingBuilt?.Invoke(this, new BuildingBuiltEventArgs(
                city, type, resultBuilding.Level, existing == null));

            return true;
        }

        /// <summary>
        /// Retourne la liste des bâtiments uniques disponibles ou déjà construits pour la ville spécifiée.
        /// Les bâtiments déjà construits (dans n'importe quelle ville de la civ) sont toujours inclus.
        /// Les bâtiments non construits sont inclus uniquement si la ville sélectionnée est niveau 4.
        /// </summary>
        public List<Building> GetUniqueBuildingsAndBuildables(City city)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == city.CivilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(city.CivilizationIndex));

            var result = new List<Building>();

            foreach (BuildingType bt in Enum.GetValues(typeof(BuildingType)))
            {
                var prototype = CreateBuilding(bt);
                if (prototype == null || !prototype.IsUnique || GetMaxLevel(prototype, city.CivilizationIndex) <= 0)
                    continue;

                bool isBuilt = civ.UniqueBuildings.Contains(bt)
                               || civ.Cities.Any(c => c.Buildings.Any(b => b.Type == bt));

                if (isBuilt)
                {
                    var instance = civ.Cities.SelectMany(c => c.Buildings).FirstOrDefault(b => b.Type == bt)
                                   ?? prototype;
                    result.Add(instance);
                }
                else if (prototype.IsBuildingAvailableForCity(_state.GetMapFor(city.Position), city))
                {
                    result.Add(prototype);
                }
            }

            return result;
        }

        public int GetMaxLevel(Building building, int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            int maxLevel = building.GetDefaultMaxLevel();
            maxLevel = civ.ModifierAggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, building.Type.ToString(), maxLevel);
            return maxLevel;
        }

        public static Building? CreateBuilding(BuildingType type)
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
                BuildingType.ArtisansGuild => new ArtisansGuild(),
                BuildingType.Watchtower => new Watchtower(),
                BuildingType.Academy => new Academy(),
                BuildingType.TraderGuild => new TraderGuild(),
                BuildingType.MilitaryAcademy => new MilitaryAcademy(),
                BuildingType.DeepestMine => new DeepestMine(),
                BuildingType.Smelter => new Smelter(),
                _ => null,
            };
        }
    }
}
