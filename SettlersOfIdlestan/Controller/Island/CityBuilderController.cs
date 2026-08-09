using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.GameplayModifier;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using SettlersOfIdlestan.Model.IslandFeatures;

namespace SettlersOfIdlestan.Controller.Island
{
    public class OutpostAutoBuiltEventArgs : EventArgs
    {
        public int CivilizationIndex { get; }
        public Vertex Position { get; }

        public OutpostAutoBuiltEventArgs(int civIndex, Vertex position)
        {
            CivilizationIndex = civIndex;
            Position = position;
        }
    }

    /// <summary>What caused a city to be destroyed — lets subscribers of <see cref="CityBuilderController.OnCityDestroyed"/>
    /// distinguish military conquest from monster attacks where that matters (e.g. task/achievement tracking).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<CityDestructionCause>))]
    public enum CityDestructionCause
    {
        Combat,
        Monster,
    }

    public class CityDestroyedEventArgs : EventArgs
    {
        public Vertex CityVertex { get; }
        public int CivilizationIndex { get; }
        public CityDestructionCause Cause { get; }

        public CityDestroyedEventArgs(Vertex cityVertex, int civilizationIndex, CityDestructionCause cause)
        {
            CityVertex = cityVertex;
            CivilizationIndex = civilizationIndex;
            Cause = cause;
        }
    }

    /// <summary>
    /// Controller handling city construction.
    /// </summary>
    public class CityBuilderController
    {
        private WorldState? _state;
        private GameClock? _clock;
        private GamePRNG? _prng;
        private readonly Dictionary<int, (int RoadCount, int TotalCityCount, int BeaconCount, int LandingSiteCount, int TerrainVersion, List<Vertex> Vertices)> _buildableVerticesCache = new();
        private readonly Dictionary<(TerrainType Terrain, int Range), (int TerrainVersion, HashSet<Vertex> Vertices)> _terrainRangeVerticesCache = new();

        // 10 s × 100 ticks/s
        public const long AutoOutpostBuildCooldownTicks = 1000L;

        public event EventHandler<OutpostAutoBuiltEventArgs>? OnAutoOutpostBuilt;
        public event EventHandler<OutpostAutoBuiltEventArgs>? OnCityBuilt;
        public event EventHandler<CityDestroyedEventArgs>? OnCityDestroyed;

        /// <summary>Fired after <see cref="RelocateCity"/> moves a city to its new vertex — distinct from
        /// <see cref="OnCityBuilt"/> so subscribers that count/log new-city creation (tasks, history) don't
        /// mistake a relocation for one. MainGameController still uses it to destroy any Camp Mobile now
        /// sitting under the city, same as MobileCampController.DestroyCampsNear on OnCityBuilt.</summary>
        public event EventHandler<OutpostAutoBuiltEventArgs>? OnCityRelocated;

        internal CityBuilderController(WorldState? state = null)
        {
            _state = state;
        }

        /// <summary>
        /// Initialize or update the WorldState for this controller.
        /// </summary>
        internal void Initialize(WorldState state, GameClock? clock = null, GamePRNG? prng = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state ?? throw new ArgumentNullException(nameof(state));
            _buildableVerticesCache.Clear();
            _clock = clock;
            if (prng != null) _prng = prng;

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { PerformBuildersGuildOutpostConstruction(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CityBuilderController] {nameof(PerformBuildersGuildOutpostConstruction)}: {ex}"); }
        }

        private void PerformBuildersGuildOutpostConstruction()
        {
            if (_state == null || _clock == null) return;

            long now = _clock.CurrentTick;
            var civ = _state.PlayerCivilization;

            BuildersGuild? guild = null;
            foreach (var city in civ.Cities)
            {
                guild = city.FindBuilding<BuildersGuild>(BuildingType.BuildersGuild);
                if (guild != null) break;
            }

            if (guild == null || guild.Level < 4) return;

            bool underworldUnlocked = civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_BUILDERS_GUILD_UNDERWORLD);
            bool surfaceEnabled = _state.AutomationSettings.IsOutpostAutomationActive;
            bool underworldEnabled = underworldUnlocked && _state.AutomationSettings.IsOutpostAutomationActiveUnderworld;

            // Keep timer running even when disabled to avoid burst on re-enable
            if (!surfaceEnabled && !underworldEnabled)
            {
                guild.LastOutpostBuildTick = now;
                return;
            }

            if (guild.LastOutpostBuildTick == 0)
            {
                guild.LastOutpostBuildTick = now;
                return;
            }

            if (now - guild.LastOutpostBuildTick < AutoOutpostBuildCooldownTicks) return;

            guild.LastOutpostBuildTick = now;

            var allBuildable = GetBuildableVertices(civ.Index);
            var buildable = new List<Vertex>();
            if (surfaceEnabled) buildable.AddRange(allBuildable.Where(v => v.Z == IslandMap.SurfaceLayer));

            // La guilde priorise la surface : l'Inframonde n'est considéré que si aucun avant-poste
            // de surface n'est disponible ce tick.
            if (buildable.Count == 0 && underworldEnabled)
                buildable.AddRange(allBuildable.Where(v => v.Z == LayerState.UnderworldZ));
            if (buildable.Count == 0) return;

            var chosen = buildable[_prng!.Next(buildable.Count)];
            if (!civ.CanPayResourceCost(NewCityBuildingCostFor(chosen, civ))) return;

            try
            {
                BuildCity(civ.Index, chosen);
                OnAutoOutpostBuilt?.Invoke(this, new OutpostAutoBuiltEventArgs(civ.Index, chosen));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CityBuilderController] BuildCity at {chosen}: {ex}"); }
        }

