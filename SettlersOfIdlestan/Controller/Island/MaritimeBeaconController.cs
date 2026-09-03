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

        /// <summary>
        /// Purge le cache de vertex constructibles d'une civilisation retirée du monde — voir
        /// <see cref="WorldState.CivilizationRemoved"/>.
        /// </summary>
        internal void PurgeCivilizationCaches(int civilizationIndex)
            => _buildableVerticesCache.Remove(civilizationIndex);

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

            var civ = _state.GetCivilization(civilizationIndex)
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

            var civ = _state.GetCivilization(civilizationIndex)
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

        /// <summary>
        /// Retire une balise, que ce soit parce que le terrain sous elle ne lui laisse plus ses 3 hexs
        /// d'eau (voir <see cref="DestroyBeaconsInvalidatedByTerrain"/>) ou pour toute autre raison
        /// future. Point d'entrée unique de suppression, à l'image de
        /// <see cref="CityBuilderController.DestroyCity"/> / <see cref="WarFleetController.DestroyFleet"/>.
        /// </summary>
        public void DestroyMaritimeBeacon(MaritimeBeacon beacon)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");
            if (beacon == null) throw new ArgumentNullException(nameof(beacon));

            var civ = _state.GetCivilization(beacon.CivilizationIndex)
                      ?? throw new ArgumentException("Beacon's civilization not found", nameof(beacon));

            civ.RemoveMaritimeBeacon(beacon);
            _buildableVerticesCache.Clear();
            _state.Visibility.Recalculate();
        }

        /// <summary>
        /// Détruit toutes les Balises Maritimes dont le vertex n'est plus entouré de 3 hexs d'eau non
        /// profonde stricte — même condition que <see cref="GetBuildableVertices"/> — et retourne
        /// celles qui sont tombées. À appeler après toute transformation de terrain, aujourd'hui
        /// uniquement Marche de Dieu (AscensionController.ApplyWalkOfGod), qui peut aussi bien assécher
        /// un hex d'eau sous une balise existante (favoured terrain = Eau, Sirènes) que la laisser
        /// intacte. Une balise détruite ainsi n'est pas assez pour retirer une Flotte de Guerre posée
        /// dessus (entité indépendante) — voir <see cref="WarFleetController.DestroyFleetsInvalidatedByTerrain"/>.
        /// </summary>
        public IReadOnlyList<MaritimeBeacon> DestroyBeaconsInvalidatedByTerrain()
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            List<MaritimeBeacon>? destroyed = null;
            foreach (var beacon in _state.GetAllMaritimeBeacons().ToList())
            {
                var map = _state.GetMapFor(beacon.Position);
                if (map != null && IsFullyWater(map, beacon.Position)) continue;

                (destroyed ??= new List<MaritimeBeacon>()).Add(beacon);
                DestroyMaritimeBeacon(beacon);
            }

            return (IReadOnlyList<MaritimeBeacon>?)destroyed ?? Array.Empty<MaritimeBeacon>();
        }

        /// <summary>Vrai si les 3 hexs du vertex existent tous et sont de l'eau non profonde stricte (TerrainType.Water).</summary>
        private static bool IsFullyWater(IslandMap map, Vertex vertex)
            => vertex.GetHexes().All(h => map.Tiles.TryGetValue(h, out var tile) && tile.TerrainType == TerrainType.Water);
    }
}
