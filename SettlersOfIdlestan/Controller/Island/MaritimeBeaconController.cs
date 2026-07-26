using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Island
{
    /// <summary>
    /// Contrôle la construction des Balises Maritimes : structures posées sur un vertex entouré de
    /// 3 hexagones d'eau non profonde, débloquées par le Grand Phare niveau 2. Une fois construites,
    /// elles servent d'ancrage côtier artificiel pour RoadController (voir IsValidMaritimeEdge),
    /// permettant de prolonger les routes maritimes en pleine mer.
    /// </summary>
    public class MaritimeBeaconController
    {
        /// <summary>Distance maximale (en edges) entre un vertex constructible et le réseau routier de la civilisation.</summary>
        private const int MaxDistanceFromRoad = 1;

        private WorldState? _state;
        private readonly Dictionary<int, (int OccupiedCount, int RoadCount, int TileCount, List<Vertex> Vertices)> _buildableVerticesCache = new();

        internal MaritimeBeaconController() { }

        internal void Initialize(WorldState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _buildableVerticesCache.Clear();
        }

        public static ResourceSet GetBuildCost() => new()
        {
            { Resource.Glass, 10 },
            { Resource.Wood, 10 },
        };

        /// <summary>Débloqué par le Grand Phare niveau 2 (voir GreatLighthouseController.GetGreatLighthouseLevel).</summary>
        public bool AreMaritimeBeaconsUnlocked()
            => (_state?.Features.OfType<GreatLighthouse>().FirstOrDefault()?.Level ?? 0) >= 2;

        /// <summary>
        /// Retourne les vertex constructibles pour la civilisation : entourés de 3 hexagones d'eau
        /// non profonde (TerrainType.Water strictement — ni terre, ni eau profonde cosmétique), non
        /// déjà occupés par une ville ou une balise (de n'importe quelle civilisation), et à distance
        /// d'au plus <see cref="MaxDistanceFromRoad"/> edge(s) d'une route de la civilisation (sinon
        /// les balises pourraient être posées n'importe où en pleine mer, sans lien avec le réseau
        /// routier existant). Résultat mis en cache par civilisation ; invalidé si le nombre
        /// d'emplacements occupés, de routes de la civilisation ou de tuiles de la carte change (ces
        /// deux derniers évoluent toujours ensemble : construire une route étend la carte adjacente,
        /// voir AutoExtendController.TryExtendMapAfterRoad).
        /// </summary>
        public List<Vertex> GetBuildableVertices(int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");
            if (!AreMaritimeBeaconsUnlocked()) return new List<Vertex>();

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            int occupiedCount = _state.GetAllBuildVertices().Count();
            int roadCount = civ.Roads.Count;
            int tileCount = _state.Layers.Values.Sum(l => l.Map.Tiles.Count);

            if (_buildableVerticesCache.TryGetValue(civilizationIndex, out var cached)
                && cached.OccupiedCount == occupiedCount
                && cached.RoadCount == roadCount
                && cached.TileCount == tileCount)
                return cached.Vertices;

            var occupied = new HashSet<Vertex>(_state.GetAllBuildVertices().Select(v => v.Position));

            var result = new List<Vertex>();
            foreach (var layer in _state.Layers.Values)
            {
                var map = layer.Map;

                var roadVertices = civ.Roads
                    .Where(r => r.Position.Z == map.Z)
                    .SelectMany(r => r.Position.GetVertices())
                    .ToList();
                if (roadVertices.Count == 0) continue;

                var candidateVertices = new HashSet<Vertex>();
                foreach (var hex in map.Tiles.Keys)
                    foreach (var dir in SecondaryHexDirectionUtils.AllSecondaryDirections)
                        candidateVertices.Add(hex.Vertex(dir));

                foreach (var vertex in candidateVertices)
                {
                    if (occupied.Contains(vertex)) continue;
                    var hexes = vertex.GetHexes();
                    if (!hexes.All(h => map.Tiles.TryGetValue(h, out var tile) && tile.TerrainType == TerrainType.Water))
                        continue;
                    if (!roadVertices.Any(rv => rv.EdgeDistanceTo(vertex) <= MaxDistanceFromRoad))
                        continue;
                    result.Add(vertex);
                }
            }

            _buildableVerticesCache[civilizationIndex] = (occupiedCount, roadCount, tileCount, result);
            return result;
        }

        /// <summary>
        /// Construit une balise maritime pour la civilisation si le vertex est constructible.
        /// Retourne null si les ressources sont insuffisantes. Lance une exception si le vertex n'est
        /// pas constructible (bug appelant).
        /// </summary>
        public MaritimeBeacon? BuildMaritimeBeacon(int civilizationIndex, Vertex vertex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            if (!GetBuildableVertices(civilizationIndex).Any(v => v.Equals(vertex)))
                throw new InvalidOperationException("Vertex not buildable by this civilization");

            var cost = GetBuildCost();
            if (!civ.CanPayResourceCost(cost))
                return null;

            civ.PayResourceCost(cost);

            var beacon = new MaritimeBeacon(vertex) { CivilizationIndex = civilizationIndex };
            civ.AddMaritimeBeacon(beacon);
            return beacon;
        }
    }
}