        /// <summary>
        /// Returns vertices where the civilization can build a city (outpost).
        /// Rules (simple):
        /// - vertex not already occupied by any IBuildVertex (city, Flotte de Guerre or Balise Maritime —
        ///   see WarFleetController, which builds fleets on beacons instead of classic cities), except
        ///   a Camp Mobile belonging to this same civilization, which does not block city construction
        ///   at its vertex (see MobileCampController.DestroyCampsNear, triggered by OnCityBuilt, which
        ///   removes it once the city is created — the UI is expected to still require destroying it
        ///   manually first, this model-level allowance only exists so a coincident city build isn't
        ///   rejected outright)
        /// - vertex touches at least one road of the civilization
        /// - no city of another civilization is at distance < 2 (at least 2 edges required between civs)
        /// - no existing city of the same civilization is at distance < 3
        /// Flottes de Guerre live outside <see cref="Civilization.Cities"/> (see IMilitaryVertex) so they
        /// never enter these distance checks at all — no distance limit between a fleet and a city.
        /// </summary>
        /// <summary>
        /// Retourne tous les vertex touchant au moins une route de la civilisation, sans aucun autre
        /// filtre (occupation, distance...). Sert de bassin de candidats à GetBuildableVertices ci-dessous,
        /// et à MobileCampController pour proposer un Camp Mobile là où un avant-poste ne peut pas être bâti.
        /// </summary>
        public List<Vertex> GetRoadTouchingVertices(int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.GetCivilization(civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            // Copie : le collecteur rend un tampon partagé, que l'appelant public ne doit pas voir
            // se faire écraser par la prochaine collecte.
            return new List<Vertex>(CollectRoadTouchingVertices(civ, out _));
        }

        // Tampons de GetBuildableVertices, réutilisés d'un appel à l'autre. Le cache de résultat de
        // GetBuildableVertices est invalidé dès qu'une route est posée — c'est-à-dire en permanence
        // pendant l'autoplay des PNJ — et chaque reconstruction repartait de collections neuves de
        // plusieurs milliers d'entrées. Les ensembles à clé Vertex étaient, et de loin, le premier
        // poste d'allocation restant de la simulation. Aucune réentrance possible : ni
        // AddFlightCandidateVertices ni les filtres de terrain ne rappellent GetBuildableVertices.
        private readonly List<Vertex> _roadTouchingScratch = new();
        private readonly HashSet<Vertex> _roadTouchingUniqueScratch = new();
        private readonly HashSet<Vertex> _occupiedVerticesScratch = new();
        private readonly HashSet<Vertex> _blockedVerticesScratch = new();

        /// <summary>
        /// Coeur de <see cref="GetRoadTouchingVertices"/>, qui expose en plus le HashSet de
        /// déduplication : GetBuildableVertices le réutilise tel quel comme ensemble « déjà connu »
        /// du BFS de Vol, au lieu de le reconstruire. La déduplication passe par ce HashSet et non
        /// par un scan linéaire de la liste — le nombre de routes se compte en milliers en fin de
        /// partie, et le scan rendait la collecte quadratique. L'ordre d'insertion est conservé
        /// (le résultat alimente un choix PRNG, il doit rester déterministe).
        ///
        /// <para>La liste et l'ensemble rendus sont des <b>tampons partagés</b>, valables jusqu'au
        /// prochain appel.</para>
        /// </summary>
        private List<Vertex> CollectRoadTouchingVertices(Civilization civ, out HashSet<Vertex> unique)
        {
            var vertices = _roadTouchingScratch;
            unique = _roadTouchingUniqueScratch;
            vertices.Clear();
            unique.Clear();

            var roads = civ.Roads;
            for (int i = 0; i < roads.Count; i++)
            {
                foreach (var v in roads[i].Position.GetVertices())
                {
                    if (unique.Add(v))
                        vertices.Add(v);
                }
            }
            return vertices;
        }

        /// <param name="excludingCity">If set, this city is ignored by the same-civilization distance check —
        /// used for relocation, to test constructibility as if the city had not been placed yet.</param>
        public List<Vertex> GetBuildableVertices(int civilizationIndex, City? excludingCity = null)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.GetCivilization(civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            // Restrictions raciales (voir RaceDefinitions) : distance minimale entre villes propres
            // éventuellement remplacée (Gobelins 2, Géants 4), adjacence de terrain exigée en
            // surface (Elfes → Forêt, Nains → Montagne), portée de terrain (Sirènes → jusqu'à 2
            // arêtes de l'Eau) et portée de Vol (Garudas).
            var requiredTerrains = GetRequiredCityPlacementTerrains(civ);
            var requiredTerrainRanges = GetRequiredCityPlacementTerrainRanges(civ);
            int minOwnCityDistance = GetMinDistanceBetweenCivilizationCities(civ);
            int flightRange = civ.ModifierAggregator.ApplyModifiers(ECategory.CITY_PLACEMENT_FLYING, "", 0);

            // Result only depends on this civ's roads and on every civ's cities/beacons (positions, via
            // count as a cheap proxy — RelocateCity clears the cache explicitly since it changes a
            // position without changing any count), plus les terrains via WorldState.TerrainVersion
            // (les restrictions raciales filtrent sur le terrain, que Marche de Dieu peut transformer
            // sans toucher à aucun compteur).
            int totalCityCount = _state.Civilizations.Sum(c => c.Cities.Count);
            int totalBeaconCount = _state.Civilizations.Sum(c => c.MaritimeBeacons.Count);
            int totalLandingSiteCount = _state.Civilizations.Sum(c => c.LandingSites.Count);
            if (excludingCity == null &&
                _buildableVerticesCache.TryGetValue(civilizationIndex, out var cached) &&
                cached.RoadCount == civ.Roads.Count &&
                cached.TotalCityCount == totalCityCount &&
                cached.BeaconCount == totalBeaconCount &&
                cached.LandingSiteCount == totalLandingSiteCount &&
                cached.TerrainVersion == _state.TerrainVersion)
                return cached.Vertices;

            var vertices = CollectRoadTouchingVertices(civ, out var knownVertices);

            if (flightRange > 0)
                AddFlightCandidateVertices(vertices, knownVertices, civ, flightRange, excludingCity);

            // Un Camp Mobile de cette même civilisation n'empêche pas d'y bâtir une ville par-dessus
            // (voir doc de GetBuildableVertices) — seuls les IBuildVertex d'autres civilisations, ou
            // les propres villes/flottes/balises, comptent comme occupation ici.
            var occupiedVertices = _occupiedVerticesScratch;
            occupiedVertices.Clear();
            foreach (var bv in _state.GetAllBuildVertices())
                if (!(bv is MobileCamp camp && camp.CivilizationIndex == civilizationIndex))
                    occupiedVertices.Add(bv.Position);

            // Contraintes de distance : plutôt que de mesurer chaque candidat contre chaque ville
            // (produit cartésien candidats × villes, les deux se comptant en centaines/milliers en
            // fin de partie), on projette une fois pour toutes le voisinage interdit autour des
            // villes. Le rayon interdit vaut distanceMin - 1 arêtes, et le BFS reste naturellement
            // sur la couche de chaque ville (les vertex voisins partagent le Z), ce qui reproduit
            // le filtre par Z de l'ancienne version.
            // Les Sites d'Arrivée (LandingSite) comptent comme des villes dans ces rayons : ils
            // réservent la place de la future ville de surface d'une race démarrant sous terre, et
            // doivent donc repousser les voisins exactement comme le ferait la ville elle-même.
            var blockedVertices = _blockedVerticesScratch;
            blockedVertices.Clear();
            AddVerticesWithinRadius(blockedVertices,
                _state.Civilizations.Where(c => c.Index != civilizationIndex)
                    .SelectMany(c => c.Cities.Select(city => city.Position).Concat(c.LandingSites.Select(s => s.Position))),
                MinDistanceBetweenCities - 1);
            AddVerticesWithinRadius(blockedVertices,
                civ.Cities.Where(city => city != excludingCity).Select(city => city.Position)
                    .Concat(civ.LandingSites.Select(s => s.Position)),
                minOwnCityDistance - 1);

            // Restrictions raciales de terrain : les ensembles de portée ne dépendent que du terrain,
            // on les résout une fois ici au lieu d'un lookup de cache par candidat.
            var terrainRangeSets = BuildTerrainRangeSets(requiredTerrainRanges);

            vertices = vertices.Where(v =>
                !occupiedVertices.Contains(v) &&
                !blockedVertices.Contains(v) &&
                SatisfiesCityTerrainRestriction(v, requiredTerrains, terrainRangeSets))
                .ToList();

            if (excludingCity == null)
                _buildableVerticesCache[civilizationIndex] = (civ.Roads.Count, totalCityCount, totalBeaconCount, totalLandingSiteCount, _state.TerrainVersion, vertices);

            return vertices;
        }

        /// <summary>
        /// Vol (CITY_PLACEMENT_FLYING, Garudas) : ajoute au bassin de candidats les vertex de
        /// surface atteignables en volant, c'est-à-dire à au plus <paramref name="flightRange"/>
        /// arêtes d'une ville de surface de la civilisation, sans exiger de route. Le survol de
        /// l'eau est permis (le parcours traverse les vertex tout-eau) mais on ne se pose que sur
        /// un vertex touchant au moins un hex terrestre. Strictement limité à la surface : les
        /// villes d'Inframonde/Abysse ne génèrent aucun candidat (le BFS ne part que des villes de
        /// surface et reste sur leur couche — les vertex adjacents partagent le Z). Les filtres
        /// avals (occupation, distances, terrain racial) s'appliquent ensuite normalement.
        /// BFS à ordre stable (Queue + List) : le résultat alimente le choix PRNG des avant-postes
        /// automatiques, l'ordre doit être déterministe.
        /// </summary>
        private void AddFlightCandidateVertices(List<Vertex> vertices, HashSet<Vertex> known, Civilization civ, int flightRange, City? excludingCity)
        {
            var map = _state!.GetMapForZ(IslandMap.SurfaceLayer);
            if (map == null) return;

            var visited = new HashSet<Vertex>();
            var queue = new Queue<(Vertex Vertex, int Depth)>();

            foreach (var city in civ.Cities)
            {
                if (city == excludingCity || city.Position.Z != IslandMap.SurfaceLayer) continue;
                if (visited.Add(city.Position))
                    queue.Enqueue((city.Position, 0));
            }

            while (queue.Count > 0)
            {
                var (vertex, depth) = queue.Dequeue();
                if (depth >= flightRange) continue;
                foreach (var neighbor in vertex.GetAdjacentVertices())
                {
                    if (!visited.Add(neighbor)) continue;
                    queue.Enqueue((neighbor, depth + 1));
                    if (TouchesLand(map, neighbor) && known.Add(neighbor))
                        vertices.Add(neighbor);
                }
            }
        }

        /// <summary>
        /// BFS multi-sources sur les arêtes : ajoute à <paramref name="result"/> toutes les origines
        /// et tout vertex à au plus <paramref name="radius"/> arêtes de l'une d'elles. Un rayon
        /// négatif n'ajoute rien (distance minimale nulle = aucun blocage). Les origines de couches
        /// différentes peuvent être mélangées : deux vertex de Z différents ne sont jamais égaux,
        /// donc chaque branche du BFS reste sur sa couche.
        /// </summary>
        private static void AddVerticesWithinRadius(HashSet<Vertex> result, IEnumerable<Vertex> origins, int radius)
        {
            if (radius < 0) return;

            var frontier = new List<Vertex>();
            var visited = new HashSet<Vertex>();
            foreach (var origin in origins)
                if (visited.Add(origin))
                {
                    result.Add(origin);
                    frontier.Add(origin);
                }

            for (int depth = 0; depth < radius && frontier.Count > 0; depth++)
            {
                var next = new List<Vertex>();
                foreach (var vertex in frontier)
                    foreach (var neighbor in vertex.GetAdjacentVertices())
                        if (visited.Add(neighbor))
                        {
                            result.Add(neighbor);
                            next.Add(neighbor);
                        }
                frontier = next;
            }
        }

        /// <summary>Vrai si le vertex touche au moins un hex existant ni eau ni Vide.</summary>
        private static bool TouchesLand(IslandMap map, Vertex vertex)
            => vertex.GetHexes().Any(h => map.GetTile(h) is { } tile
                && !tile.TerrainType.IsWater() && !tile.TerrainType.IsVoid());

        /// <summary>
        /// Terrains dont l'un au moins doit toucher tout nouveau vertex de ville en surface pour
        /// cette civilisation (CITY_PLACEMENT_REQUIRES_TERRAIN, restriction raciale — vide pour les
        /// PNJ et les races sans restriction).
        /// </summary>
        private static List<TerrainType> GetRequiredCityPlacementTerrains(Civilization civ)
        {
            var terrains = new List<TerrainType>();
            foreach (var sub in civ.ModifierAggregator.GetActiveSubCategories(ECategory.CITY_PLACEMENT_REQUIRES_TERRAIN))
                if (Enum.TryParse<TerrainType>(sub, out var terrain))
                    terrains.Add(terrain);
            return terrains;
        }

        /// <summary>
        /// Vrai si le vertex respecte la restriction raciale de terrain (adjacence stricte OU
        /// portée). Seule la surface est concernée : l'Inframonde et l'Abysse restent libres (leurs
        /// terrains propres rendraient toute restriction de surface injouable).
        /// </summary>
        /// <param name="terrainRangeSets">Ensembles pré-calculés par <see cref="BuildTerrainRangeSets"/>,
        /// ou <c>null</c> si aucune portée de terrain n'est exigée.</param>
        private bool SatisfiesCityTerrainRestriction(Vertex vertex, List<TerrainType> requiredTerrains,
            List<HashSet<Vertex>>? terrainRangeSets)
        {
            if (requiredTerrains.Count == 0 && terrainRangeSets == null) return true;
            if (vertex.Z != IslandMap.SurfaceLayer) return true;

            var map = _state!.GetMapFor(vertex);
            if (map == null) return false;

            foreach (var terrain in requiredTerrains)
                if (map.VertexHasTerrainType(vertex, terrain))
                    return true;

            if (terrainRangeSets != null)
                foreach (var set in terrainRangeSets)
                    if (set.Contains(vertex))
                        return true;

            return false;
        }

        /// <summary>
        /// Résout les ensembles de vertex de chaque portée de terrain exigée. Retourne <c>null</c>
        /// si aucune portée n'est exigée. Les restrictions ne s'appliquant qu'en surface, la carte
        /// utilisée est toujours celle de la surface.
        /// </summary>
        private List<HashSet<Vertex>>? BuildTerrainRangeSets(List<(TerrainType Terrain, int Range)> requiredTerrainRanges)
        {
            if (requiredTerrainRanges.Count == 0) return null;

            var sets = new List<HashSet<Vertex>>(requiredTerrainRanges.Count);
            var map = _state!.GetMapForZ(IslandMap.SurfaceLayer);
            if (map == null) return sets;

            foreach (var (terrain, range) in requiredTerrainRanges)
                sets.Add(GetVerticesWithinRangeOfTerrain(map, terrain, range));
            return sets;
        }

        /// <summary>
        /// Portées de terrain (CITY_PLACEMENT_TERRAIN_RANGE, Sirènes) : un nouveau vertex de ville
        /// en surface est valide s'il est à au plus la portée indiquée (en arêtes) d'un vertex
        /// touchant directement le terrain. Cumulable en OR avec GetRequiredCityPlacementTerrains
        /// (adjacence stricte).
        /// </summary>
        private static List<(TerrainType Terrain, int Range)> GetRequiredCityPlacementTerrainRanges(Civilization civ)
        {
            var result = new List<(TerrainType, int)>();
            foreach (var modifier in civ.ModifierAggregator.GetActiveModifiers(ECategory.CITY_PLACEMENT_TERRAIN_RANGE))
                if (Enum.TryParse<TerrainType>(modifier.SubCategory, out var terrain))
                    result.Add((terrain, (int)modifier.Value));
            return result;
        }

        /// <summary>
        /// BFS par arêtes (ordre stable, comme AddFlightCandidateVertices) : ensemble de tous les
        /// vertex de la carte à au plus <paramref name="range"/> arêtes d'un vertex touchant
        /// directement <paramref name="terrain"/> (portée 0 = adjacence stricte incluse). Mis en
        /// cache par (terrain, range), invalidé sur TerrainVersion (la Marche de Dieu peut changer
        /// le terrain sans toucher aux compteurs de routes/villes).
        /// </summary>
        private HashSet<Vertex> GetVerticesWithinRangeOfTerrain(IslandMap map, TerrainType terrain, int range)
        {
            var key = (terrain, range);
            if (_terrainRangeVerticesCache.TryGetValue(key, out var cached) && cached.TerrainVersion == _state!.TerrainVersion)
                return cached.Vertices;

            var visited = new HashSet<Vertex>();
            var queue = new Queue<(Vertex Vertex, int Depth)>();

            foreach (var (coord, tile) in map.Tiles)
            {
                if (tile.TerrainType != terrain) continue;
                foreach (var dir in SecondaryHexDirectionUtils.AllSecondaryDirections)
                {
                    var seed = coord.Vertex(dir);
                    if (visited.Add(seed))
                        queue.Enqueue((seed, 0));
                }
            }

            while (queue.Count > 0)
            {
                var (vertex, depth) = queue.Dequeue();
                if (depth >= range) continue;
                foreach (var neighbor in vertex.GetAdjacentVertices())
                    if (visited.Add(neighbor))
                        queue.Enqueue((neighbor, depth + 1));
            }

            _terrainRangeVerticesCache[key] = (_state!.TerrainVersion, visited);
            return visited;
        }

        /// <summary>
        /// Vertices a city could relocate to: constructible as if the city weren't there, within
        /// <paramref name="maxEdgeDistance"/> edges of its current position, excluding that position itself.
        /// </summary>
        public List<Vertex> GetRelocationTargets(City city, int maxEdgeDistance = 3)
        {
            var origin = city.Position;
            return GetBuildableVertices(city.CivilizationIndex, excludingCity: city)
                .Where(v => v.Z == origin.Z && !v.Equals(origin) && origin.EdgeDistanceTo(v) <= maxEdgeDistance)
                .ToList();
        }

        public static ResourceSet RelocationCost() => new()
        {
            { Resource.Gold, 100 },
            { Resource.Food, 100 },
        };

        /// <summary>
        /// Moves a city to a new vertex, paying <see cref="RelocationCost"/>. Returns false if the destination
        /// is not a valid relocation target or the civilization cannot afford the cost — nothing is charged in that case.
        /// </summary>
        public bool RelocateCity(City city, Vertex destination)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.GetCivilization(city.CivilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(city));

            if (!GetRelocationTargets(city).Any(v => v.Equals(destination)))
                return false;

            var cost = RelocationCost();
            if (!civ.CanPayResourceCost(cost))
                return false;

            civ.PayResourceCost(cost);
            city.Position = destination;
            // Position changed without any road/city count change — the count-keyed cache wouldn't
            // otherwise notice, so clear it explicitly.
            _buildableVerticesCache.Clear();
            _state.Visibility.RecalculateFor(city.CivilizationIndex);
            ClaimTreasureTrovesAt(city, civ);
            OnCityRelocated?.Invoke(this, new OutpostAutoBuiltEventArgs(city.CivilizationIndex, destination));
            return true;
        }

