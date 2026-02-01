using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Road;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// Contr?le la logique de construction de routes pour un IslandState.
    /// </summary>
    public class RoadController
    {
        private readonly IslandState _state;

        internal RoadController(IslandState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Retourne la liste des routes constructibles pour la civilisation d'indice sp?cifi?.
        /// R?gle: une ar?te est constructible si elle n'est pas d?j? occup?e par une route,
        /// et si un de ses deux vertex contient une ville de la civilisation, ou si une route
        /// existante de la civilisation touche ce vertex.
        /// </summary>
        public List<Road> GetBuildableRoads(int civilizationIndex)
        {
            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            // Recompute distances for existing roads of the civilization so we can expose costs
            ComputeRoadDistancesForCivilization(civ);

            // Collecte toutes les ar?tes g?om?triques de la carte
            var edges = new HashSet<Edge>();
            foreach (var tile in _state.Map.Tiles.Values)
            {
                foreach (var dir in HexDirectionUtils.AllHexDirections)
                {
                    edges.Add(tile.Coord.Edge(dir));
                }
            }

            // Routes d?j? pr?sentes (toutes civilisations confondues)
            var occupied = new HashSet<Edge>(_state.Civilizations.SelectMany(c => c.Roads).Select(r => r.Position));

            var result = new List<Road>();
            foreach (var edge in edges)
            {
                if (occupied.Any(e => e.Equals(edge))) continue; // deja occup?e

                var vertices = edge.GetVertices();

                bool buildable = false;
                foreach (var vertex in vertices)
                {
                    // Si un city appartient ? la civilisation sur ce vertex
                    if (civ.Cities.Any(city => city.Position.Equals(vertex)))
                    {
                        buildable = true;
                        break;
                    }

                    // Ou si une autre route de la civilisation touche ce vertex
                    if (civ.Roads.Any(road => RoadTouchesVertex(road, vertex)))
                    {
                        buildable = true;
                        break;
                    }
                }

                if (buildable)
                {
                    var road = new Road(edge) { CivilizationIndex = civilizationIndex };
                    // assign a distance so callers can know the build cost
                    road.DistanceToNearestCity = GetDistanceForEdge(edge, civ);
                    result.Add(road);
                }
            }

            return result;
        }

        /// <summary>
        /// Construit une route pour la civilisation si l'arête est constructible.
        /// Lance une exception si la civilisation ou l'arête n'est pas trouvée ou si l'arête n'est pas constructible.
        /// </summary>
        public Road BuildRoad(int civilizationIndex, Edge edge)
        {
            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            // Vérifier que l'arête fait partie de la carte
            var edges = new HashSet<Edge>();
            foreach (var tile in _state.Map.Tiles.Values)
            {
                foreach (var dir in HexDirectionUtils.AllHexDirections)
                {
                    edges.Add(tile.Coord.Edge(dir));
                }
            }

            if (!edges.Any(e => e.Equals(edge)))
                throw new ArgumentException("Edge not part of the map", nameof(edge));

            // Vérifier occupée
            var occupied = new HashSet<Edge>(_state.Civilizations.SelectMany(c => c.Roads).Select(r => r.Position));
            if (occupied.Any(e => e.Equals(edge)))
                throw new InvalidOperationException("Edge already occupied");

            // Vérifier constructible
            if (!IsEdgeBuildableByCivilization(edge, civ))
                throw new InvalidOperationException("Edge not buildable by this civilization");

            // Recompute distances for existing roads
            ComputeRoadDistancesForCivilization(civ);

            var distance = GetDistanceForEdge(edge, civ);
            if (distance == int.MaxValue)
                throw new InvalidOperationException("Cannot determine distance to a city for this edge");

            // cost = 2 * distance^2 for both wood and brick
            var cost = 2 * (distance * distance);

            var woodCount = civ.GetResourceQuantity(Resource.Wood);
            var brickCount = civ.GetResourceQuantity(Resource.Brick);

            if (woodCount < cost || brickCount < cost)
                throw new InvalidOperationException("Not enough resources to build the road");

            // consume resources
            civ.RemoveResource(Resource.Wood, cost);
            civ.RemoveResource(Resource.Brick, cost);

            var road = new Road(edge) { CivilizationIndex = civilizationIndex, DistanceToNearestCity = distance };
            civ.Roads.Add(road);
            return road;
        }

        private bool IsEdgeBuildableByCivilization(Edge edge, Civilization civ)
        {
            var vertices = edge.GetVertices();

            foreach (var vertex in vertices)
            {
                if (civ.Cities.Any(city => city.Position.Equals(vertex))) return true;
                if (civ.Roads.Any(road => RoadTouchesVertex(road, vertex))) return true;
            }

            return false;
        }

        private void ComputeRoadDistancesForCivilization(Civilization civ)
        {
            // initialize distances
            foreach (var r in civ.Roads)
            {
                r.DistanceToNearestCity = int.MaxValue;
            }

            var queue = new Queue<Road>();

            // roads adjacent to a city have distance 1
            foreach (var r in civ.Roads)
            {
                var verts = r.Position.GetVertices();
                if (verts.Any(v => civ.Cities.Any(c => c.Position.Equals(v))))
                {
                    r.DistanceToNearestCity = 1;
                    queue.Enqueue(r);
                }
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentVerts = current.Position.GetVertices();

                foreach (var neighbor in civ.Roads)
                {
                    if (neighbor.DistanceToNearestCity != int.MaxValue) continue; // already set
                    var neighVerts = neighbor.Position.GetVertices();
                    if (currentVerts.Any(cv => neighVerts.Any(nv => nv.Equals(cv))))
                    {
                        neighbor.DistanceToNearestCity = current.DistanceToNearestCity + 1;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        private int GetDistanceForEdge(Edge edge, Civilization civ)
        {
            var vertices = edge.GetVertices();

            int min = int.MaxValue;
            foreach (var v in vertices)
            {
                if (civ.Cities.Any(c => c.Position.Equals(v)))
                {
                    min = Math.Min(min, 1);
                }

                var touchingRoads = civ.Roads.Where(r => RoadTouchesVertex(r, v));
                foreach (var tr in touchingRoads)
                {
                    if (tr.DistanceToNearestCity != int.MaxValue)
                    {
                        min = Math.Min(min, tr.DistanceToNearestCity + 1);
                    }
                }
            }

            return min;
        }

        
        

        private static bool RoadTouchesVertex(Road road, Vertex vertex)
        {
            var verts = road.Position.GetVertices();
            return verts.Any(v => v.Equals(vertex));
        }
    }
}
