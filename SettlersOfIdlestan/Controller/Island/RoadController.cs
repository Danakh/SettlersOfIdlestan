using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.GameplayModifier;

namespace SettlersOfIdlestan.Controller.Island
{
    public class RoadAutoBuiltEventArgs : EventArgs
    {
        public int CivilizationIndex { get; }
        public Edge RoadPosition { get; }

        public RoadAutoBuiltEventArgs(int civIndex, Edge position)
        {
            CivilizationIndex = civIndex;
            RoadPosition = position;
        }
    }

    /// <summary>
    /// Contr?le la logique de construction de routes pour un IslandState.
    /// </summary>
    public class RoadController
    {
        private IslandState? _state;
        private GameClock? _clock;
        private GamePRNG _prng = new();
        private readonly Dictionary<int, (int CityCount, List<Road> Roads)> _buildableRoadsCache = new();

        // 5 s × 100 ticks/s — same cadence as automatic harvests
        public const long AutoRoadBuildCooldownTicks = 500L;

        public event EventHandler<RoadAutoBuiltEventArgs>? OnAutoRoadBuilt;
        public event EventHandler<RoadAutoBuiltEventArgs>? OnRoadBuilt;

        internal RoadController(IslandState? state = null)
        {
            _state = state;
        }

        /// <summary>
        /// Initialize or update the IslandState for this controller.
        /// </summary>
        internal void Initialize(IslandState state, GameClock? clock = null, GamePRNG? prng = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state ?? throw new ArgumentNullException(nameof(state));
            _buildableRoadsCache.Clear();

            _clock = clock;
            if (prng != null) _prng = prng;

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { PerformBuildersGuildConstruction(); }
            catch { }
        }

        private void PerformBuildersGuildConstruction()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;

            foreach (var civ in _state.Civilizations)
            {
                BuildersGuild? guild = null;
                foreach (var city in civ.Cities)
                {
                    guild = city.Buildings.OfType<BuildersGuild>().FirstOrDefault();
                    if (guild != null) break;
                }

                if (guild == null || guild.Level == 0) continue;

                // Keep timer running when disabled to avoid burst on re-enable (player only)
                bool isPlayerCiv = civ.Index == _state.PlayerCivilization.Index;
                if (isPlayerCiv && !_state.AutomationSettings.RoadAutomationEnabled)
                {
                    guild.LastRoadBuildTick = now;
                    continue;
                }

                if (guild.LastRoadBuildTick == 0)
                {
                    guild.LastRoadBuildTick = now;
                    continue;
                }

                if (now - guild.LastRoadBuildTick < AutoRoadBuildCooldownTicks) continue;

                var candidates = new List<Road>();
                for (int d = 1; d <= guild.MaxAutoRoadDistance; d++)
                    candidates.AddRange(GetBuildableRoadsAtDistance(civ.Index, d));

                guild.LastRoadBuildTick = now;

                if (candidates.Count == 0) continue;

                var chosen = candidates[_prng.Next(candidates.Count)];
                TryRemoveEnemyRoadAt(chosen.Position, civ.Index);
                var road = new Road(chosen.Position) { CivilizationIndex = civ.Index, DistanceToNearestCity = chosen.DistanceToNearestCity };
                civ.Roads.Add(road);
                ComputeRoadDistancesForCivilization(civ);
                _buildableRoadsCache.Clear();
                _state.RecalculateVisibleIslandMap(civ.Index);

                OnAutoRoadBuilt?.Invoke(this, new RoadAutoBuiltEventArgs(civ.Index, chosen.Position));
            }
        }