        public bool IsRelocationUnlocked(Civilization civ)
            => civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_RELOCATION);

        /// <summary>
        /// Build a city at the given vertex. Cost: 10 Brick, 10 Wood, 10 Wheat, 10 Sheep.
        /// Returns null if resources are insufficient. Throws if the vertex is not buildable (bug appelant).
        /// </summary>
        public City? BuildCity(int civilizationIndex, Vertex vertex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));

            var civ = _state.GetCivilization(civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            var cost = NewCityBuildingCostFor(vertex, civ);

            if (!civ.CanPayResourceCost(cost))
                return null;

            EnsureVertexBuildable(civilizationIndex, vertex);

            civ.PayResourceCost(cost);

            return CreateCityAt(civilizationIndex, vertex, civ);
        }

        /// <summary>
        /// Fonde une ville sur un vertex constructible sans en payer le coût (utilisé par les sorts magiques).
        /// Lance une exception si le vertex n'est pas constructible par cette civilisation.
        /// </summary>
        public City CreateCityFree(int civilizationIndex, Vertex vertex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));

            var civ = _state.GetCivilization(civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            EnsureVertexBuildable(civilizationIndex, vertex);

            return CreateCityAt(civilizationIndex, vertex, civ);
        }

        private void EnsureVertexBuildable(int civilizationIndex, Vertex vertex)
        {
            var buildable = GetBuildableVertices(civilizationIndex);
            if (!buildable.Any(v => v.Equals(vertex)))
                throw new InvalidOperationException("Vertex not buildable by this civilization");
        }

        private City CreateCityAt(int civilizationIndex, Vertex vertex, Civilization civ)
        {
            var vertexMap = _state!.GetMapFor(vertex)
                ?? throw new ArgumentException("Vertex belongs to an unknown layer.", nameof(vertex));

            var city = new City(vertex) { CivilizationIndex = civilizationIndex };
            civ.AddCity(city);

            if (civilizationIndex == _state.PlayerCivilization.Index)
                foreach (var bt in civ.ModifierAggregator.GetGrantedBuildingTypes(ECategory.NEW_CITY_BUILDING))
                    if (!city.Buildings.Any(b => b.Type == bt))
                    {
                        var b = BuildingController.CreateBuilding(bt);
                        if (b != null && !b.IsAvailableInLayer(vertexMap.Z))
                            continue;
                        if (b != null)
                        {
                            b.Level = 1;
                            city.Buildings.Add(b);
                            if (b.Type == BuildingType.TownHall) city.InvalidateLevelCache();
                            int defBonus = b.GetDefenseBonus();
                            if (defBonus > 0 && civ.ModifierAggregator.HasModifier(ECategory.BUILDING_DEFENSE_ON_CONSTRUCT))
                                city.CurrentDefense += defBonus;
                        }
                    }

            _state.Visibility.RecalculateFor(civilizationIndex);

            ClaimTreasureTrovesAt(city, civ);

            OnCityBuilt?.Invoke(this, new OutpostAutoBuiltEventArgs(civilizationIndex, vertex));
            return city;
        }

        /// <summary>
        /// Claims every TreasureTrove on a hex touched by the city's current position — called whenever
        /// a city ends up on a new vertex, whether founded there (<see cref="CreateCityAt"/>) or moved
        /// there (<see cref="RelocateCity"/>).
        /// </summary>
        private void ClaimTreasureTrovesAt(City city, Civilization civ)
        {
            var cityHexSet = new HashSet<HexCoord>(city.Position.GetHexes());
            var claimedTroves = cityHexSet.SelectMany(h => _state!.GetFeaturesAt(h))
                .OfType<TreasureTrove>()
                .ToList();
            foreach (var trove in claimedTroves)
            {
                _state!.EventLog.Add(trove.RemovedEventType);
                _state.RemoveFeature(trove);
                civ.AddResource(Resource.Gold, 100);
                _state.RunRecord.TreasuresTroveClaimed++;
            }
        }

        /// <summary>
        /// Single entry point for removing a city, whatever destroyed it (military conquest or a
        /// monster attack). Callers (CityAttackEngine, MonsterFeatureController) must call this instead
        /// of mutating <c>civ.Cities</c> themselves, so every downstream concern — road cleanup,
        /// contested-territory refresh, underworld checks, this controller's own vertex cache — reacts
        /// uniformly via <see cref="OnCityDestroyed"/> regardless of the cause.
        /// </summary>
        public void DestroyCity(City city, CityDestructionCause cause)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");
            if (city == null) throw new ArgumentNullException(nameof(city));

            var civ = _state.GetCivilization(city.CivilizationIndex)
                      ?? throw new ArgumentException("City's civilization not found", nameof(city));

            city.RaiseDestroyed();
            civ.RemoveCity(city);
            civ.TrimResourcesToMax();
            _buildableVerticesCache.Clear();
            _state.Visibility.Recalculate();

            OnCityDestroyed?.Invoke(this, new CityDestroyedEventArgs(city.Position, civ.Index, cause));
        }

        public ResourceSet NewCityBuildingCost()
        {
            return new ResourceSet
            {
                { Resource.Brick, 10 },
                { Resource.Wood, 10 },
                { Resource.Food, 15 },
            };
        }

        public ResourceSet NewCityBuildingCostFor(Vertex targetVertex, Civilization civ)
        {
            var cost = NewCityBuildingCost();
            double surchargeFactor = HasActiveBuildersGuild(civ) ? BuildersGuild.NewCitySurchargeMultiplier : 1.0;

            int surfaceCities = civ.Cities.Count(c => c.Position.Z == IslandMap.SurfaceLayer);
            int underworldCities = civ.Cities.Count(c => c.Position.Z == LayerState.UnderworldZ);
            int abyssCities = civ.Cities.Count(c => c.Position.Z == LayerState.AbyssZ);

            // La ville de départ (toujours en surface) ne compte pas comme "ville supplémentaire".
            int surchargeableSurfaceCities = Math.Max(0, surfaceCities - 1);

            // Les couches plus profondes (Inframonde, Abysse) plafonnent le nombre de villes des
            // couches au-dessus qu'elles prennent en compte, pour éviter une explosion du coût
            // quand la surface/l'Inframonde comptent énormément de villes.
            const int MaxCitiesFromShallowerLayers = 20;
            int cappedSurchargeableSurfaceCities = Math.Min(surchargeableSurfaceCities, MaxCitiesFromShallowerLayers);
            int cappedUnderworldCities = Math.Min(underworldCities, MaxCitiesFromShallowerLayers);

            double effectiveSurfaceOverCost = surchargeFactor * surchargeableSurfaceCities;
            double effectiveSurfaceOverCostCapped = surchargeFactor * cappedSurchargeableSurfaceCities;
            double effectiveUnderworldOverCost = Math.Pow(surchargeFactor * underworldCities, 1.5);
            double effectiveUnderworldOverCostCapped = Math.Pow(surchargeFactor * cappedUnderworldCities, 1.5);
            double effectiveAbyssOverCost = Math.Pow(surchargeFactor * abyssCities, 2);

            double multiplier;
            if (targetVertex.Z == LayerState.AbyssZ)
            {
                cost[Resource.Gold] = 10;
                cost[Resource.Crystal] = 5;
                multiplier = 1.0 + 1.0 * (effectiveSurfaceOverCostCapped + effectiveUnderworldOverCostCapped + effectiveAbyssOverCost);
            }
            else if (targetVertex.Z == LayerState.UnderworldZ)
            {
                cost[Resource.Gold] = 10;
                multiplier = 1.0 + 0.5 * (effectiveSurfaceOverCostCapped + effectiveUnderworldOverCost);
            }
            else
            {
                multiplier = 1.0 + 0.1 * (surchargeFactor * surchargeableSurfaceCities);
            }

            foreach (var resource in cost.Keys.ToList())
                cost[resource] = (int)Math.Round(cost[resource] * multiplier);

            // Grand Terrier (Gobelins) : réduction fractionnaire du coût final (voir NEW_CITY_COST_REDUCTION).
            double reduction = civ.ModifierAggregator.ApplyModifiers(ECategory.NEW_CITY_COST_REDUCTION, "", 0.0);
            if (reduction > 0)
                foreach (var resource in cost.Keys.ToList())
                    cost[resource] = Math.Max(1, (int)Math.Round(cost[resource] * (1.0 - reduction)));

            return cost;
        }

        private static bool HasActiveBuildersGuild(Civilization civ)
        {
            foreach (var city in civ.Cities)
                if (city.Buildings.OfType<BuildersGuild>().Any(b => b.Level > 0))
                    return true;
            return false;
        }

        public int MinDistanceBetweenCities => 2;
        public int MinDistanceBetweenCivilizationCities => 3;

        /// <summary>
        /// Distance minimale effective entre deux villes de cette civilisation : la base (3),
        /// éventuellement remplacée par la race jouée (CITY_MIN_DISTANCE — Gobelins 2, Géants 4).
        /// </summary>
        public int GetMinDistanceBetweenCivilizationCities(Civilization civ)
            => civ.ModifierAggregator.ApplyModifiers(ECategory.CITY_MIN_DISTANCE, "", MinDistanceBetweenCivilizationCities);
    }
}
