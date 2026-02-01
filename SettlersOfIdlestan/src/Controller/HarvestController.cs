using System;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// Controller to handle manual harvesting by civilizations.
    /// A civilization can manually harvest resources from hexes adjacent to one of its cities,
    /// subject to a cooldown of 2 seconds (in-game time) enforced via the GameClock.
    /// </summary>
    public class HarvestController
    {
        private readonly IslandState _state;
        private readonly GameClock _clock;
        private static readonly TimeSpan HarvestCooldown = TimeSpan.FromSeconds(2);

        internal HarvestController(IslandState state, GameClock clock)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>
        /// Manually harvests resources for the civilization at the given index from a hex adjacent to one of its cities.
        /// The coord must be one of the three hexes surrounding the city's vertex.
        /// Returns true if harvest succeeded and resources were added, false otherwise.
        /// Throws ArgumentException if civilization not found or coord not adjacent to any city of the civ.
        /// </summary>
        public bool ManualHarvest(int civilizationIndex, HexCoord hex)
        {
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

            return true;
        }
    }
}