        /// <summary>
        /// Retourne la liste des routes constructibles pour la civilisation d'indice sp�cifi�.
        /// R�gle: une ar�te est constructible si elle n'est pas d�j� occup�e par une route,
        /// et si un de ses deux vertex contient une ville de la civilisation, ou si une route
        /// existante de la civilisation touche ce vertex.
        /// </summary>
        public List<Road> GetBuildableRoads(int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                          ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            if (_buildableRoadsCache.TryGetValue(civilizationIndex, out var cached) && cached.CityCount == civ.Cities.Count)
                return cached.Roads;

            // Seules les routes de NOTRE civilisation bloquent la construction.
            // Les routes ennemies sont conquérables (elles seront détruites à la construction).
            var ownOccupied = new HashSet<Edge>(civ.Roads.Select(r => r.Position));

            // Collecte les ar�tes candidates depuis les vertices des villes
            // et les ar�tes voisines des routes existantes
            var candidates = new HashSet<Edge>();
            foreach (var city in civ.Cities)
            {
                foreach (var edge in GetEdgesAtVertex(city.Position))
                    candidates.Add(edge);
            }
            foreach (var road in civ.Roads)
            {
                foreach (var edge in road.Position.GetNeighboringEdges())
                    candidates.Add(edge);
            }

            var result = new List<Road>();
            foreach (var edge in candidates)
            {
                if (ownOccupied.Any(e => e.Equals(edge))) continue;
                if (!IsEdgeOnLand(edge))
                {
                    if (!civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_MARITIME_ROUTES)
                        || !IsValidMaritimeEdge(edge))
                        continue;
                }

                var road = new Road(edge) { CivilizationIndex = civilizationIndex };
                // assign a distance so callers can know the build cost
                road.DistanceToNearestCity = GetDistanceForEdge(edge, civ);
                result.Add(road);
            }

