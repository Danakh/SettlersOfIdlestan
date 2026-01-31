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

        public RoadController(IslandState state)
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

                var vertices = GetEdgeVertices(edge);

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
                    result.Add(road);
                }
            }

            return result;
        }

        private static Vertex[] GetEdgeVertices(Edge edge)
        {
            var (h1, h2) = edge.GetHexes();
            var verticesH1 = new[]
            {
                h1.Vertex(SecondaryHexDirection.N),
                h1.Vertex(SecondaryHexDirection.EN),
                h1.Vertex(SecondaryHexDirection.ES),
                h1.Vertex(SecondaryHexDirection.S),
                h1.Vertex(SecondaryHexDirection.WS),
                h1.Vertex(SecondaryHexDirection.WN),
            };
            var verticesH2 = new[]
            {
                h2.Vertex(SecondaryHexDirection.N),
                h2.Vertex(SecondaryHexDirection.EN),
                h2.Vertex(SecondaryHexDirection.ES),
                h2.Vertex(SecondaryHexDirection.S),
                h2.Vertex(SecondaryHexDirection.WS),
                h2.Vertex(SecondaryHexDirection.WN),
            };

            return verticesH1.Where(v1 => verticesH2.Any(v2 => v1.Equals(v2))).Distinct().ToArray();
        }

        private static bool RoadTouchesVertex(Road road, Vertex vertex)
        {
            var verts = GetEdgeVertices(road.Position);
            return verts.Any(v => v.Equals(vertex));
        }
    }
}
