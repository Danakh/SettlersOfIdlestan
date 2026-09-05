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

        /// <summary>
        /// Purge le cache de routes constructibles d'une civilisation retirée du monde — voir
        /// <see cref="WorldState.CivilizationRemoved"/>. Une entrée par layer, chacune retenant une
        /// liste de routes ; les index de civilisation n'étant jamais recyclés, elles ne seraient
        /// autrement libérées qu'au changement d'île.
        /// </summary>
        internal void PurgeCivilizationCaches(int civilizationIndex)
        {
            foreach (var key in _buildableRoadsCache.Keys.Where(k => k.CivilizationIndex == civilizationIndex).ToList())
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
            catch (Exception ex) { GameLog.Error(nameof(RoadController), nameof(PerformBuildersGuildConstruction), ex); }
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

                long lastTick = guild.LastRoadBuildTick;
                long cycles = TickCooldown.ConsumeElapsedCycles(now, ref lastTick, effectiveCooldown);
                guild.LastRoadBuildTick = lastTick;
                if (cycles <= 0) continue;

                BuildRoadsForGuildBurst(civ, guild, cycles, surfaceEnabled, underworldEnabled);
            }
        }

        /// <summary>
        /// Pose jusqu'à <paramref name="cycles"/> routes automatiques pour une civilisation en un seul
        /// appel (rattrapage après un saut de temps ; un cycle = une route posée, comportement
        /// inchangé). S'arrête dès qu'un cycle ne trouve plus de route constructible — les cycles
        /// suivants échoueraient pour la même raison.
        ///
        /// Les deux layers (surface et Inframonde — l'Abysse n'a pas d'automatisation de routes)
        /// maintiennent chacun une liste de travail locale des arêtes constructibles, mise à jour de
        /// proche en proche à chaque route posée (une route n'ouvre que 0 à 2 nouvelles arêtes
        /// candidates, à son extrémité libre — voir <see cref="PatchCandidatesAfterBuild"/>), au lieu
        /// de rappeler <see cref="ComputeBuildableRoadsForLayer"/>/
        /// <see cref="ComputeRoadDistancesForCivilization"/>/
        /// <see cref="Model.IslandMap.WorldVisibility.RecalculateFor"/> — tous trois O(routes de la
        /// civilisation sur ce layer) — à chaque route posée. Sur un réseau de plusieurs milliers de
        /// routes (fin de partie), poser `cycles` routes en rattrapage coûtait O(routes × cycles) au
        /// lieu de O(routes + cycles) ; c'était le coût dominant restant après les correctifs de
        /// nettoyage des civs mortes et du cache d'automatisation des guildes (voir mémoire
        /// endgame_x10_freeze_investigation).
        ///
        /// L'Inframonde est une couche AutoExtend : y poser une route peut révéler de nouveaux
        /// hexagones de carte aux deux sommets de CETTE route (voir
        /// <see cref="AutoExtendController.TryExtendMapAfterRoad"/>, qui ne touche jamais que les
        /// hexagones à un pas de l'arête qui vient d'être construite). C'est pour ça que
        /// <see cref="OnAutoRoadBuilt"/> est levé ICI, avant le patch de la liste de travail, et non
        /// après comme le reste du contrôleur le fait ailleurs : le patch a besoin que ces hexagones
        /// existent déjà pour valider/générer correctement les candidats du troisième hexagone de
        /// cette route (sans ça, TryAddCandidate les rejetterait comme "hors carte"). Un vertex
        /// appartenant par ailleurs à une tout autre route déjà construite ailleurs sur la carte ne
        /// peut jamais devenir complet par cette révélation sans l'avoir déjà été : chacun de ses
        /// hexagones aurait alors déjà été révélé quand CETTE AUTRE route a été construite (même
        /// mécanisme). Le seul angle mort réel est donc un vertex qui touche à la fois le nouvel
        /// hexagone ET deux hexagones déjà là mais qui n'appartiennent à AUCUNE route/ville existante
        /// (candidat désormais valide mais jamais généré ici, puisqu'il ne touche pas la route qu'on
        /// vient de poser) — rattrapé par le recalcul complet et différé de fin de rafale
        /// (invalidation du cache ci-dessous), au pire un cycle plus tard.
        ///
        /// La visibilité, elle, reste recalculée à CHAQUE route de l'Inframonde (jamais différée) :
        /// c'est l'instantané "avant" que lit <c>TrySpawnAggressiveCivilization</c> (via
        /// <c>TryExtendMapAfterRoad</c> → <c>GetPlayerVisibleHexCoords</c>, qui lit directement
        /// <c>WorldVisibility.GetForZ</c> sans le recalculer) pour décider si un hexagone nouvellement
        /// révélé était déjà visible du joueur. <c>TryExtendMapAfterRoad</c> ne rappelle lui-même
        /// <c>RecalculateFor</c> que quand de nouveaux hexagones ont été ajoutés — jamais quand une
        /// route étend juste son propre rayon de vision sans en révéler — donc différer cet appel
        /// changerait quels hexagones comptent comme "nouveaux" pour ce mécanisme au fil d'une rafale
        /// à plusieurs cycles, un changement de comportement de jeu et pas seulement de performance.
        /// </summary>
        private void BuildRoadsForGuildBurst(Civilization civ, BuildersGuild guild, long cycles, bool surfaceEnabled, bool underworldEnabled)
        {
            LayerBurstContext? surfaceCtx = null;
            LayerBurstContext? underworldCtx = null;
            bool surfaceTouched = false;
            bool underworldTouched = false;

            for (long c = 0; c < cycles; c++)
            {
                Road? chosen = null;
                LayerBurstContext? chosenCtx = null;

                if (surfaceEnabled)
                {
                    surfaceCtx ??= SeedLayerBurstContext(civ, IslandMap.SurfaceLayer, guild.MaxAutoRoadDistance);
                    if (surfaceCtx.Working.Count > 0)
                    {
                        chosen = surfaceCtx.Working[_prng!.Next(surfaceCtx.Working.Count)];
                        chosenCtx = surfaceCtx;
                    }
                }

                // La guilde priorise la surface : l'Inframonde n'est considéré que si aucune route
                // de surface n'est disponible ce cycle.
                if (chosen == null && underworldEnabled)
                {
                    underworldCtx ??= SeedLayerBurstContext(civ, LayerState.UnderworldZ, guild.MaxAutoRoadDistance);
                    if (underworldCtx.Working.Count > 0)
                    {
                        chosen = underworldCtx.Working[_prng!.Next(underworldCtx.Working.Count)];
                        chosenCtx = underworldCtx;
                    }
                }

                if (chosen == null) break;

                TryRemoveEnemyRoadAt(chosen.Position, civ.Index);
                var road = new Road(chosen.Position) { CivilizationIndex = civ.Index, DistanceToNearestCity = chosen.DistanceToNearestCity };
                civ.AddRoad(road);

                bool onSurface = chosen.Position.Z == IslandMap.SurfaceLayer;
                if (onSurface) surfaceTouched = true; else underworldTouched = true;

                // Voir la doc de la méthode : doit être émis avant le patch pour l'Inframonde.
                OnAutoRoadBuilt?.Invoke(this, new RoadAutoBuiltEventArgs(civ.Index, chosen.Position));

                PatchCandidatesAfterBuild(civ, guild, chosenCtx!, road);

                // Seule la couche de la route posée a changé : les autres n'ont aucune raison d'être
                // reconstruites, et le sont à chaque route quand l'Inframonde est automatisé.
                if (!onSurface)
                    _state!.Visibility.RecalculateForLayer(civ.Index, chosen.Position.Z);
            }

            if (surfaceTouched)
            {
                ComputeRoadDistancesForCivilization(civ, IslandMap.SurfaceLayer);
                InvalidateBuildableRoadsCacheForLayer(IslandMap.SurfaceLayer);
                _state!.Visibility.RecalculateForLayer(civ.Index, IslandMap.SurfaceLayer);
            }
            if (underworldTouched)
            {
                ComputeRoadDistancesForCivilization(civ, LayerState.UnderworldZ);
                InvalidateBuildableRoadsCacheForLayer(LayerState.UnderworldZ);
                // Visibilité déjà tenue à jour route par route ci-dessus (voir doc) : pas de second
                // appel ici.
            }
        }

        /// <summary>
        /// Liste de travail des arêtes constructibles d'un layer pour une civilisation, maintenue
        /// pendant une rafale de <see cref="BuildRoadsForGuildBurst"/> et patchée de proche en proche
        /// au lieu d'être recalculée à chaque route posée.
        /// </summary>
        private sealed class LayerBurstContext
        {
            public readonly List<Road> Working;
            public readonly HashSet<Edge> OwnOccupied;
            public readonly HashSet<Edge> EnemyProtectedEdges;
            public readonly IReadOnlyDictionary<HexCoord, HexTile>? MapTiles;

            public LayerBurstContext(List<Road> working, HashSet<Edge> ownOccupied, HashSet<Edge> enemyProtectedEdges, IReadOnlyDictionary<HexCoord, HexTile>? mapTiles)
            {
                Working = working;
                OwnOccupied = ownOccupied;
                EnemyProtectedEdges = enemyProtectedEdges;
                MapTiles = mapTiles;
            }
        }

        /// <summary>
        /// Calcule le contexte de travail d'un layer pour une rafale : une seule fois par rafale
        /// (jamais par route posée), et en réutilisant le cache normal quand il est encore valide —
        /// sans ce HIT, une civilisation dont ce layer est saturé (plus aucune route à construire, cas
        /// fréquent une fois le territoire couvert) repayerait le calcul complet — le plus coûteux de
        /// tous, à cause de <see cref="HasEnemyCityAt"/> appelé pour chaque route existante — à CHAQUE
        /// événement d'horloge pour toujours, alors que rien n'a changé depuis la dernière fois. Sur un
        /// HIT, seuls ownOccupied/enemyProtectedEdges/mapTiles sont recalculés (nettement moins cher :
        /// aucun ne passe par <see cref="HasEnemyCityAt"/>), la liste de routes constructibles étant
        /// déjà bonne telle quelle.
        /// </summary>
        private LayerBurstContext SeedLayerBurstContext(Civilization civ, int layer, int maxAutoRoadDistance)
        {
            int cityCount = civ.Cities.Count(c => c.Position.Z == layer);
            int beaconCount = civ.MaritimeBeacons.Count(b => b.Position.Z == layer);
            var cacheKey = (civ.Index, layer);

            List<Road> roads;
            HashSet<Edge> ownOccupied;
            HashSet<Edge> enemyProtectedEdges;
            IReadOnlyDictionary<HexCoord, HexTile>? mapTiles;

            if (_buildableRoadsCache.TryGetValue(cacheKey, out var cached)
                && cached.CityCount == cityCount
                && cached.BeaconCount == beaconCount)
            {
                roads = cached.Roads;
                ownOccupied = new HashSet<Edge>(civ.Roads.Where(r => r.Position.Z == layer).Select(r => r.Position));
                enemyProtectedEdges = ComputeEnemyProtectedEdgesSet(civ, layer);
                mapTiles = _state!.GetMapForZ(layer)?.Tiles;
            }
            else
            {
                var computed = ComputeBuildableRoadsForLayer(civ, layer);
                _buildableRoadsCache[cacheKey] = (cityCount, beaconCount, computed.Roads);
                roads = computed.Roads;
                ownOccupied = computed.OwnOccupied;
                enemyProtectedEdges = computed.EnemyProtectedEdges;
                mapTiles = computed.MapTiles;
            }

            var working = roads.Where(r => r.DistanceToNearestCity <= maxAutoRoadDistance).ToList();
            return new LayerBurstContext(working, ownOccupied, enemyProtectedEdges, mapTiles);
        }

        private HashSet<Edge> ComputeEnemyProtectedEdgesSet(Civilization civ, int layer) =>
            new HashSet<Edge>(
                _state!.Civilizations
                    .Where(c => c.Index != civ.Index)
                    .SelectMany(c => c.Roads)
                    .Where(r => r.Position.Z == layer && r.DistanceToNearestCity <= 2)
                    .Select(r => r.Position));

        /// <summary>
        /// Ajoute à la liste de travail les 0 à 2 nouvelles arêtes candidates ouvertes par la route
        /// qui vient d'être posée (le troisième hexagone de chacun de ses deux sommets — même logique
        /// que la boucle "roadsInLayer" de <see cref="ComputeBuildableRoadsForLayer"/>, appliquée à une
        /// seule route au lieu de tout le réseau).
        /// </summary>
        private void PatchCandidatesAfterBuild(Civilization civ, BuildersGuild guild, LayerBurstContext ctx, Road built)
        {
            ctx.Working.RemoveAll(r => r.Position.Equals(built.Position));
            ctx.OwnOccupied.Add(built.Position);

            if (ctx.MapTiles == null) return;

            foreach (var vertex in built.Position.GetVertices())
            {
                if (HasEnemyCityAt(vertex, civ)) continue;
                var thirdHex = vertex.GetHexes().First(h => !h.Equals(built.Position.Hex1) && !h.Equals(built.Position.Hex2));
                TryAddCandidate(civ, guild, ctx, Edge.Create(built.Position.Hex1, thirdHex), built.DistanceToNearestCity + 1);
                TryAddCandidate(civ, guild, ctx, Edge.Create(built.Position.Hex2, thirdHex), built.DistanceToNearestCity + 1);
            }
        }

        /// <summary>
        /// Mêmes règles de validité qu'un candidat de <see cref="ComputeBuildableRoadsForLayer"/> (arête
        /// sur la carte, non occupée par nous, non protégée par un ennemi, terre/Vide/mer débloqué·e
        /// selon le cas), plus le filtre de distance maximale de la guilde (implicite dans
        /// <see cref="GetBuildableRoadsAtDistance"/> côté recalcul complet).
        /// </summary>
        private void TryAddCandidate(Civilization civ, BuildersGuild guild, LayerBurstContext ctx, Edge edge, int distance)
        {
            if (distance > guild.MaxAutoRoadDistance) return;
            if (ctx.MapTiles == null || !ctx.MapTiles.ContainsKey(edge.Hex1) || !ctx.MapTiles.ContainsKey(edge.Hex2)) return;
            if (ctx.OwnOccupied.Contains(edge) || ctx.EnemyProtectedEdges.Contains(edge)) return;
            if (ctx.Working.Any(r => r.Position.Equals(edge))) return;

            if (IsEdgeBetweenVoidHexes(edge))
            {
                if (!civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_VOID_ROUTES)) return;
            }
            else if (!IsEdgeOnLand(edge))
            {
                if (EdgeTouchesDeepWater(edge, civ)) return;
                if (!civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_MARITIME_ROUTES) || !IsValidMaritimeEdge(edge, civ)) return;
            }

            ctx.Working.Add(new Road(edge) { CivilizationIndex = civ.Index, DistanceToNearestCity = distance });
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
        ///
        /// Un HIT de cache doit rester un simple retour de <c>cached.Roads</c>, sans le moindre calcul
        /// supplémentaire : c'est le chemin emprunté par tous les appelants qui ne posent pas de route
        /// (UI, IA des PNJ qui n'ont pas de BuildersGuild — voir <see cref="BuildRoadsForGuildBurst"/>
        /// pour pourquoi seul le joueur en construit une). Un calcul même léger ajouté ici pénaliserait
        /// TOUS ces appels — mesuré en régression nette sur SOIBench la première fois que
        /// ownOccupied/enemyProtectedEdges ont été calculés dans cette méthode pour les besoins de la
        /// rafale de guilde (voir <see cref="ComputeBuildableRoadsForLayer"/>, qui les calcule à part).
        /// </summary>
        private List<Road> GetBuildableRoadsForLayer(Civilization civ, int layer)
        {
            int cityCount = civ.Cities.Count(c => c.Position.Z == layer);
            int beaconCount = civ.MaritimeBeacons.Count(b => b.Position.Z == layer);
            var cacheKey = (civ.Index, layer);

            if (_buildableRoadsCache.TryGetValue(cacheKey, out var cached)
                && cached.CityCount == cityCount
                && cached.BeaconCount == beaconCount)
                return cached.Roads;

            var computed = ComputeBuildableRoadsForLayer(civ, layer);
            _buildableRoadsCache[cacheKey] = (cityCount, beaconCount, computed.Roads);
            return computed.Roads;
        }

        /// <summary>
        /// Calcul complet (jamais depuis le cache) des routes constructibles d'un layer, avec les trois
        /// ensembles intermédiaires utilisés pour les construire (arêtes déjà occupées par nous, arêtes
        /// protégées par un ennemi proche, tuiles de la carte). <see cref="GetBuildableRoadsForLayer"/>
        /// n'en garde que la liste de routes (le reste est jeté après un HIT de cache, où il n'a même
        /// pas été calculé) ; <see cref="BuildRoadsForGuildBurst"/> en a besoin des quatre, une seule
        /// fois par rafale, pour patcher sa liste de travail de proche en proche au lieu de rappeler
        /// cette méthode — O(routes de la civilisation + routes des autres civs) — à chaque route posée.
        /// </summary>
        private (List<Road> Roads, HashSet<Edge> OwnOccupied, HashSet<Edge> EnemyProtectedEdges, IReadOnlyDictionary<HexCoord, HexTile>? MapTiles)
            ComputeBuildableRoadsForLayer(Civilization civ, int layer)
        {
            int civilizationIndex = civ.Index;
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
            // Positions des villes ennemies relevées une fois pour toutes : HasEnemyCityAt les
            // rebalayait, toutes civilisations confondues, pour chacun des deux vertex de chacune des
            // milliers de routes du layer.
            var enemyCityVertices = BuildEnemyCityVertexSet(civ, layer);

            foreach (var road in roadsInLayer)
            {
                foreach (var vertex in road.Position.GetVertices())
                {
                    if (enemyCityVertices.Contains(vertex)) continue;
                    var thirdHex = vertex.GetHexes().First(h => !h.Equals(road.Position.Hex1) && !h.Equals(road.Position.Hex2));
                    candidates.Add(Edge.Create(road.Position.Hex1, thirdHex));
                    candidates.Add(Edge.Create(road.Position.Hex2, thirdHex));
                }
            }

            var enemyProtectedEdges = ComputeEnemyProtectedEdgesSet(civ, layer);

            // Index vertex → routes et positions des villes, bâtis une fois pour toute la passe : sans
            // eux, GetDistanceForEdge rebalaye toutes les routes et toutes les villes de la
            // civilisation pour chacun des deux vertex de chaque candidat (voir sa variante indexée).
            var cityVertices = new HashSet<Vertex>();
            for (int i = 0; i < citiesInLayer.Count; i++)
                cityVertices.Add(citiesInLayer[i].Position);
            var vertexToRoads = BuildVertexIndex(roadsInLayer);

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
                    if (EdgeTouchesDeepWater(edge, civ))
                        continue;
                    if (!civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_MARITIME_ROUTES)
                        || !IsValidMaritimeEdge(edge, civ))
                        continue;
                }

                var road = new Road(edge) { CivilizationIndex = civilizationIndex };
                // assign a distance so callers can know the build cost
                road.DistanceToNearestCity = GetDistanceForEdge(edge, cityVertices, vertexToRoads);
                result.Add(road);
            }

            return (result, ownOccupied, enemyProtectedEdges, mapTiles);
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
                if (EdgeTouchesDeepWater(edge, civ))
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
        /// Vrai si l'arête sépare deux hexagones de Vide — cible du sort Pont du Vide
        /// (<see cref="BuildVoidBridge"/>). Les deux hexagones doivent exister sur la carte fournie.
        /// </summary>
        public bool IsVoidBridgeEdge(Edge edge, IslandMap map)
        {
            var tile1 = map.GetTile(edge.Hex1);
            var tile2 = map.GetTile(edge.Hex2);
            return tile1?.TerrainType == TerrainType.Void && tile2?.TerrainType == TerrainType.Void;
        }

        /// <summary>
        /// Sort Pont du Vide : bâtit gratuitement la route du Vide ciblée — ni ressources, ni points de
        /// recherche (le coût est payé en cristaux par le sort), ni contrainte de raccordement au réseau.
        /// Une arête déjà occupée par une route de la civilisation, ou protégée par une route ennemie
        /// proche de sa ville, n'est pas bâtie ; les autres routes ennemies sont conquises comme lors
        /// d'une construction normale.
        /// Retourne vrai si la route a réellement été posée.
        /// </summary>
        public bool BuildVoidBridge(int civilizationIndex, Edge edge)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.GetCivilization(civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            var map = _state.GetMapForZ(edge.Z);
            if (map == null) return false;
            if (!map.HasTile(edge.Hex1) || !map.HasTile(edge.Hex2)) return false;
            if (civ.Roads.Any(r => r.Position.Equals(edge))) return false;

            bool isEnemyProtected = _state.Civilizations
                .Where(c => c.Index != civilizationIndex)
                .SelectMany(c => c.Roads)
                .Any(r => r.Position.Equals(edge) && r.DistanceToNearestCity <= 2);
            if (isEnemyProtected) return false;

            TryRemoveEnemyRoadAt(edge, civilizationIndex);
            civ.AddRoad(new Road(edge) { CivilizationIndex = civilizationIndex, BuiltBySpell = true });

            ComputeRoadDistancesForCivilization(civ, edge.Z);
            InvalidateBuildableRoadsCacheForLayer(edge.Z);
            _state.Visibility.RecalculateFor(civilizationIndex);

            // Même événement qu'une route bâtie à la main : c'est lui qui déclenche l'extension
            // automatique de la carte de l'Abysse (voir MainGameController.OnRoadBuiltExtendMap).
            OnRoadBuilt?.Invoke(this, new RoadAutoBuiltEventArgs(civilizationIndex, edge));

            return true;
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

        /// <summary>
        /// À appeler après qu'une ville a été fondée. Une nouvelle ville peut raccourcir la distance
        /// jusqu'à des routes déjà construites (raccourci), ce qui les rendrait éligibles à
        /// l'automatisation de la guilde des bâtisseurs (<see cref="BuildersGuild.MaxAutoRoadDistance"/>)
        /// alors qu'elles ne l'étaient pas avant. <see cref="BuildRoadsForGuildBurst"/> filtre sur le
        /// champ <see cref="Road.DistanceToNearestCity"/> figé sur chaque route lors de son dernier
        /// recalcul : sans cet appel, il reste celui d'avant la nouvelle ville et la construction
        /// automatique de routes semble ne jamais reprendre autour d'elle, même une fois son propre
        /// réseau immédiat saturé.
        /// </summary>
        public void OnCityBuilt(Civilization civ, Vertex cityVertex)
        {
            ComputeRoadDistancesForCivilization(civ, cityVertex.Z);
            InvalidateBuildableRoadsCacheForLayer(cityVertex.Z);
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
        /// Positions des villes des <b>autres</b> civilisations sur ce layer — même réponse que
        /// <see cref="HasEnemyCityAt"/>, relevée une fois au lieu d'être rebalayée par vertex.
        /// </summary>
        private HashSet<Vertex> BuildEnemyCityVertexSet(Civilization civ, int layer)
        {
            var set = new HashSet<Vertex>();
            if (_state == null) return set;

            var civilizations = _state.Civilizations;
            for (int i = 0; i < civilizations.Count; i++)
            {
                if (civilizations[i].Index == civ.Index) continue;
                var cities = civilizations[i].Cities;
                for (int c = 0; c < cities.Count; c++)
                    if (cities[c].Position.Z == layer)
                        set.Add(cities[c].Position);
            }
            return set;
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
            => GetDistanceForEdge(edge, BuildCityVertexSet(civ, edge.Z), BuildVertexIndex(RoadsInLayer(civ, edge.Z)));

        /// <summary>
        /// Variante indexée de <see cref="GetDistanceForEdge(Edge, Civilization)"/>, à utiliser dès
        /// qu'on interroge plus d'une arête d'un même layer.
        ///
        /// <para>La version non indexée répond à « quelles routes touchent ce vertex ? » par un
        /// balayage des routes de la civilisation — toutes couches confondues — et à « une ville
        /// est-elle ici ? » par un balayage de ses villes. Posée pour les deux vertex de chaque arête
        /// candidate, elle rendait <see cref="ComputeBuildableRoadsForLayer"/> quadratique :
        /// candidats × routes. Sur une sauvegarde de fin de partie avec l'automatisation des routes
        /// active, ce seul calcul prenait 28 ms par appel et faisait à lui seul tout le coût du
        /// contrôleur pendant un saut de temps.</para>
        /// </summary>
        private static int GetDistanceForEdge(Edge edge, HashSet<Vertex> cityVertices, Dictionary<Vertex, List<Road>> vertexToRoads)
        {
            var vertices = edge.GetVertices();

            int min = int.MaxValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i];
                if (cityVertices.Contains(v))
                    min = Math.Min(min, 1);

                if (!vertexToRoads.TryGetValue(v, out var touchingRoads)) continue;
                for (int r = 0; r < touchingRoads.Count; r++)
                {
                    int distance = touchingRoads[r].DistanceToNearestCity;
                    if (distance != int.MaxValue)
                        min = Math.Min(min, distance + 1);
                }
            }

            return min;
        }

        /// <summary>Positions des villes de la civilisation sur ce layer, en ensemble — voir <see cref="GetDistanceForEdge(Edge, HashSet{Vertex}, Dictionary{Vertex, List{Road}})"/>.</summary>
        private static HashSet<Vertex> BuildCityVertexSet(Civilization civ, int layer)
        {
            var set = new HashSet<Vertex>();
            var cities = civ.Cities;
            for (int i = 0; i < cities.Count; i++)
                if (cities[i].Position.Z == layer)
                    set.Add(cities[i].Position);
            return set;
        }

        /// <summary>Routes de la civilisation sur ce layer, sans allocation LINQ.</summary>
        private static List<Road> RoadsInLayer(Civilization civ, int layer)
        {
            var result = new List<Road>();
            var roads = civ.Roads;
            for (int i = 0; i < roads.Count; i++)
                if (roads[i].Position.Z == layer)
                    result.Add(roads[i]);
            return result;
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
        /// <paramref name="alreadyBuilt"/> est un <c>double</c> : Cartographie du Vide n'en garde
        /// que les deux tiers, et cette fraction doit rester à l'exposant sans être arrondie (voir
        /// <see cref="GetVoidRouteResearchCostFor"/>).
        /// </summary>
        public static long GetVoidRouteResearchCost(double alreadyBuilt, double multiplier = Observatory.BaseVoidRouteCostMultiplier)
        {
            if (alreadyBuilt <= 0) return VoidRouteBaseResearchCost;
            double cost = VoidRouteBaseResearchCost * Math.Pow(multiplier, alreadyBuilt);
            return cost >= long.MaxValue ? long.MaxValue : (long)cost;
        }

        /// <summary>
        /// Vrai si l'un des deux hexagones de l'arête est de l'eau profonde (bordure cosmétique,
        /// jamais traversable ni constructible — voir <see cref="TerrainTypeExtensions.IsWater"/>) —
        /// sauf si <paramref name="civ"/> possède MARITIME_BEACON_DEEP_WATER_PLACEMENT (Grotte aux
        /// Perles, Sirènes) et que l'arête rejoint l'un de ses vertex : la balise en Eau profonde ne
        /// serait sinon jamais atteignable par route (voir MaritimeBeaconController.GetBuildableVertices).
        /// </summary>
        private bool EdgeTouchesDeepWater(Edge edge, Civilization civ)
        {
            if (_state == null) return false;
            var mapTiles = _state.GetMapFor(edge)?.Tiles;
            if (mapTiles == null) return false;
            bool hex1IsDeepWater = mapTiles.TryGetValue(edge.Hex1, out var tile1) && tile1.TerrainType == TerrainType.DeepWater;
            bool hex2IsDeepWater = mapTiles.TryGetValue(edge.Hex2, out var tile2) && tile2.TerrainType == TerrainType.DeepWater;
            if (!hex1IsDeepWater && !hex2IsDeepWater) return false;

            if (civ.ModifierAggregator.HasModifier(Modifier.ECategory.MARITIME_BEACON_DEEP_WATER_PLACEMENT)
                && edge.GetVertices().Any(v => civ.MaritimeBeacons.Any(b => b.Position.Equals(v))))
                return false;

            return true;
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
        /// Coût de la prochaine route du Vide pour cette civilisation. Les routes posées gratuitement
        /// par le sort Pont du Vide (<see cref="Road.BuiltBySpell"/>) ne comptent pas dans l'exposant :
        /// le sort ne doit pas renchérir les routes du Vide classiques. Avec Cartographie du Vide
        /// (VOID_ROUTE_COST_REDUCTION), les routes déjà bâties (hors sort) ne comptent que pour deux
        /// tiers dans l'exposant de <see cref="GetVoidRouteResearchCost"/> — fraction laissée telle
        /// quelle : l'arrondir avant de l'élever en puissance faisait des paliers de trois routes au
        /// même prix, puis un saut au cube du multiplicateur. L'Observatoire, lui, abaisse le
        /// multiplicateur lui-même (voir <see cref="GetVoidRouteCostMultiplier"/>).
        /// </summary>
        private long GetVoidRouteResearchCostFor(Civilization civ)
        {
            double alreadyBuilt = civ.Roads.Count(r => !r.BuiltBySpell && IsEdgeBetweenVoidHexes(r.Position));
            if (civ.ModifierAggregator.HasModifier(Modifier.ECategory.VOID_ROUTE_COST_REDUCTION))
                alreadyBuilt = alreadyBuilt * 2 / 3;
            return GetVoidRouteResearchCost(alreadyBuilt, GetVoidRouteCostMultiplier());
        }

        /// <summary>
        /// Multiplicateur exponentiel courant du coût des routes du Vide : ×3 sans Observatoire,
        /// abaissé d'un pas par niveau jusqu'à ×2 une fois l'Observatoire complet. L'Observatoire est
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
