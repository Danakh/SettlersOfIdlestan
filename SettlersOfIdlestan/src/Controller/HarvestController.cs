using System;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;

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

        public HarvestCompletedEventArgs(int civIndex, HexCoord hex, Resource resource, bool isAutomatic = false)
        {
            CivilizationIndex = civIndex;
            HexCoord = hex;
            Resource = resource;
            IsAutomatic = isAutomatic;
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
                        // Buildings without production skip
                        if (building.Production == null || building.Production.Count == 0) continue;

                        // For each produced resource, attempt to harvest one adjacent hex that contains this resource.
                        foreach (var prod in building.Production)
                        {
                            var resource = prod.Key;
                            // find an adjacent tile of the city that provides this resource
                            var hexes = city.Position.GetHexes();
                            foreach (var hex in hexes)
                            {
                                if (hex == null) continue;
                                var tile = _state.Map.GetTile(hex);
                                if (tile == null || tile.Resource == null) continue;
                                if (tile.Resource.Value != resource) continue;

                                // check automatic cooldown
                                if (autoMap.TryGetValue(hex, out var lastAuto) && now - lastAuto < AutomaticHarvestCooldown)
                                {
                                    continue;
                                }

                                // perform harvest: add one unit
                                civ.AddResource(resource, 1);
                                autoMap[hex] = now;
                                // Déclenche l'événement de récolte
                                OnHarvestCompleted?.Invoke(this, new HarvestCompletedEventArgs(civ.Index, hex, resource, isAutomatic: true));
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

            // Find a city that is adjacent to the hex (i.e., city vertex contains the hex)
            var city = civ.Cities.FirstOrDefault(c => c.Position.IsAdjacentTo(hex));
            if (city == null)
                throw new ArgumentException("Specified hex is not adjacent to any city of the civilization", nameof(hex));

            // Verify the hex exists and has a resource
            var tile = _state.Map.GetTile(hex);
            if (tile == null) return false;
            var resource = tile.Resource;
            if (resource == null) return false;

            // Add one unit of the resource to the civilization
            civ.AddResource(resource.Value, 1);

            // Update last harvest time for this hex so cooldown persists in the model
            perHex[hex] = now;

            // Déclenche l'événement de récolte
            OnHarvestCompleted?.Invoke(this, new HarvestCompletedEventArgs(civilizationIndex, hex, resource.Value, isAutomatic: false));

            return true;
        }
    }
}
