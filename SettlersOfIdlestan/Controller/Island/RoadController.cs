using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.Prestige;

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
    /// Contr?le la logique de construction de routes pour un WorldState.
    /// </summary>
    public class RoadController
    {
        private WorldState? _state;
        private GameClock? _clock;
        private GamePRNG? _prng;
        private PrestigeState? _prestigeState;
        private readonly Dictionary<int, (int CityCount, int BeaconCount, List<Road> Roads)> _buildableRoadsCache = new();

        // 5 s × 100 ticks/s — same cadence as automatic harvests
        public const long AutoRoadBuildCooldownTicks = 500L;

        public event EventHandler<RoadAutoBuiltEventArgs>? OnAutoRoadBuilt;
        public event EventHandler<RoadAutoBuiltEventArgs>? OnRoadBuilt;

        internal RoadController(WorldState? state = null)
        {
            _state = state;
        }

        /// <summary>
        /// Initialize or update the WorldState for this controller.
        /// </summary>
        internal void Initialize(WorldState state, GameClock? clock = null, GamePRNG? prng = null, PrestigeState? prestigeState = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state ?? throw new ArgumentNullException(nameof(state));
            _buildableRoadsCache.Clear();

            _clock = clock;
            if (prng != null) _prng = prng;
            _prestigeState = prestigeState;

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { PerformBuildersGuildConstruction(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[RoadController] {nameof(PerformBuildersGuildConstruction)}: {ex}"); }
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
                bool underworldUnlocked = civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_BUILDERS_GUILD_UNDERWORLD);
                bool surfaceEnabled = !isPlayerCiv || _state.AutomationSettings.RoadAutomationEnabled;
                bool underworldEnabled = underworldUnlocked && (!isPlayerCiv || _state.AutomationSettings.RoadAutomationEnabledUnderworld);
                if (!surfaceEnabled && !underworldEnabled)
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
                {
                    var atDistance = GetBuildableRoadsAtDistance(civ.Index, d);
                    if (surfaceEnabled) candidates.AddRange(atDistance.Where(r => r.Position.Z == IslandMap.SurfaceLayer));
                    if (underworldEnabled) candidates.AddRange(atDistance.Where(r => r.Position.Z == LayerState.UnderworldZ));
                }

                guild.LastRoadBuildTick = now;

                if (candidates.Count == 0) continue;

                var chosen = candidates[_prng!.Next(candidates.Count)];
                TryRemoveEnemyRoadAt(chosen.Position, civ.Index);
                var road = new Road(chosen.Position) { CivilizationIndex = civ.Index, DistanceToNearestCity = chosen.DistanceToNearestCity };
                civ.AddRoad(road);
                ComputeRoadDistancesForCivilization(civ);
                _buildableRoadsCache.Clear();
                _state.Visibility.RecalculateFor(civ.Index);

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
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                          ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            if (_buildableRoadsCache.TryGetValue(civilizationIndex, out var cached)
                && cached.CityCount == civ.Cities.Count
                && cached.BeaconCount == civ.MaritimeBeacons.Count)
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
                foreach (var vertex in road.Position.GetVertices())
                {
                    if (HasEnemyCityAt(vertex, civ)) continue;
                    var thirdHex = vertex.GetHexes().First(h => !h.Equals(road.Position.Hex1) && !h.Equals(road.Position.Hex2));
                    candidates.Add(Edge.Create(road.Position.Hex1, thirdHex));
                    candidates.Add(Edge.Create(road.Position.Hex2, thirdHex));
                }
            }

            var enemyProtectedEdges = new HashSet<Edge>(
                _state.Civilizations
                    .Where(c => c.Index != civilizationIndex)
                    .SelectMany(c => c.Roads)
                    .Where(r => r.DistanceToNearestCity <= 2)
                    .Select(r => r.Position));

            var result = new List<Road>();
            foreach (var edge in candidates)
            {
                if (ownOccupied.Any(e => e.Equals(edge))) continue;
                if (enemyProtectedEdges.Contains(edge)) continue;
                if (IsEdgeBetweenVoidHexes(edge))
                {
                    if (!civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_VOID_ROUTES))
                        continue;
                }
                else if (!IsEdgeOnLand(edge))
                {
                    if (EdgeTouchesDeepWater(edge))
                        continue;
                    if (!civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_MARITIME_ROUTES)
                        || !IsValidMaritimeEdge(edge, civ))
                        continue;
                }

                var road = new Road(edge) { CivilizationIndex = civilizationIndex };
                // assign a distance so callers can know the build cost
                road.DistanceToNearestCity = GetDistanceForEdge(edge, civ);
                result.Add(road);
            }

            _buildableRoadsCache[civilizationIndex] = (civ.Cities.Count, civ.MaritimeBeacons.Count, result);
            return result;
        }

        /// <summary>
        /// Retourne les arêtes adjacentes au réseau de la civilisation qui sont bloquées par une route
        /// ennemie à distance ≤ 2 de sa ville (zone d'influence protégée).
        /// </summary>
        public List<Edge> GetEnemyProtectedRoadEdges(int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                          ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            var ownOccupied = new HashSet<Edge>(civ.Roads.Select(r => r.Position));

            var candidates = new HashSet<Edge>();
            foreach (var city in civ.Cities)
            {
                foreach (var edge in GetEdgesAtVertex(city.Position))
                    candidates.Add(edge);
            }
            foreach (var road in civ.Roads)
            {
                foreach (var vertex in road.Position.GetVertices())
                {
                    if (HasEnemyCityAt(vertex, civ)) continue;
                    var thirdHex = vertex.GetHexes().First(h => !h.Equals(road.Position.Hex1) && !h.Equals(road.Position.Hex2));
                    candidates.Add(Edge.Create(road.Position.Hex1, thirdHex));
                    candidates.Add(Edge.Create(road.Position.Hex2, thirdHex));
                }
            }

            var enemyProtectedEdges = new HashSet<Edge>(
                _state.Civilizations
                    .Where(c => c.Index != civilizationIndex)
                    .SelectMany(c => c.Roads)
                    .Where(r => r.DistanceToNearestCity <= 2)
                    .Select(r => r.Position));

            return candidates
                .Where(e => !ownOccupied.Contains(e) && enemyProtectedEdges.Contains(e))
                .ToList();
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
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            // Vérifier que l'arête fait partie de la carte
            var map = _state.GetMapFor(edge);
            if (map == null) throw new ArgumentException("Edge belongs to an unknown layer.", nameof(edge));
            var mapTiles = map.Tiles;
            if (!mapTiles.ContainsKey(edge.Hex1) || !mapTiles.ContainsKey(edge.Hex2))
                throw new ArgumentException("Edge not part of the map", nameof(edge));

            // V�rifier que l'ar�te n'est pas entre deux hexagones de type eau ou de Vide
            // (sauf routes maritimes/du Vide débloquées)
            bool isVoidPath = IsEdgeBetweenVoidHexes(edge);
            bool isMaritimePath = !isVoidPath && !IsEdgeOnLand(edge);
            if (isVoidPath)
            {
                if (!civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_VOID_ROUTES))
                    throw new InvalidOperationException("Cannot build a road on an edge between two void hexes");
            }
            else if (isMaritimePath)
            {
                if (EdgeTouchesDeepWater(edge))
                    throw new InvalidOperationException("Cannot build a road through deep water");
                if (!civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_MARITIME_ROUTES))
                    throw new InvalidOperationException("Cannot build a road on an edge between two water hexes");
                if (!IsValidMaritimeEdge(edge, civ))
                    throw new InvalidOperationException("Maritime route must connect two coastal vertices or maritime beacons");
            }

            // Seule notre propre civilisation peut bloquer la construction
            if (civ.Roads.Any(r => r.Position.Equals(edge)))
                throw new InvalidOperationException("Edge already occupied");

            // Les routes ennemies proches de leur ville ne sont pas conquérables
            bool isEnemyProtected = _state.Civilizations
                .Where(c => c.Index != civilizationIndex)
                .SelectMany(c => c.Roads)
                .Any(r => r.Position.Equals(edge) && r.DistanceToNearestCity <= 2);
            if (isEnemyProtected)
                throw new InvalidOperationException("Edge is protected by an enemy road");

            // V�rifier constructible
            if (!IsEdgeBuildableByCivilization(edge, civ))
                throw new InvalidOperationException("Edge not buildable by this civilization");

            // Recompute distances for existing roads
            ComputeRoadDistancesForCivilization(civ);

            var distance = GetDistanceForEdge(edge, civ);
            if (distance == int.MaxValue)
                return null; // road must no longer be linked to a city

            var cost = (isVoidPath || isMaritimePath) ? GetMaritimeRoadCost() : GetRoadCost(distance, civ);

            long voidResearchCost = 0;
            if (isVoidPath)
            {
                voidResearchCost = GetVoidRouteResearchCostFor(civ);
                if ((_prestigeState?.TechnologyTree.ResearchPoints ?? 0) < voidResearchCost)
                    return null;
            }

            if (!civ.CanPayResourceCost(cost))
                return null;

            // Détruire la route ennemie éventuelle sur cette arête
            TryRemoveEnemyRoadAt(edge, civilizationIndex);

            // consume resources
            civ.PayResourceCost(cost);
            if (isVoidPath)
                _prestigeState!.TechnologyTree.ResearchPoints -= voidResearchCost;

            var road = new Road(edge) { CivilizationIndex = civilizationIndex, DistanceToNearestCity = distance };
            civ.AddRoad(road);

            ComputeRoadDistancesForCivilization(civ);
            _buildableRoadsCache.Clear();
            _state.Visibility.RecalculateFor(civilizationIndex);

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
                    otherCiv.RemoveRoad(enemyRoad);
                    ComputeRoadDistancesForCivilization(otherCiv);
                    RemoveDisconnectedRoads(otherCiv);
                    return;
                }
            }
        }

        /// <summary>
        /// Supprime les routes à distance ≤ 2 de la ville détruite, puis toutes les routes
        /// désormais déconnectées de toute ville. Doit être appelé après avoir retiré la ville de civ.Cities.
        /// </summary>
        public void OnCityDestroyed(Civilization civ, Vertex cityVertex)
        {
            var toRemove = GetRoadsWithinDistanceOfVertex(civ.Roads, cityVertex, 2);
            foreach (var road in toRemove)
                civ.RemoveRoad(road);

            ComputeRoadDistancesForCivilization(civ);
            RemoveDisconnectedRoads(civ);

            _buildableRoadsCache.Clear();
            _state?.Visibility.RecalculateFor(civ.Index);
        }

        private static List<Road> GetRoadsWithinDistanceOfVertex(IReadOnlyList<Road> roads, Vertex vertex, int maxDistance)
        {
            var result = new List<Road>();
            var visited = new HashSet<Edge>();
            var frontier = new List<Road>();

            var vertexIndex = BuildVertexIndex(roads);

            if (vertexIndex.TryGetValue(vertex, out var seed))
            {
                foreach (var road in seed)
                {
                    if (visited.Add(road.Position))
                    {
                        result.Add(road);
                        frontier.Add(road);
                    }
                }
            }

            for (int dist = 1; dist < maxDistance; dist++)
            {
                var next = new List<Road>();
                foreach (var current in frontier)
                {
                    foreach (var v in current.Position.GetVertices())
                    {
                        if (!vertexIndex.TryGetValue(v, out var neighbors)) continue;
                        foreach (var neighbor in neighbors)
                        {
                            if (visited.Contains(neighbor.Position)) continue;
                            visited.Add(neighbor.Position);
                            result.Add(neighbor);
                            next.Add(neighbor);
                        }
                    }
                }
                frontier = next;
            }

            return result;
        }

        private static void RemoveDisconnectedRoads(Civilization civ)
        {
            civ.RemoveAllRoads(r => r.DistanceToNearestCity == int.MaxValue);
        }

        private bool IsEdgeBuildableByCivilization(Edge edge, Civilization civ)
        {
            var vertices = edge.GetVertices();

            foreach (var vertex in vertices)
            {
                if (civ.Cities.Any(city => city.Position.Equals(vertex))) return true;
                if (!HasEnemyCityAt(vertex, civ) && civ.Roads.Any(road => RoadTouchesVertex(road, vertex))) return true;
            }

            return false;
        }

        private bool HasEnemyCityAt(Vertex vertex, Civilization civ)
        {
            if (_state == null) return false;
            return _state.Civilizations.Any(c => c.Index != civ.Index && c.Cities.Any(city => city.Position.Equals(vertex)));
        }

        private void ComputeRoadDistancesForCivilization(Civilization civ)
        {
            foreach (var r in civ.Roads)
                r.DistanceToNearestCity = int.MaxValue;

            var vertexToRoads = BuildVertexIndex(civ.Roads);
            var cityVertices = new HashSet<Vertex>(civ.Cities.Select(c => c.Position));
            var queue = new Queue<Road>();

            foreach (var r in civ.Roads)
            {
                var verts = r.Position.GetVertices();
                if (verts.Any(v => cityVertices.Contains(v)))
                {
                    r.DistanceToNearestCity = 1;
                    queue.Enqueue(r);
                }
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var nextDist = current.DistanceToNearestCity + 1;
                foreach (var v in current.Position.GetVertices())
                {
                    if (!vertexToRoads.TryGetValue(v, out var neighbors)) continue;
                    foreach (var neighbor in neighbors)
                    {
                        if (neighbor.DistanceToNearestCity != int.MaxValue) continue;
                        neighbor.DistanceToNearestCity = nextDist;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        private static Dictionary<Vertex, List<Road>> BuildVertexIndex(IEnumerable<Road> roads)
        {
            var index = new Dictionary<Vertex, List<Road>>();
            foreach (var r in roads)
            {
                foreach (var v in r.Position.GetVertices())
                {
                    if (!index.TryGetValue(v, out var list))
                        index[v] = list = new List<Road>();
                    list.Add(r);
                }
            }
            return index;
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

        /// <summary>
        /// Une arête maritime est constructible si chacun de ses deux vertex touche la terre ferme
        /// (hex ni Water ni DeepWater), ou porte une Balise Maritime (<see cref="MaritimeBeacon"/>) de la
        /// civilisation qui construit — ce qui permet de prolonger les routes maritimes en pleine mer
        /// de balise en balise, ou de la côte à une balise.
        /// </summary>
        private bool IsValidMaritimeEdge(Edge edge, Civilization civ)
        {
            if (_state == null) return false;
            var mapTiles = _state.GetMapFor(edge)?.Tiles;
            if (mapTiles == null) return false;
            foreach (var v in edge.GetVertices())
            {
                bool touchesLand = v.GetHexes().Any(h =>
                    mapTiles.TryGetValue(h, out var tile) && !tile.TerrainType.IsWater());
                bool hasOwnBeacon = civ.MaritimeBeacons.Any(b => b.Position.Equals(v));
                if (!touchesLand && !hasOwnBeacon) return false;
            }
            return true;
        }

        private bool IsEdgeOnLand(Edge edge)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var mapTiles = _state.GetMapFor(edge)?.Tiles;
            if (mapTiles == null) return false;
            bool hex1IsWaterOrAbsent = !mapTiles.TryGetValue(edge.Hex1, out var tile1) || tile1.TerrainType.IsWater();
            bool hex2IsWaterOrAbsent = !mapTiles.TryGetValue(edge.Hex2, out var tile2) || tile2.TerrainType.IsWater();
            return !(hex1IsWaterOrAbsent && hex2IsWaterOrAbsent);
        }

        /// <summary>
        /// Vrai si les deux hexagones de l'arête sont du Vide — arête normalement infranchissable,
        /// rendue constructible (comme une route maritime) par <see cref="Modifier.ECategory.UNLOCK_VOID_ROUTES"/>.
        /// </summary>
        private bool IsEdgeBetweenVoidHexes(Edge edge)
        {
            if (_state == null) return false;
            var mapTiles = _state.GetMapFor(edge)?.Tiles;
            if (mapTiles == null) return false;
            bool hex1IsVoid = mapTiles.TryGetValue(edge.Hex1, out var tile1) && tile1.TerrainType == TerrainType.Void;
            bool hex2IsVoid = mapTiles.TryGetValue(edge.Hex2, out var tile2) && tile2.TerrainType == TerrainType.Void;
            return hex1IsVoid && hex2IsVoid;
        }

        /// <summary>
        /// Coût en points de recherche d'une route du Vide supplémentaire : 1 000 000 × 4^n,
        /// n étant le nombre de routes du Vide déjà construites par la civilisation.
        /// </summary>
        public static long GetVoidRouteResearchCost(int alreadyBuilt)
        {
            long cost = 1_000_000L;
            for (int i = 0; i < alreadyBuilt; i++)
                cost *= 4;
            return cost;
        }

        /// <summary>
        /// Vrai si l'un des deux hexagones de l'arête est de l'eau profonde (bordure cosmétique,
        /// jamais traversable ni constructible — voir <see cref="TerrainTypeExtensions.IsWater"/>).
        /// </summary>
        private bool EdgeTouchesDeepWater(Edge edge)
        {
            if (_state == null) return false;
            var mapTiles = _state.GetMapFor(edge)?.Tiles;
            if (mapTiles == null) return false;
            bool hex1IsDeepWater = mapTiles.TryGetValue(edge.Hex1, out var tile1) && tile1.TerrainType == TerrainType.DeepWater;
            bool hex2IsDeepWater = mapTiles.TryGetValue(edge.Hex2, out var tile2) && tile2.TerrainType == TerrainType.DeepWater;
            return hex1IsDeepWater || hex2IsDeepWater;
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

        /// <summary>Coût en points de recherche à afficher pour une route du Vide sur cette arête (null si l'arête n'en est pas une).</summary>
        public long? GetPlayerVoidRoadResearchCost(Edge edge)
        {
            if (!IsEdgeBetweenVoidHexes(edge)) return null;
            return GetVoidRouteResearchCostFor(_state!.PlayerCivilization);
        }

        /// <summary>
        /// Coût de la prochaine route du Vide pour cette civilisation. Avec Cartographie du Vide
        /// (VOID_ROUTE_COST_REDUCTION), les routes déjà bâties ne comptent que pour moitié
        /// (arrondi en faveur du joueur) dans l'exposant de <see cref="GetVoidRouteResearchCost"/>.
        /// </summary>
        private long GetVoidRouteResearchCostFor(Civilization civ)
        {
            int alreadyBuilt = civ.Roads.Count(r => IsEdgeBetweenVoidHexes(r.Position));
            if (civ.ModifierAggregator.HasModifier(Modifier.ECategory.VOID_ROUTE_COST_REDUCTION))
                alreadyBuilt /= 2;
            return GetVoidRouteResearchCost(alreadyBuilt);
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
            if (IsEdgeBetweenVoidHexes(edge) || !IsEdgeOnLand(edge))
                return GetMaritimeRoadCost();
            var civ = _state!.PlayerCivilization;
            var distance = GetDistanceForEdge(edge, civ);
            var cost = GetRoadCost(distance, civ);
            if (edge.Z == LayerState.UnderworldZ)
            {
                int reduction = civ.ModifierAggregator.ApplyModifiers(Modifier.ECategory.UNDERWORLD_ROAD_BASE_REDUCTION, "", 0);
                int baseOre   = Math.Max(0, 5  - reduction / 2);
                int baseStone = Math.Max(0, 10 - reduction);
                cost[Resource.Ore]   = cost[Resource.Ore]   + baseOre;
                cost[Resource.Stone] = cost[Resource.Stone] + baseStone;
            }
            foreach (var k in cost.Keys)
            {
                double arrivalDist = Math.Round(Math.Pow(GetDistanceFromArrivalVertex(edge, civ), 1.5));
                cost[k] = cost[k] * (int)arrivalDist;
            }

            return cost;
        }

        private int GetDistanceFromArrivalVertex(Edge edge, Civilization civ)
        {
            if (_state == null) return 1;
            if (!_state.Layers.TryGetValue(LayerState.UnderworldZ, out var underworldLayer)) return 1;
            var arrival = underworldLayer.ArrivalVertex;
            if (arrival == null) return 1;

            var underworldRoads = civ.Roads.Where(r => r.Position.Z == LayerState.UnderworldZ).ToList();
            var vertexIndex = BuildVertexIndex(underworldRoads);

            var dist = new Dictionary<Vertex, int> { [arrival] = 0 };
            var queue = new Queue<Vertex>();
            queue.Enqueue(arrival);

            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                if (!vertexIndex.TryGetValue(v, out var neighbors)) continue;
                foreach (var road in neighbors)
                {
                    foreach (var nv in road.Position.GetVertices())
                    {
                        if (dist.ContainsKey(nv)) continue;
                        dist[nv] = dist[v] + 1;
                        queue.Enqueue(nv);
                    }
                }
            }

            int minVertexDist = int.MaxValue;
            foreach (var v in edge.GetVertices())
            {
                if (dist.TryGetValue(v, out var d))
                    minVertexDist = Math.Min(minVertexDist, d);
            }

            return minVertexDist == int.MaxValue ? 1 : minVertexDist + 1;
        }
    }
}
