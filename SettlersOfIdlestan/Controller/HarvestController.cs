using System;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// Arguments d'événement pour une récolte complétée (manuelle ou automatique).
    /// </summary>
    public class HarvestCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// Index de la civilisation qui a récolté.
        /// </summary>
        public int CivilizationIndex { get; set; }

        /// <summary>
        /// Coordonnées de l'hexagone récolté.
        /// </summary>
        public HexCoord HexCoord { get; set; }

        /// <summary>
        /// Type de ressource récolté.
        /// </summary>
        public Resource Resource { get; set; }

        /// <summary>
        /// Indique si c'est une récolte automatique ou manuelle.
        /// </summary>
        public bool IsAutomatic { get; set; }

        /// <summary>
        /// Position du vertex de la ville qui a récolté.
        /// </summary>
        public Vertex CityPosition { get; set; }

        public HarvestCompletedEventArgs(int civIndex, HexCoord hex, Resource resource, Vertex cityPosition, bool isAutomatic = false)
        {
            CivilizationIndex = civIndex;
            HexCoord = hex;
            Resource = resource;
            IsAutomatic = isAutomatic;
            CityPosition = cityPosition;
        }
    }

    /// <summary>
    /// Controller to handle manual harvesting by civilizations.
    /// A civilization can manually harvest resources from hexes adjacent to one of its cities,
    /// subject to a cooldown of 2 seconds (in-game time) enforced via the GameClock.
    /// </summary>
    public class HarvestController
    {
        private IslandState? _state;
        private GameClock? _clock;
        private static readonly TimeSpan HarvestCooldown = TimeSpan.FromSeconds(2);
        // Cooldown for automatic production harvests triggered by producer buildings
        private static readonly TimeSpan AutomaticHarvestCooldown = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Événement déclenché quand une récolte (manuelle ou automatique) est complétée avec succès.
        /// </summary>
        public event EventHandler<HarvestCompletedEventArgs>? OnHarvestCompleted;

        internal HarvestController(IslandState? state = null, GameClock? clock = null)
        {
            Initialize(state, clock);
        }

        /// <summary>
        /// Initialize or update the IslandState and GameClock for this controller.
        /// </summary>
        internal void Initialize(IslandState? state, GameClock? clock)
        {
            // Unsubscribe from old clock if it exists
            if (_clock != null)
            {
                _clock.Advanced -= OnClockAdvanced;
            }

            _state = state;
            _clock = clock;

            // Subscribe to new clock if provided
            if (_clock != null)
            {
                _clock.Advanced += OnClockAdvanced;
            }
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try
            {
                PerformAutomaticProductionHarvests();
            }
            catch
            {
                // swallow exceptions to avoid affecting clock propagation
            }
        }

        private TimeSpan GetAutoHarvestCooldown(Civilization civ)
        {
            double speedMultiplier = civ.TechnologyTree.ApplyModifiers(ECategory.HARVEST_SPEED, "", 1.0);
            return TimeSpan.FromSeconds(AutomaticHarvestCooldown.TotalSeconds / speedMultiplier);
        }

        private void PerformAutomaticProductionHarvests()
        {
            if (_state == null || _clock == null) return;
            
            // For each civilization and each of its cities, for each building that produces,
            // harvest adjacent hexes corresponding to the building's produced resource, subject to per-hex automatic cooldown.
            foreach (var civ in _state.Civilizations)
            {
                if (!_state.AutomaticHarvestLastTimesByCivilization.TryGetValue(civ.Index, out var autoMap))
                {
                    autoMap = new System.Collections.Generic.Dictionary<HexCoord, DateTimeOffset>();
                    _state.AutomaticHarvestLastTimesByCivilization[civ.Index] = autoMap;
                }

                var now = _clock.CurrentTime;

                foreach (var city in civ.Cities)
                {
                    foreach (var building in city.Buildings)
                    {
                        var hexes = city.Position.GetHexes();
                        foreach (var hex in hexes)
                        {
                            if (hex == null) continue;
                            var tile = _state.Map.GetTile(hex);
                            if (tile == null) continue;

                            Resource? resource = building.AutomaticHarvestCapability(tile.TerrainType);
                            if (resource != null)
                            {
                                // check automatic cooldown (adjusted by civilization harvest speed, building terrain multiplier, and level-based reduction)
                                var effectiveCooldown = GetAutoHarvestCooldown(civ) * building.GetAutomaticHarvestCooldownMultiplier(tile.TerrainType)
                                    - building.GetAutomaticHarvestCooldownReduction(tile.TerrainType);
                                if (autoMap.TryGetValue(hex, out var lastAuto) && now - lastAuto < effectiveCooldown)
                                {
                                    continue;
                                }
                                var res = resource.Value;
                                // perform harvest: add one unit
                                civ.AddResource(res, 1);
                                autoMap[hex] = now;
                                // Déclenche l'événement de récolte
                                OnHarvestCompleted?.Invoke(this, new HarvestCompletedEventArgs(civ.Index, hex, res, city.Position, isAutomatic: true));
                                // only harvest one hex per production entry per invocation
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Manually harvests resources for the civilization at the given index from a hex adjacent to one of its cities.
        /// The coord must be one of the three hexes surrounding the city's vertex.
        /// Returns true if harvest succeeded and resources were added, false otherwise.
        /// Throws ArgumentException if civilization not found or coord not adjacent to any city of the civ.
        /// </summary>
        /// <summary>
        /// Retourne la liste des ressources que la civilisation peut récolter manuellement
        /// sur l'hexagone donné, en fonction de ses bâtiments adjacents.
        /// </summary>
        public IReadOnlyList<Resource> GetManualHarvestableResources(int civilizationIndex, HexCoord hex)
        {
            if (_state == null) return Array.Empty<Resource>();

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex);
            if (civ == null) return Array.Empty<Resource>();

            var tile = _state.Map.GetTile(hex);
            if (tile == null) return Array.Empty<Resource>();

            var resources = new HashSet<Resource>();
            foreach (var city in civ.Cities.Where(c => c.Position.IsAdjacentTo(hex)))
                foreach (var building in city.Buildings)
                {
                    var res = building.ManualHarvestCapability(tile.TerrainType);
                    if (res.HasValue) resources.Add(res.Value);
                }

            return resources.ToList();
        }

        /// <summary>
        /// Retourne la liste des ressources que la civilisation peut récolter automatiquement
        /// sur l'hexagone donné, en fonction de ses bâtiments adjacents.
        /// </summary>
        public IReadOnlyList<Resource> GetAutomaticHarvestableResources(int civilizationIndex, HexCoord hex)
        {
            if (_state == null) return Array.Empty<Resource>();

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex);
            if (civ == null) return Array.Empty<Resource>();

            var tile = _state.Map.GetTile(hex);
            if (tile == null) return Array.Empty<Resource>();

            var resources = new HashSet<Resource>();
            foreach (var city in civ.Cities.Where(c => c.Position.IsAdjacentTo(hex)))
                foreach (var building in city.Buildings)
                {
                    var res = building.AutomaticHarvestCapability(tile.TerrainType);
                    if (res.HasValue) resources.Add(res.Value);
                }

            return resources.ToList();
        }

        public TimeSpan GetManualHarvestCooldown(int civilizationIndex)
        {
            return HarvestCooldown;
        }

        public TimeSpan GetEffectiveAutoHarvestCooldown(int civilizationIndex, HexCoord hex)
        {
            if (_state == null) return AutomaticHarvestCooldown;

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex);
            if (civ == null) return AutomaticHarvestCooldown;

            var baseCooldown = GetAutoHarvestCooldown(civ);

            var tile = _state.Map.GetTile(hex);
            if (tile == null) return baseCooldown;

            TimeSpan? min = null;
            foreach (var city in civ.Cities.Where(c => c.Position.IsAdjacentTo(hex)))
                foreach (var building in city.Buildings)
                    if (building.AutomaticHarvestCapability(tile.TerrainType).HasValue)
                    {
                        var effective = baseCooldown * building.GetAutomaticHarvestCooldownMultiplier(tile.TerrainType)
                            - building.GetAutomaticHarvestCooldownReduction(tile.TerrainType);
                        if (min == null || effective < min) min = effective;
                    }

            return min ?? baseCooldown;
        }

        public bool ManualHarvest(int civilizationIndex, HexCoord hex)
        {
            if (_state == null || _clock == null) throw new InvalidOperationException("IslandState and GameClock have not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            // Verify cooldown per-hex using IslandState.HarvestLastTimesByCivilization
            var now = _clock.CurrentTime;
            var civMap = _state.HarvestLastTimesByCivilization;
            if (!civMap.TryGetValue(civilizationIndex, out var perHex))
            {
                perHex = new System.Collections.Generic.Dictionary<HexCoord, DateTimeOffset>();
                civMap[civilizationIndex] = perHex;
            }
            if (perHex.TryGetValue(hex, out var lastHarvest) && now - lastHarvest < HarvestCooldown)
            {
                return false; // still on cooldown for this hex
            }

            // Find all adjacent cities
            var cities = civ.Cities.Where(c => c.Position.IsAdjacentTo(hex)).ToList();
            if (cities.Count == 0)
                throw new ArgumentException("Specified hex is not adjacent to any city of the civilization", nameof(hex));

            // Verify the hex exists and has a resource
            var tile = _state.Map.GetTile(hex);
            if (tile == null) return false;

            List<Resource> manualHarvestResources = new List<Resource>();
            foreach (var city in cities)
            {
                foreach (var building in city.Buildings)
                {
                    Resource? resource = building.ManualHarvestCapability(tile.TerrainType);
                    if (resource != null)
                    {
                        var res = resource.Value;
                        if (!manualHarvestResources.Contains(res))
                        {
                            manualHarvestResources.Add(res); 

                            // Add one unit of the resource to the civilization
                            civ.AddResource(res, 1);

                            // Déclenche l'événement de récolte
                            OnHarvestCompleted?.Invoke(this, new HarvestCompletedEventArgs(civilizationIndex, hex, res, city.Position, isAutomatic: false));
                        }
                    }
                }
            }

            if (manualHarvestResources.Count == 0) return false;

            // Update last harvest time for this hex so cooldown persists in the model
            perHex[hex] = now;

            return true;
        }
    }
}
