using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandFeatures;
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
        // Clé (CivilizationIndex, Layer) — un layer (surface/inframonde/abysse) forme un graphe de
        // vertex/edge totalement indépendant des autres (voir Vertex.Z / Edge.Z). Construire une route
        // dans un layer ne peut donc jamais changer les routes constructibles d'un autre layer : mettre
        // en cache par layer évite de tout recalculer pour toute la civilisation (potentiellement des
        // milliers de routes cumulées sur plusieurs layers) à chaque route posée dans un seul layer.
        private readonly Dictionary<(int CivilizationIndex, int Layer), (int CityCount, int BeaconCount, List<Road> Roads)> _buildableRoadsCache = new();

        /// <summary>
        /// Invalide le cache de routes constructibles de TOUTES les civilisations pour un layer donné.
        /// Nécessaire car le calcul d'une civilisation dépend aussi des routes/villes des AUTRES
        /// civilisations (enemyProtectedEdges, HasEnemyCityAt) : un changement de routes/ville chez une
        /// seule civilisation peut donc rendre le cache d'une autre civilisation obsolète sur ce layer.
        /// </summary>
        internal void InvalidateBuildableRoadsCacheForLayer(int layer)
        {
            foreach (var key in _buildableRoadsCache.Keys.Where(k => k.Layer == layer).ToList())
                _buildableRoadsCache.Remove(key);
        }

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

            // Boucle indexée (et non foreach) car OnAutoRoadBuilt ci-dessous peut, via
            // AutoExtendController.TryExtendMapAfterRoad → SpawnAggressiveCivilization, ajouter
            // une civilisation PNJ à _state.Civilizations pendant cette même itération : un foreach
            // lèverait "Collection was modified" dès l'appel MoveNext suivant. Recalculer Count à
            // chaque tour tolère l'ajout (la nouvelle civ est simplement traitée ce tick, sans effet
            // puisqu'elle n'a pas encore de BuildersGuild).
            for (int i = 0; i < _state.Civilizations.Count; i++)
            {
                var civ = _state.Civilizations[i];
                if (civ.GetUniqueBuilding(BuildingType.BuildersGuild) is not BuildersGuild guild || guild.Level == 0) continue;

                // Keep timer running when disabled to avoid burst on re-enable (player only)
                bool isPlayerCiv = civ.Index == _state.PlayerCivilization.Index;
                bool underworldUnlocked = civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_BUILDERS_GUILD_UNDERWORLD);
                bool surfaceEnabled = !isPlayerCiv || _state.AutomationSettings.IsRoadAutomationActive;
                bool underworldEnabled = underworldUnlocked && (!isPlayerCiv || _state.AutomationSettings.IsRoadAutomationActiveUnderworld);
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

                // Même accélération par ville que l'automatisation des bâtiments
                // (voir BuildingController.TickGuildAutomation) : sans elle, la cadence de pose des
                // routes restait fixe alors que le réseau à couvrir grandit avec la civilisation.
                double guildSpeedBonus = civ.ModifierAggregator.ApplyModifiers(Modifier.ECategory.GUILD_AUTOMATION_SPEED_PER_CITY, "", 0.0) * civ.Cities.Count;
                long effectiveCooldown = guildSpeedBonus > 0
                    ? Math.Max(1L, (long)(AutoRoadBuildCooldownTicks / (1.0 + guildSpeedBonus)))
                    : AutoRoadBuildCooldownTicks;
                if (now - guild.LastRoadBuildTick < effectiveCooldown) continue;

                var candidates = new List<Road>();
                if (surfaceEnabled)
                    for (int d = 1; d <= guild.MaxAutoRoadDistance; d++)
                        candidates.AddRange(GetBuildableRoadsAtDistance(civ.Index, d).Where(r => r.Position.Z == IslandMap.SurfaceLayer));

                // La guilde priorise la surface : l'Inframonde n'est considéré que si aucune route
                // de surface n'est disponible ce tick.
                if (candidates.Count == 0 && underworldEnabled)
                    for (int d = 1; d <= guild.MaxAutoRoadDistance; d++)
                        candidates.AddRange(GetBuildableRoadsAtDistance(civ.Index, d).Where(r => r.Position.Z == LayerState.UnderworldZ));

                guild.LastRoadBuildTick = now;

                if (candidates.Count == 0) continue;

                var chosen = candidates[_prng!.Next(candidates.Count)];
                TryRemoveEnemyRoadAt(chosen.Position, civ.Index);
                var road = new Road(chosen.Position) { CivilizationIndex = civ.Index, DistanceToNearestCity = chosen.DistanceToNearestCity };
                civ.AddRoad(road);
                ComputeRoadDistancesForCivilization(civ, chosen.Position.Z);
                InvalidateBuildableRoadsCacheForLayer(chosen.Position.Z);
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

            var civ = _state.GetCivilization(civilizationIndex)
                          ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            var layers = new HashSet<int>();
            foreach (var city in civ.Cities) layers.Add(city.Position.Z);
            foreach (var road in civ.Roads) layers.Add(road.Position.Z);

            if (layers.Count == 0) return new List<Road>();
            if (layers.Count == 1) return GetBuildableRoadsForLayer(civ, layers.First());

            var result = new List<Road>();
            foreach (var layer in layers)
                result.AddRange(GetBuildableRoadsForLayer(civ, layer));
            return result;
        }

        /// <summary>
        /// Calcule (ou renvoie depuis le cache) les routes constructibles pour un seul layer de la
        /// civilisation. Un layer (surface/inframonde/abysse) est un graphe de vertex/edge totalement
        /// indépendant des autres, donc ce calcul n'a besoin de considérer que les villes/routes/balises
        /// de ce layer.
        /// </summary>
        private List<Road> GetBuildableRoadsForLayer(Civilization civ, int layer)
        {
            int civilizationIndex = civ.Index;
            int cityCount = civ.Cities.Count(c => c.Position.Z == layer);
            int beaconCount = civ.MaritimeBeacons.Count(b => b.Position.Z == layer);
            var cacheKey = (civilizationIndex, layer);

            if (_buildableRoadsCache.TryGetValue(cacheKey, out var cached)
                && cached.CityCount == cityCount
                && cached.BeaconCount == beaconCount)
                return cached.Roads;

            var citiesInLayer = civ.Cities.Where(c => c.Position.Z == layer).ToList();
            var roadsInLayer = civ.Roads.Where(r => r.Position.Z == layer).ToList();
            var mapTiles = _state!.GetMapForZ(layer)?.Tiles;

            // Seules les routes de NOTRE civilisation bloquent la construction.
            // Les routes ennemies sont conquérables (elles seront détruites à la construction).
            var ownOccupied = new HashSet<Edge>(roadsInLayer.Select(r => r.Position));

            // Collecte les arêtes candidates depuis les vertices des villes
            // et les arêtes voisines des routes existantes
            var candidates = new HashSet<Edge>();
            foreach (var city in citiesInLayer)
            {
                foreach (var edge in GetEdgesAtVertex(city.Position))
                    candidates.Add(edge);
            }
            foreach (var road in roadsInLayer)
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
                _state!.Civilizations
                    .Where(c => c.Index != civilizationIndex)
                    .SelectMany(c => c.Roads)
                    .Where(r => r.Position.Z == layer && r.DistanceToNearestCity <= 2)
                    .Select(r => r.Position));

            var result = new List<Road>();
            foreach (var edge in candidates)
            {
                // Un candidat dont l'un des deux hex n'existe pas encore sur la carte (au-delà de
                // l'anneau d'eau profonde en surface, ou pas encore révélé sur une couche AutoExtend)
                // ne peut jamais être construit — BuildRoad rejette systématiquement une telle arête
                // (voir sa vérification "Edge not part of the map"). IsEdgeOnLand ci-dessous traite un
                // hex absent comme de l'eau pour décider si l'arête est "sur terre", ce qui la fait à
                // tort passer pour une route terrestre normale dès que l'autre hex est un vrai hex de
                // terre — reproduit en jeu par un PNJ dont une route côtière touche l'anneau d'eau
                // profonde : le "troisième hex" du vertex suivant est alors totalement absent de la
                // carte, jamais re-révélé (l'anneau n'est ajouté qu'une fois, autour des hex d'Eau
                // d'origine, pas autour de lui-même). Sur une couche AutoExtend, ce filtre ne retire
                // jamais de candidat légitime : TryExtendMapAfterRoad révèle systématiquement les deux
                // vertex complets de toute arête construite, donc tout hex à un pas d'une ville ou
                // d'une route existante est déjà garanti présent ici.
                if (mapTiles == null || !mapTiles.ContainsKey(edge.Hex1) || !mapTiles.ContainsKey(edge.Hex2))
                    continue;
                if (ownOccupied.Contains(edge)) continue;
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

            _buildableRoadsCache[cacheKey] = (cityCount, beaconCount, result);
            return result;
        }

        /// <summary>
        /// Retourne les arêtes adjacentes au réseau de la civilisation qui sont bloquées par une route
        /// ennemie à distance ≤ 2 de sa ville (zone d'influence protégée).
        /// </summary>
        public List<Edge> GetEnemyProtectedRoadEdges(int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.GetCivilization(civilizationIndex)
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

            var civ = _state.GetCivilization(civilizationIndex)
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

            // Recompute distances for existing roads (only this layer — see ComputeRoadDistancesForCivilization)
            ComputeRoadDistancesForCivilization(civ, edge.Z);

            var distance = GetDistanceForEdge(edge, civ);
            if (distance == int.MaxValue)
                return null; // road must no longer be linked to a city

            var cost = (isVoidPath || isMaritimePath) ? GetMaritimeRoadCost() : ApplyUnderworldRoadCostAdjustments(GetRoadCost(distance, civ), edge, civ);

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

            ComputeRoadDistancesForCivilization(civ, edge.Z);
            InvalidateBuildableRoadsCacheForLayer(edge.Z);
            _state.Visibility.RecalculateFor(civilizationIndex);

            OnRoadBuilt?.Invoke(this, new RoadAutoBuiltEventArgs(civilizationIndex, edge));
            return road;
        }

        /// <summary>
        /// Vrai si le vertex est bordé par au moins deux hexagones de Vide — cible du sort Pont du Vide
        /// (<see cref="BuildVoidBridge"/>). Les trois hexagones doivent exister sur la carte du layer.
        /// </summary>
        public bool IsVoidBridgeVertex(Vertex vertex, IslandMap map)
        {
            int voidCount = 0;
            foreach (var hex in vertex.GetHexes())
            {
                var tile = map.GetTile(hex);
                if (tile == null) return false;
                if (tile.TerrainType == TerrainType.Void) voidCount++;
            }
            return voidCount >= 2;
        }

        /// <summary>
        /// Sort Pont du Vide : bâtit d'un coup, et gratuitement, les trois routes autour d'un vertex bordé
        /// de Vide — ni ressources, ni points de recherche (le coût est payé en cristaux par le sort), ni
        /// contrainte de raccordement au réseau. Les arêtes déjà occupées par une route de la civilisation
        /// ou protégées par une route ennemie proche de sa ville sont simplement ignorées ; les autres
        /// routes ennemies sont conquises comme lors d'une construction normale.
        /// Retourne le nombre de routes réellement posées (0 si le vertex n'offrait plus rien à bâtir).
        /// </summary>
        public int BuildVoidBridge(int civilizationIndex, Vertex vertex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.GetCivilization(civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            var map = _state.GetMapForZ(vertex.Z);
            if (map == null) return 0;

            var enemyProtectedEdges = new HashSet<Edge>(
                _state.Civilizations
                    .Where(c => c.Index != civilizationIndex)
                    .SelectMany(c => c.Roads)
                    .Where(r => r.Position.Z == vertex.Z && r.DistanceToNearestCity <= 2)
                    .Select(r => r.Position));

            var built = new List<Edge>();
            foreach (var edge in GetEdgesAtVertex(vertex))
            {
                if (!map.HasTile(edge.Hex1) || !map.HasTile(edge.Hex2)) continue;
                if (civ.Roads.Any(r => r.Position.Equals(edge))) continue;
                if (enemyProtectedEdges.Contains(edge)) continue;

                TryRemoveEnemyRoadAt(edge, civilizationIndex);
                civ.AddRoad(new Road(edge) { CivilizationIndex = civilizationIndex });
                built.Add(edge);
            }

            if (built.Count == 0) return 0;

            ComputeRoadDistancesForCivilization(civ, vertex.Z);
            InvalidateBuildableRoadsCacheForLayer(vertex.Z);
            _state.Visibility.RecalculateFor(civilizationIndex);

            // Même événement qu'une route bâtie à la main : c'est lui qui déclenche l'extension
            // automatique de la carte de l'Abysse (voir MainGameController.OnRoadBuiltExtendMap).
            foreach (var edge in built)
                OnRoadBuilt?.Invoke(this, new RoadAutoBuiltEventArgs(civilizationIndex, edge));

            return built.Count;
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
                    ComputeRoadDistancesForCivilization(otherCiv, edge.Z);
                    RemoveDisconnectedRoads(otherCiv);
                    InvalidateBuildableRoadsCacheForLayer(edge.Z);
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
            // Les routes du Vide (coûteuses en points de recherche — voir GetVoidRouteResearchCostFor)
            // ne sont jamais détruites, y compris par la perte de la ville qui les reliait : voir aussi
            // l'exclusion symétrique dans RemoveDisconnectedRoads.
            var toRemove = GetRoadsWithinDistanceOfVertex(civ.Roads, cityVertex, 2)
                .Where(r => !IsEdgeBetweenVoidHexes(r.Position));
            foreach (var road in toRemove)
                civ.RemoveRoad(road);

            ComputeRoadDistancesForCivilization(civ, cityVertex.Z);
            RemoveDisconnectedRoads(civ);

            InvalidateBuildableRoadsCacheForLayer(cityVertex.Z);
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

        /// <summary>
        /// Supprime les routes désormais déconnectées de toute ville — sauf les routes du Vide, jamais
        /// détruites même déconnectées (voir <see cref="OnCityDestroyed"/>) : elles restent en place,
        /// invisibles/inutilisables tant qu'aucune ville ne les reconnecte.
        /// </summary>
        private void RemoveDisconnectedRoads(Civilization civ)
        {
            civ.RemoveAllRoads(r => r.DistanceToNearestCity == int.MaxValue && !IsEdgeBetweenVoidHexes(r.Position));
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

        /// <summary>
        /// Recalcule les distances à la ville la plus proche pour les routes d'un seul layer de la
        /// civilisation. Un layer forme un graphe de vertex/edge totalement indépendant des autres
        /// (voir Vertex.Z / Edge.Z) : poser une route dans un layer ne peut jamais affecter les distances
        /// d'un autre layer, donc restreindre le recalcul au layer concerné évite de reparcourir toutes
        /// les routes cumulées de la civilisation sur tous les layers à chaque route posée.
        /// </summary>
        private void ComputeRoadDistancesForCivilization(Civilization civ, int layer)
        {
            var roads = civ.Roads.Where(r => r.Position.Z == layer).ToList();
            foreach (var r in roads)
                r.DistanceToNearestCity = int.MaxValue;

            var vertexToRoads = BuildVertexIndex(roads);
            var cityVertices = new HashSet<Vertex>(civ.Cities.Where(c => c.Position.Z == layer).Select(c => c.Position));
            var queue = new Queue<Road>();

            foreach (var r in roads)
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
            return road.Position.TouchesVertex(vertex);
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
            _state.Layers.TryGetValue(edge.Z, out var layerState);
            foreach (var v in edge.GetVertices())
            {
                bool touchesLand = v.GetHexes().Any(h => IsLandOrUnrevealedLand(h, mapTiles, layerState));
                bool hasOwnBeacon = civ.MaritimeBeacons.Any(b => b.Position.Equals(v));
                if (!touchesLand && !hasOwnBeacon) return false;
            }
            return true;
        }

        /// <summary>
        /// Vrai si l'hexagone est de la terre ferme, ou n'est pas encore généré sur une carte
        /// AutoExtend (Inframonde/Abysse) mais ne fait pas partie du tracé de rivière planifié —
        /// il deviendra donc de la terre ferme dès qu'il sera révélé. Sans ce cas, la toute première
        /// traversée de rivière serait impossible à construire : la rive opposée n'existe pas encore
        /// sur la carte tant qu'aucune route n'a été construite jusqu'à elle (voir
        /// AutoExtendController.TryExtendMapAfterRoad), or on ne peut construire cette route que si
        /// elle est déjà jugée valide.
        /// Sur une carte figée (île de surface), un hexagone absent reste traité comme de l'eau : il
        /// s'agit alors du bord de la carte (pleine mer), pas d'un futur hexagone à révéler.
        /// </summary>
        private static bool IsLandOrUnrevealedLand(HexCoord h, IReadOnlyDictionary<HexCoord, HexTile> mapTiles, LayerState? layerState)
        {
            if (mapTiles.TryGetValue(h, out var tile))
                return !tile.TerrainType.IsWater();
            if (layerState == null || !layerState.AutoExtend) return false;
            return !AutoExtendController.IsRiverHex(h, layerState);
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

        /// <summary>Coût en points de recherche de la première route du Vide.</summary>
        public const long VoidRouteBaseResearchCost = 1_000_000L;

        /// <summary>
        /// Coût en points de recherche d'une route du Vide supplémentaire : 1 000 000 × m^n,
        /// n étant le nombre de routes du Vide déjà construites par la civilisation et m le
        /// multiplicateur exponentiel (3 par défaut, abaissé jusqu'à 2 par l'Observatoire — voir
        /// <see cref="Observatory.GetVoidRouteCostMultiplierForLevel"/>).
        /// </summary>
        public static long GetVoidRouteResearchCost(int alreadyBuilt, double multiplier = Observatory.BaseVoidRouteCostMultiplier)
        {
            if (alreadyBuilt <= 0) return VoidRouteBaseResearchCost;
            double cost = VoidRouteBaseResearchCost * Math.Pow(multiplier, alreadyBuilt);
            return cost >= long.MaxValue ? long.MaxValue : (long)cost;
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

        /// <summary>Les trois arêtes qui se rejoignent sur ce vertex.</summary>
        public static Edge[] GetEdgesAtVertex(Vertex vertex)
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
            if (civ.GetUniqueBuilding(BuildingType.BuildersGuild) is BuildersGuild { Level: > 0 } guild)
                return guild.RoadCostReduction;
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
        /// (arrondi en faveur du joueur) dans l'exposant de <see cref="GetVoidRouteResearchCost"/> ;
        /// l'Observatoire, lui, abaisse le multiplicateur lui-même (voir
        /// <see cref="GetVoidRouteCostMultiplier"/>).
        /// </summary>
        private long GetVoidRouteResearchCostFor(Civilization civ)
        {
            int alreadyBuilt = civ.Roads.Count(r => IsEdgeBetweenVoidHexes(r.Position));
            if (civ.ModifierAggregator.HasModifier(Modifier.ECategory.VOID_ROUTE_COST_REDUCTION))
                alreadyBuilt /= 2;
            return GetVoidRouteResearchCost(alreadyBuilt, GetVoidRouteCostMultiplier());
        }

        /// <summary>
        /// Multiplicateur exponentiel courant du coût des routes du Vide : ×4 sans Observatoire,
        /// abaissé d'un pas par niveau jusqu'à ×3 une fois l'Observatoire complet. L'Observatoire est
        /// unique sur la carte (monument du joueur) : le multiplicateur vaut donc pour toutes les
        /// civilisations, comme les bonus de portée du Grand Phare.
        /// </summary>
        public double GetVoidRouteCostMultiplier()
        {
            var observatory = _state?.Features.OfType<Observatory>().FirstOrDefault();
            return observatory?.VoidRouteCostMultiplier ?? Observatory.BaseVoidRouteCostMultiplier;
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

        /// <summary>
        /// Coût réellement débité par <see cref="BuildRoad"/> à cette civilisation pour cette arête :
        /// route maritime/du Vide, ou coût de base majoré des surcoûts de l'Inframonde
        /// (<see cref="ApplyUnderworldRoadCostAdjustments"/>). C'est cette méthode, et jamais
        /// <see cref="GetRoadCost(int, Civilization?)"/> seule, que doit interroger tout appelant qui
        /// veut savoir ce qu'il lui manque — sur une arête de l'Inframonde, le coût de base ne
        /// mentionne ni le Minerai ni la Pierre, si bien qu'un stock de Bois/Brique au plafond
        /// paraît suffire (voir <see cref="CivilizationAutoplayer.TryBuildRoadOnce"/>, dont le troc
        /// automatique ne cherchait alors jamais à acheter ce qui bloquait vraiment).
        /// </summary>
        public ResourceSet GetRoadCostFor(Civilization civ, Edge edge)
        {
            if (IsEdgeBetweenVoidHexes(edge) || !IsEdgeOnLand(edge))
                return GetMaritimeRoadCost();
            var distance = GetDistanceForEdge(edge, civ);
            // Arête déconnectée du réseau : jamais constructible, donc ce coût n'est qu'indicatif —
            // mais distance² déborderait sur int.MaxValue.
            if (distance == int.MaxValue) distance = 1;
            var cost = GetRoadCost(distance, civ);
            return ApplyUnderworldRoadCostAdjustments(cost, edge, civ);
        }

        public ResourceSet GetPlayerRoadCost(Edge edge) => GetRoadCostFor(_state!.PlayerCivilization, edge);

        /// <summary>
        /// Applique au coût de base d'une route terrestre les majorations propres à l'Inframonde :
        /// surcoût fixe en Minerai/Pierre (réduit par UNDERWORLD_ROAD_BASE_REDUCTION), puis
        /// multiplication par la distance au vertex d'arrivée (élevée à la puissance 1.5). Utilisé à
        /// la fois par <see cref="GetPlayerRoadCost"/> (affichage tooltip) et par <see cref="BuildRoad"/>
        /// (coût réellement débité) afin que les deux restent cohérents.
        /// </summary>
        private ResourceSet ApplyUnderworldRoadCostAdjustments(ResourceSet cost, Edge edge, Civilization civ)
        {
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