            _buildableRoadsCache[civilizationIndex] = (civ.Cities.Count, result);
            return result;
        }

        /// <summary>
        /// Retourne les routes constructibles pour la civilisation d'indice sp�cifi? dont la distance
        /// ? la ville la plus proche est ?gale ? la valeur fournie (ex: 2).
        /// </summary>
        public List<Road> GetBuildableRoadsAtDistance(int civilizationIndex, int distance)
        {
            if (distance <= 0) throw new ArgumentException("Distance must be >= 1", nameof(distance));

            // R?utilise la logique existante puis filtre par distance
            var all = GetBuildableRoads(civilizationIndex);
            return all.Where(r => r.DistanceToNearestCity == distance).ToList();
        }

        /// <summary>
        /// Construit une route pour la civilisation si l'ar�te est constructible.
        /// Retourne null si la civilisation n'a pas les ressources suffisantes.
        /// Lance une exception si l'ar�te n'est pas constructible (bug appelant).
        /// </summary>
        public Road? BuildRoad(int civilizationIndex, Edge edge)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            // V�rifier que l'ar�te fait partie de la carte
            var map = _state.GetMapFor(edge);
            var mapTiles = map.Tiles;
            if (!mapTiles.ContainsKey(edge.Hex1) || !mapTiles.ContainsKey(edge.Hex2))
                throw new ArgumentException("Edge not part of the map", nameof(edge));

            // V�rifier que l'ar�te n'est pas entre deux hexagones de type eau (sauf routes maritimes débloquées)
            bool isMaritimePath = mapTiles[edge.Hex1].TerrainType == TerrainType.Water
                && mapTiles[edge.Hex2].TerrainType == TerrainType.Water;
            if (isMaritimePath)
            {
                if (!civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_MARITIME_ROUTES))
                    throw new InvalidOperationException("Cannot build a road on an edge between two water hexes");
                if (!IsValidMaritimeEdge(edge))
                    throw new InvalidOperationException("Maritime route must connect two coastal vertices");
            }

            // Seule notre propre civilisation peut bloquer la construction
            if (civ.Roads.Any(r => r.Position.Equals(edge)))
                throw new InvalidOperationException("Edge already occupied");

            // V�rifier constructible
            if (!IsEdgeBuildableByCivilization(edge, civ))
                throw new InvalidOperationException("Edge not buildable by this civilization");

            // Recompute distances for existing roads
            ComputeRoadDistancesForCivilization(civ);

            var distance = GetDistanceForEdge(edge, civ);
            if (distance == int.MaxValue)
                throw new InvalidOperationException("Cannot determine distance to a city for this edge");

            var cost = isMaritimePath ? GetMaritimeRoadCost() : GetRoadCost(distance, civ);

            if (!civ.CanPayResourceCost(cost))
                return null;

            // Détruire la route ennemie éventuelle sur cette arête
            TryRemoveEnemyRoadAt(edge, civilizationIndex);

            // consume resources
            civ.PayResourceCost(cost);

            var road = new Road(edge) { CivilizationIndex = civilizationIndex, DistanceToNearestCity = distance };
            civ.Roads.Add(road);

            ComputeRoadDistancesForCivilization(civ);
            _buildableRoadsCache.Clear();
            _state.RecalculateVisibleIslandMap(civilizationIndex);

            OnRoadBuilt?.Invoke(this, new RoadAutoBuiltEventArgs(civilizationIndex, edge));
            return road;
        }

        private void TryRemoveEnemyRoadAt(Edge edge, int buildingCivIndex)
        {
            if (_state == null) return;
            foreach (var otherCiv in _state.Civilizations.Where(c => c.Index != buildingCivIndex))
            {
                var enemyRoad = otherCiv.Roads.FirstOrDefault(r => r.Position.Equals(edge));
                if (enemyRoad != null)
                {
                    otherCiv.Roads.Remove(enemyRoad);
                    ComputeRoadDistancesForCivilization(otherCiv);
                    return;
                }
            }
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

        private bool IsValidMaritimeEdge(Edge edge)
        {
            if (_state == null) return false;
            var mapTiles = _state.GetMapFor(edge).Tiles;
            foreach (var v in edge.GetVertices())
            {
                bool touchesLand = v.GetHexes().Any(h =>
                    mapTiles.TryGetValue(h, out var tile) && tile.TerrainType != TerrainType.Water);
                if (!touchesLand) return false;
            }
            return true;
        }

        private bool IsEdgeOnLand(Edge edge)
        {
            if (_state == null) throw new InvalidOperationException("IslandState has not been initialized.");

            var mapTiles = _state.GetMapFor(edge).Tiles;
            bool hex1IsWaterOrAbsent = !mapTiles.TryGetValue(edge.Hex1, out var tile1) || tile1.TerrainType == TerrainType.Water;
            bool hex2IsWaterOrAbsent = !mapTiles.TryGetValue(edge.Hex2, out var tile2) || tile2.TerrainType == TerrainType.Water;
            return !(hex1IsWaterOrAbsent && hex2IsWaterOrAbsent);
        }

        private static Edge[] GetEdgesAtVertex(Vertex vertex)
        {
            var hexes = vertex.GetHexes();
            return new[]
            {
                Edge.Create(hexes[0], hexes[1]),
                Edge.Create(hexes[0], hexes[2]),
                Edge.Create(hexes[1], hexes[2])
            };
        }

        private static int GetGuildRoadCostReduction(Civilization civ)
        {
            foreach (var city in civ.Cities)
            {
                var guild = city.Buildings.OfType<BuildersGuild>().FirstOrDefault();
                if (guild != null && guild.Level > 0)
                    return guild.RoadCostReduction;
            }
            return 0;
        }

        public static ResourceSet GetMaritimeRoadCost() => new ResourceSet
        {
            { Resource.Wood, 10 },
            { Resource.Brick, 10 },
            { Resource.Gold, 5 },
        };

        public ResourceSet GetRoadCost(int distance, Civilization? civ = null)
        {
            if (distance <= 0) throw new ArgumentException("Distance must be >= 1", nameof(distance));
            var cost = 1 + (distance * distance);
            if (civ != null)
                cost = Math.Max(0, cost - GetGuildRoadCostReduction(civ));
            return new ResourceSet
            {
                { Resource.Wood, cost },
                { Resource.Brick, cost }
            };
        }

        public ResourceSet GetPlayerRoadCost(Edge edge)
        {
            if (!IsEdgeOnLand(edge))
                return GetMaritimeRoadCost();
            var civ = _state!.PlayerCivilization;
            var distance = GetDistanceForEdge(edge, civ);
            return GetRoadCost(distance, civ);
        }
    }
}
