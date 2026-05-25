using System;
using System.Collections.Generic;
using System.Linq;
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
            var vertices = new List<Vertex>();
            foreach (var road in civ.Roads)
            {
                foreach (var v in road.Position.GetVertices())
                {
                    if (!vertices.Any(vr => vr.Equals(v)))
                        vertices.Add(v);
                }
            }

            // now we filter vertices that aren't far enough from any city using MinDistanceBetweenCities and MinDistanceBetweenCivilizationCities
            vertices = vertices.Where(v =>
                !_state.Civilizations.Any(c => c.Cities.Any(city => city.Position.EdgeDistanceTo(v) < MinDistanceBetweenCities)) &&
                !civ.Cities.Any(city => city.Position.EdgeDistanceTo(v) < MinDistanceBetweenCivilizationCities))
                .ToList();

            return vertices;
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

            var cost = NewCityBuildingCost();

            if (!civ.CanPayResourceCost(cost))
                throw new InvalidOperationException("Not enough resources to build the city");

            civ.PayResourceCost(cost);

            var city = new City(vertex) { CivilizationIndex = civilizationIndex };
            civ.Cities.Add(city);
            _state.RecalculateVisibleIslandMap(civilizationIndex);

            var cityHexSet = new HashSet<HexCoord>(city.Position.GetHexes());
            foreach (var trove in _state.TreasureTroves)
            {
                if (!trove.Claimed && cityHexSet.Contains(trove.Position))
                {
                    trove.Claimed = true;
                    civ.AddResource(Resource.Gold, 10);
                    _state.EventLog.Add(trove.RemovedEventType);
                }
            }

            return city;
        }

        public ResourceCost NewCityBuildingCost()
        {
            return new ResourceCost
            {
                { Resource.Brick, 10 },
                { Resource.Wood, 10 },
                { Resource.Food, 15 },
            };
        }

        public int MinDistanceBetweenCities => 1;
        public int MinDistanceBetweenCivilizationCities => 3;
    }
}
