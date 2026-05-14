using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model;
using SettlersOfIdlestan.Model.City;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// Controller handling city construction.
    /// </summary>
    public class CityBuilderController
    {
        private IslandState? _state;

        internal CityBuilderController(IslandState? state = null)
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
        /// Returns vertices where the civilization can build a city (outpost).
        /// Rules (simple):
        /// - vertex not already occupied by any city
        /// - vertex touches at least one road of the civilization
        /// - no existing city of the civilization is at distance 1 (shares 2 hexes)
        /// - touching roads must have DistanceToNearestCity >= 2
        /// </summary>
        public List<Vertex> GetBuildableVertices(int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            // Build vertex → touching roads map directly from the civilization's roads
            var vertexRoads = new Dictionary<Vertex, List<Model.Road.Road>>();
            foreach (var road in civ.Roads)
            {
                foreach (var v in road.Position.GetVertices())
                {
                    if (!vertexRoads.TryGetValue(v, out var list))
                    {
                        list = new List<Model.Road.Road>();
                        vertexRoads[v] = list;
                    }
                    list.Add(road);
                }
            }

            var occupied = new HashSet<Vertex>(_state.Civilizations.SelectMany(c => c.Cities).Select(ct => ct.Position));

            var result = new List<Vertex>();
            foreach (var (v, touchingRoads) in vertexRoads)
            {
                if (occupied.Any(o => o.Equals(v))) continue;

                // ensure no existing city is at distance 1 (shares 2 hexes)
                if (civ.Cities.Any(city => SharedHexCount(city.Position, v) >= 2)) continue;

                // ensure touching roads are not adjacent to a city (distance 1)
                if (touchingRoads.Any(tr => tr.DistanceToNearestCity == 1)) continue;

                result.Add(v);
            }

            return result;
        }

        /// <summary>
        /// Build a city at the given vertex. Cost: 10 Brick, 10 Wood, 10 Wheat, 10 Sheep.
        /// Throws InvalidOperationException if not enough resources or vertex not buildable.
        /// </summary>
        public City BuildCity(int civilizationIndex, Vertex vertex)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            var buildable = GetBuildableVertices(civilizationIndex);
            if (!buildable.Any(v => v.Equals(vertex)))
                throw new InvalidOperationException("Vertex not buildable by this civilization");

            var cost = new ResourceCost
            {
                { Resource.Brick, 10 },
                { Resource.Wood, 10 },
                { Resource.Wheat, 10 },
                { Resource.Sheep, 10 }
            };

            foreach (var kvp in cost)
            {
                if (civ.GetResourceQuantity(kvp.Key) < kvp.Value)
                    throw new InvalidOperationException("Not enough resources to build the city");
            }

            foreach (var kvp in cost)
            {
                civ.RemoveResource(kvp.Key, kvp.Value);
            }

            var city = new City(vertex) { CivilizationIndex = civilizationIndex };
            civ.Cities.Add(city);
            return city;
        }

        private static int SharedHexCount(Vertex a, Vertex b)
        {
            var ah = a.GetHexes();
            var bh = b.GetHexes();
            int count = 0;
            foreach (var x in ah)
                foreach (var y in bh)
                    if (x.Equals(y)) count++;
            return count;
        }
    }
}
