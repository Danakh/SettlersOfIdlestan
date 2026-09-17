using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Military;

/// <summary>
/// Gère les renforts entre emplacements militaires alliés (villes et Flottes de Guerre — voir
/// IMilitaryVertex) et les automatisations d'attaque/renfort du joueur.
/// Les soldats expédiés réservent immédiatement un slot dans la cible et suivent les routes de la
/// civilisation. Ils arrivent après un délai de ReinforcementTicksPerRoadSegment × nbSegments.
/// </summary>
internal class ReinforcementEngine
{
    private WorldState? _state;
    private SoldierProductionEngine? _productionEngine;

    private long _lastPlayerAutoReinforcementTick = 0;

    // Cache du graphe d'adjacence par (civIndex, z), invalidé dès que le nombre de routes change.
    private readonly Dictionary<(int civIndex, int z), (int roadCount, Dictionary<Vertex, List<Vertex>> adj)> _adjCache = new();

    private Dictionary<Vertex, List<Vertex>> GetAdjacency(Civilization civ, int z)
    {
        var key = (civ.Index, z);
        if (_adjCache.TryGetValue(key, out var cached) && cached.roadCount == civ.Roads.Count)
            return cached.adj;
        var adj = RoadPathfinder.BuildAdjacency(civ.Roads, z);
        _adjCache[key] = (civ.Roads.Count, adj);
        return adj;
    }

    private const int DefaultReinforcementRange = 5;
    private const long AutoReinforcementIntervalTicks = 100L;

    internal void Initialize(WorldState? state, SoldierProductionEngine productionEngine)
    {
        _state = state;
        _productionEngine = productionEngine;
    }

    /// <summary>
    /// Purge le graphe d'adjacence caché d'une civilisation retirée du monde — voir
    /// <see cref="WorldState.CivilizationRemoved"/>. Une entrée par layer, chacune retenant un
    /// dictionnaire de vertex.
    /// </summary>
    internal void PurgeCivilizationCaches(int civilizationIndex)
    {
        foreach (var key in _adjCache.Keys.Where(k => k.civIndex == civilizationIndex).ToList())
            _adjCache.Remove(key);
    }

    internal int ReinforcementRange(Civilization civ)
        => civ.ModifierAggregator.ApplyModifiers(ECategory.REINFORCEMENT_RANGE, "", DefaultReinforcementRange);

    /// <summary>Intervalle effectif entre deux expéditions depuis le même emplacement, après REINFORCEMENT_SPEED.</summary>
    internal static long EffectiveReinforcementInterval(Civilization civ)
    {
        double speed = civ.ModifierAggregator.ApplyModifiers(ECategory.REINFORCEMENT_SPEED, "", 1.0);
        return Math.Max(1L, (long)(MilitaryController.ReinforcementIntervalTicks / speed));
    }

    /// <summary>
    /// Vrai si la civilisation expédie ses renforts sans route (UNLOCK_ROADLESS_REINFORCEMENT,
    /// Trône des Vents) : à défaut de chemin routier, les soldats volent en ligne droite jusqu'à la
    /// cible. La portée de renfort (REINFORCEMENT_RANGE) continue de s'appliquer, seul le réseau
    /// routier cesse d'être exigé.
    /// </summary>
    internal static bool HasRoadlessReinforcement(Civilization civ)
        => civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_ROADLESS_REINFORCEMENT);

    /// <summary>
    /// Vrai si l'Arbre-Cœur relie ces deux villes par la Forêt (UNLOCK_FOREST_REINFORCEMENT_LINK) :
    /// deux villes de la civilisation, sur le même plan, toutes deux adjacentes à une case Forêt.
    /// La Forêt <b>est</b> le chemin : ni la portée (REINFORCEMENT_RANGE) ni le réseau routier
    /// n'entrent en compte, deux villes forestières sans aucune route entre elles sont éligibles.
    /// Un renfort emprunté sur ce lien est en plus instantané (voir ResolveReinforcements) — pas de
    /// soldat en transit ni d'animation sur la carte.
    /// </summary>
    internal bool HasUnlimitedRangeReinforcementLink(Civilization civ, IMilitaryVertex source, IMilitaryVertex target)
    {
        if (source is not City || target is not City) return false;
        if (source.Position.Z != target.Position.Z) return false;
        if (!civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_FOREST_REINFORCEMENT_LINK)) return false;

        var map = _state?.GetMapForZ(source.Position.Z);
        if (map == null) return false;
        return map.VertexHasTerrainType(source.Position, TerrainType.Forest)
            && map.VertexHasTerrainType(target.Position, TerrainType.Forest);
    }

    /// <summary>
    /// Convertit les soldats dont le tick d'arrivée est atteint de IncomingSoldiers vers la garnison.
    /// </summary>
    internal void ResolveArrivals(long currentTick)
    {
        if (_state == null) return;
        foreach (var civ in _state.Civilizations)
        {
            foreach (var vertex in civ.MilitaryVertices)
            {
                for (int i = vertex.IncomingSoldiers.Count - 1; i >= 0; i--)
                {
                    if (vertex.IncomingSoldiers[i].ArrivalTick > currentTick) continue;
                    vertex.IncomingSoldiers.RemoveAt(i);
                    int max = _productionEngine!.GetMaximumSoldierCapacity(vertex);
                    if (vertex.Soldiers < max)
                        vertex.Soldiers++;
                }
            }
        }
    }

    /// <summary>
    /// Expédition des renforts de toutes les civilisations pour cet événement d'horloge.
    ///
    /// <para><paramref name="elapsedTicks"/> est la durée couverte par l'événement. En jeu normal
    /// (quelques ticks par image) elle est très inférieure à l'intervalle d'expédition et cette
    /// méthode se comporte exactement comme avant : au plus un soldat par emplacement source.
    /// Pendant un saut de temps (<c>TimeJumpService</c>, tranches de 10 000 ticks), un seul
    /// événement couvre des dizaines d'intervalles : on expédie alors tous les soldats dus sur la
    /// tranche d'un coup, et ceux dont le trajet est déjà terminé à <paramref name="currentTick"/>
    /// arrivent dans la foulée au lieu d'attendre l'événement suivant — voir
    /// <see cref="SettleBurstBuffer"/> pour la suite du raisonnement.</para>
    /// </summary>
    internal void ResolveReinforcements(long currentTick, long elapsedTicks, bool monsterBurstFollows,
        Action<ReinforcementEventArgs> onReinforcementSent)
    {
        if (_state == null) return;

        // Un seul test en jeu normal : tout le rattrapage ci-dessous (et le tampon de rafale) est
        // inerte tant qu'un événement ne couvre pas plus d'un intervalle d'expédition.
        bool catchUp = elapsedTicks > MilitaryController.ReinforcementIntervalTicks;
        bool burstBufferAllowed = catchUp && monsterBurstFollows;
        _threatenedHexesBuilt = false;
        if (_burstGrants.Count > 0) _burstGrants.Clear();

        foreach (var civ in _state.Civilizations)
        {
            long interval = EffectiveReinforcementInterval(civ);
            long windowCycles = Math.Max(1L, elapsedTicks / interval);
            int range = ReinforcementRange(civ);
            bool roadless = HasRoadlessReinforcement(civ);

            // Lookup O(1) par position — évite FirstOrDefault O(n) pour chaque source. L'index est
            // construit paresseusement, à la première source réellement prête à expédier : à 2 ticks
            // par événement (une frame de jeu) le cooldown de 100 ticks n'est presque jamais échu, et
            // le construire d'office par civilisation et par événement en faisait le premier poste
            // d'allocation de toute la simulation. Le dictionnaire lui-même est réutilisé.
            var vertexByPos = _vertexByPositionScratch;
            bool indexBuilt = false;

            var vertices = civ.MilitaryVertices;
            for (int i = 0; i < vertices.Count; i++)
            {
                var sourceVertex = vertices[i];
                if (currentTick - sourceVertex.LastReinforcementTick < interval) continue;
                if (sourceVertex.Soldiers == 0) continue;
                if (sourceVertex.FlowTarget == null) continue;

                if (!indexBuilt)
                {
                    vertexByPos.Clear();
                    for (int v = 0; v < vertices.Count; v++) vertexByPos[vertices[v].Position] = vertices[v];
                    indexBuilt = true;
                }

                if (!vertexByPos.TryGetValue(sourceVertex.FlowTarget, out var targetVertex) || targetVertex == sourceVertex) continue;

                // Nombre d'expéditions dues depuis la dernière. Hors rattrapage, c'est 1 et
                // `dispatchBase` vaut `currentTick - interval`, ce qui restitue exactement les
                // anciennes formules (arrivée à currentTick + trajet, LastReinforcementTick = currentTick).
                //
                // Plafonné au débit réel de la tranche (`windowCycles`) et non au seul écart depuis
                // LastReinforcementTick : un emplacement dont le tracker traîne loin derrière (flux
                // défini à l'instant, sortie d'une longue période sans cible, tracker jamais amorcé
                // et resté à 0) viderait sinon toute sa garnison d'un coup sur le premier événement
                // de rattrapage venu. C'est la tranche qu'on rattrape, pas l'historique.
                long cycles = catchUp
                    ? Math.Min((currentTick - sourceVertex.LastReinforcementTick) / interval, windowCycles)
                    : 1L;
                long dispatchBase = catchUp ? sourceVertex.LastReinforcementTick : currentTick - interval;

                // Lien forestier de l'Arbre-Cœur : renfort magique instantané, sans soldat en transit
                // ni animation sur la carte — ni portée ni route requises, la Forêt est le chemin.
                bool forestLinked = HasUnlimitedRangeReinforcementLink(civ, sourceVertex, targetVertex);
                if (forestLinked)
                {
                    long forestSent = PlannedShipmentCount(sourceVertex, targetVertex, cycles, burstBufferAllowed,
                        out int forestCapacity);
                    if (forestSent <= 0) continue;

                    sourceVertex.Soldiers -= (int)forestSent;
                    sourceVertex.LastReinforcementTick = dispatchBase + forestSent * interval;
                    RegisterArrivedSoldiers(sourceVertex, targetVertex, (int)forestSent, forestCapacity);
                    continue;
                }

                var adj = GetAdjacency(civ, sourceVertex.Position.Z);
                var roadPath = RoadPathfinder.FindPathInGraph(adj, sourceVertex.Position, targetVertex.Position, range);

                int roadSegments;
                if (roadPath != null)
                {
                    roadSegments = roadPath.Count - 1;
                }
                else
                {
                    // Renfort aérien du Trône des Vents : aucune route ne relie les deux emplacements,
                    // les soldats volent donc en ligne droite. La portée reste celle du renfort normal
                    // et le vol dure autant de segments qu'il y a d'arêtes à franchir — soit au plus
                    // ce qu'aurait coûté une route, jamais davantage.
                    if (!roadless) continue;
                    if (sourceVertex.Position.Z != targetVertex.Position.Z) continue;

                    int edgeDistance = sourceVertex.Position.EdgeDistanceTo(targetVertex.Position);
                    if (edgeDistance > range) continue;

                    roadPath = new List<Vertex> { sourceVertex.Position, targetVertex.Position };
                    roadSegments = edgeDistance;
                }

                // Le slot est réservé immédiatement : garnison + en-transit ne doit pas dépasser la capacité max
                long sent = PlannedShipmentCount(sourceVertex, targetVertex, cycles, burstBufferAllowed, out int capacity);
                if (sent <= 0) continue;

                long transitTicks = roadSegments * MilitaryController.ReinforcementTicksPerRoadSegment;
                int landed = 0;
                int queued = 0;
                for (long k = 1; k <= sent; k++)
                {
                    long arrivalTick = dispatchBase + k * interval + transitTicks;

                    // Trajet déjà terminé quelque part dans la tranche : le soldat entre en garnison
                    // maintenant. Le mettre en transit le retarderait jusqu'à l'événement suivant,
                    // soit après la rafale d'attaques qu'il est justement censé encaisser.
                    if (arrivalTick <= currentTick) { landed++; continue; }

                    // Encore en route à la fin de la tranche : soumis au plafond normal, comme une
                    // expédition ordinaire. Un slot réservé au-delà du plafond serait purement perdu
                    // à l'arrivée (voir ResolveArrivals), alors que le tampon de rafale ne vaut que
                    // pour les soldats réellement présents pendant la rafale. Les échéances étant
                    // croissantes, la première qui ne passe pas arrête la série.
                    if (targetVertex.Soldiers + landed + targetVertex.IncomingSoldiers.Count >= capacity) break;
                    targetVertex.IncomingSoldiers.Add(new InTransitSoldier(arrivalTick));
                    queued++;
                }

                int shipped = landed + queued;
                if (shipped <= 0) continue;

                sourceVertex.Soldiers -= shipped;
                sourceVertex.LastReinforcementTick = dispatchBase + shipped * interval;
                RegisterArrivedSoldiers(sourceVertex, targetVertex, landed, capacity);

                // Un seul événement par expédition groupée : l'animation ne connaît qu'un chemin, et
                // en rejouer cent par tranche noierait le rendu.
                onReinforcementSent(new ReinforcementEventArgs(sourceVertex.Position, targetVertex.Position, roadPath));
            }
        }
    }

    // ── Rattrapage de saut de temps : tampon de rafale ───────────────────────

    /// <summary>
    /// Soldats que <paramref name="sourceVertex"/> peut expédier vers <paramref name="targetVertex"/>
    /// pour les <paramref name="cycles"/> expéditions dues, et capacité de garnison de la cible
    /// (<paramref name="capacity"/>), relue une seule fois pour l'appelant.
    ///
    /// <para>Hors rattrapage, <paramref name="cycles"/> vaut 1 et la règle est inchangée : rien ne
    /// part si la cible est pleine, soldats en transit compris. Sous rafale
    /// (<paramref name="burstBufferAllowed"/>) et seulement vers une cible réellement menacée, le
    /// plafond de garnison cesse de brider l'expédition — voir <see cref="SettleBurstBuffer"/>.</para>
    /// </summary>
    private long PlannedShipmentCount(IMilitaryVertex sourceVertex, IMilitaryVertex targetVertex,
        long cycles, bool burstBufferAllowed, out int capacity)
    {
        capacity = _productionEngine!.GetMaximumSoldierCapacity(targetVertex);
        int effectiveTarget = targetVertex.Soldiers + targetVertex.IncomingSoldiers.Count;
        long normalHeadroom = Math.Max(0, capacity - effectiveTarget);

        long allowance = normalHeadroom;
        if (burstBufferAllowed && cycles > normalHeadroom && IsUnderMonsterThreat(targetVertex))
            allowance = cycles;

        return Math.Min(cycles, Math.Min(sourceVertex.Soldiers, allowance));
    }

    /// <summary>
    /// Fait entrer <paramref name="count"/> soldats en garnison et enregistre, le cas échéant, la
    /// part que cette entrée pousse au-dessus du plafond — la seule que
    /// <see cref="SettleBurstBuffer"/> aura à rendre. Mesurée comme la variation du dépassement, et
    /// non comme le dépassement total : plusieurs sources peuvent alimenter la même cible dans le
    /// même événement, et chacune ne doit se voir rendre que ce qu'elle a avancé.
    /// </summary>
    private void RegisterArrivedSoldiers(IMilitaryVertex sourceVertex, IMilitaryVertex targetVertex,
        int count, int capacity)
    {
        if (count <= 0) return;

        int overCapacityBefore = Math.Max(0, targetVertex.Soldiers - capacity);
        targetVertex.Soldiers += count;
        int granted = Math.Max(0, targetVertex.Soldiers - capacity) - overCapacityBefore;

        if (granted > 0) _burstGrants.Add((sourceVertex, targetVertex, granted));
    }

    /// <summary>
    /// Rend aux emplacements sources les soldats du tampon de rafale que les attaques de monstres
    /// n'ont pas consommés, et replafonne la garnison des cibles. À appeler une fois que
    /// <see cref="MonsterFeatureController"/> a fini de rejouer toutes les attaques dues sur la
    /// tranche — au même endroit que <see cref="MilitaryController.ClampDefenseAfterCombat"/>.
    ///
    /// <para>Pourquoi un tampon. Pendant un saut de temps, un seul événement <c>Advanced</c> couvre
    /// 10 000 ticks : <see cref="MonsterFeatureController"/> y rejoue TOUTES les attaques dues sur
    /// la tranche (voir <c>MaxMonsterCatchUpSteps</c>), alors que la ville assiégée ne peut leur
    /// opposer que son <b>stock</b> de garnison. En jeu continu, c'est un <b>flux</b> bien plus
    /// grand que ce stock qui traverse la ville sur la même période : les renforts arrivent entre
    /// deux coups et le plafond de garnison n'est jamais la limite. Le tampon restitue ce flux —
    /// une cible réellement menacée peut dépasser son plafond le temps de la rafale, puis on
    /// replafonne. Même raisonnement que <see cref="MilitaryController.ClampDefenseAfterCombat"/>
    /// pour la régénération de défense.</para>
    ///
    /// <para>Les soldats sont conservés, jamais créés ni détruits : ce qui reste au-dessus du
    /// plafond après la rafale retourne à la ville source qui l'avait avancé, en ordre inverse
    /// d'expédition. Sans agresseur, aucun tampon n'est accordé (voir
    /// <see cref="IsUnderMonsterThreat"/>) et cette méthode n'a rien à faire.</para>
    /// </summary>
    internal void SettleBurstBuffer()
    {
        if (_burstGrants.Count == 0) return;

        for (int i = _burstGrants.Count - 1; i >= 0; i--)
        {
            var (source, target, granted) = _burstGrants[i];
            int surplus = target.Soldiers - _productionEngine!.GetMaximumSoldierCapacity(target);
            if (surplus <= 0) continue;

            int refunded = Math.Min(surplus, granted);
            target.Soldiers -= refunded;
            source.Soldiers += refunded;
        }

        _burstGrants.Clear();
    }

    /// <summary>Tampons accordés pendant cet événement : (source ayant avancé les soldats, cible, quantité au-dessus du plafond).</summary>
    private readonly List<(IMilitaryVertex source, IMilitaryVertex target, int granted)> _burstGrants = new();

    /// <summary>
    /// Hexs couverts par la portée d'attaque d'au moins un monstre hostile, construits une seule
    /// fois par événement et seulement si une expédition de rattrapage le demande vraiment (voir
    /// <see cref="_threatenedHexesBuilt"/>) : une partie sans monstre à portée d'une cible de flux
    /// ne paie rien du tout.
    /// </summary>
    private readonly HashSet<HexCoord> _threatenedHexesScratch = new();
    private bool _threatenedHexesBuilt;

    /// <summary>Vrai si un monstre hostile peut frapper cet emplacement — seule condition d'octroi d'un tampon de rafale.</summary>
    private bool IsUnderMonsterThreat(IMilitaryVertex vertex)
    {
        if (!_threatenedHexesBuilt)
        {
            BuildThreatenedHexes();
            _threatenedHexesBuilt = true;
        }
        if (_threatenedHexesScratch.Count == 0) return false;

        var position = vertex.Position;
        return _threatenedHexesScratch.Contains(position.Hex1)
            || _threatenedHexesScratch.Contains(position.Hex2)
            || _threatenedHexesScratch.Contains(position.Hex3);
    }

    private void BuildThreatenedHexes()
    {
        _threatenedHexesScratch.Clear();

        // Index par type du WorldState : pas de balayage des features ni d'allocation d'itérateur.
        var monsters = _state!.GetFeaturesOfType<MonsterFeature>();
        for (int i = 0; i < monsters.Count; i++)
        {
            var monster = (MonsterFeature)monsters[i];
            if (monster.Hp <= 0) continue;
            // Aventurier, Titan d'Acier… : combattent les autres monstres, jamais les villes.
            if (monster.AttacksOtherMonsters) continue;

            // Même convention que MonsterFeatureController.FindAttackTarget : portée 1 = le seul hex
            // du monstre, portée 2 = ses voisins, etc.
            int radius = monster.MaxAttackRangeInHexes - 1;
            if (radius < 0) continue;

            var center = monster.Position;
            for (int dq = -radius; dq <= radius; dq++)
            {
                int from = Math.Max(-radius, -dq - radius);
                int to = Math.Min(radius, -dq + radius);
                for (int dr = from; dr <= to; dr++)
                    _threatenedHexesScratch.Add(new HexCoord(center.Q + dq, center.R + dr, center.Z));
            }
        }
    }

    internal void ResolvePlayerAutoReinforcement(long currentTick)
    {
        if (_state == null) return;
        if (!_state.AutomationSettings.IsMilitaryReinforcementAutomationActive) return;
        if (currentTick - _lastPlayerAutoReinforcementTick < AutoReinforcementIntervalTicks) return;
        _lastPlayerAutoReinforcementTick = currentTick;

        var playerCiv = _state.PlayerCivilization;
        if (!playerCiv.ModifierAggregator.HasModifier(ECategory.UNLOCK_AUTO_REINFORCEMENT)) return;

        UpdateCivilizationReinforcementFlows(playerCiv);
    }

    // Tampons réutilisés par UpdateCivilizationReinforcementFlows : appelée toutes les 100 ticks pour
    // le joueur et à chaque tour d'IA pour les PNJ sans cible prioritaire, elle reconstruisait sinon
    // ces collections — jusqu'à plusieurs centaines d'entrées en fin de partie — à chaque appel.
    private readonly HashSet<Vertex> _enemyPositionsScratch = new();
    private readonly Dictionary<Vertex, IMilitaryVertex> _ownVertexByPositionScratch = new();

    /// <summary>Index position → emplacement de <see cref="ResolveReinforcements"/> — distinct de
    /// <see cref="_ownVertexByPositionScratch"/>, les deux méthodes pouvant être actives sur le même tick.</summary>
    private readonly Dictionary<Vertex, IMilitaryVertex> _vertexByPositionScratch = new();
    private readonly HashSet<Vertex> _reachableScratch = new();
    private readonly Queue<Vertex> _reachableQueueScratch = new();

    /// <summary>
    /// Réassigne les flux de renfort d'une civilisation. Toutes les capacités comparées ici sont les
    /// capacités <b>effectives</b> (<see cref="SoldierProductionEngine.GetMaximumSoldierCapacity"/> :
    /// bâtiments + <see cref="ECategory.CITY_MAX_SOLDIERS_BONUS"/> de la civilisation), les mêmes que
    /// celles que <see cref="ResolveReinforcements"/> et <see cref="SoldierProductionEngine"/>
    /// appliquent ensuite. Sur la capacité brute des seuls bâtiments, une ville à Caserne niveau 1
    /// (5 places) était déclarée « plus qu'à moitié pleine » dès 3 soldats alors que les bonus
    /// civ-wide lui en donnent une trentaine : elle sortait des cibles éligibles quasi immédiatement
    /// et restait éternellement sous-garnie, pendant que les grosses villes débordaient.
    /// </summary>
    internal void UpdateCivilizationReinforcementFlows(Civilization civ)
    {
        // HashSet des positions ennemies — évite le double Any() pour chaque emplacement
        var enemyPositions = _enemyPositionsScratch;
        enemyPositions.Clear();
        foreach (var otherCiv in _state!.Civilizations)
            if (otherCiv.Index != civ.Index)
                foreach (var ev in otherCiv.MilitaryVertices)
                    enemyPositions.Add(ev.Position);

        // Index par position des emplacements de cette civilisation : la cible de flux courante était
        // retrouvée par un FirstOrDefault sur tous les emplacements, pour chaque emplacement — un
        // produit cartésien, plus une fermeture allouée à chaque fois.
        var ownByPosition = _ownVertexByPositionScratch;
        ownByPosition.Clear();
        // TryAdd et non l'indexeur : premier gagnant, comme le FirstOrDefault de SetCityFlow que cet
        // index remplace en fin de boucle.
        foreach (var v in civ.MilitaryVertices) ownByPosition.TryAdd(v.Position, v);

        int range = ReinforcementRange(civ);
        bool roadless = HasRoadlessReinforcement(civ);

        foreach (var vertex in civ.MilitaryVertices)
        {
            if (vertex.FlowTarget != null && enemyPositions.Contains(vertex.FlowTarget)) continue;
            if (vertex.MonsterAttackTarget != null) continue;

            Vertex? newFlow = null;
            int capacity = _productionEngine!.GetMaximumSoldierCapacity(vertex);
            if (capacity > 0 && vertex.Soldiers * 4 >= capacity)
            {
                int z = vertex.Position.Z;
                var adj = GetAdjacency(civ, z);

                // Un seul parcours depuis cet emplacement : toutes les cibles candidates partagent
                // la même origine et la même portée, inutile de relancer un pathfinding par candidat.
                var reachable = RoadPathfinder.ReachableWithin(
                    adj, vertex.Position, range, _reachableScratch, _reachableQueueScratch);

                // On ne quitte la cible actuelle que pour une cible strictement moins garnie —
                // évite qu'une ville change de cible de renfort tant que la sienne reste valide.
                IMilitaryVertex? currentTarget = vertex.FlowTarget != null
                    && ownByPosition.TryGetValue(vertex.FlowTarget, out var existing) ? existing : null;

                IMilitaryVertex? target = currentTarget != null && IsEligibleTarget(currentTarget, vertex, civ, z, range, reachable, roadless)
                    ? currentTarget : null;
                int fewestSoldiers = target?.Soldiers ?? vertex.Soldiers;

                var candidates = civ.MilitaryVertices;
                for (int i = 0; i < candidates.Count; i++)
                {
                    var friendly = candidates[i];
                    if (friendly == target) continue;
                    if (friendly.Soldiers > fewestSoldiers) continue;
                    if (!IsEligibleTarget(friendly, vertex, civ, z, range, reachable, roadless)) continue;

                    target = friendly;
                    fewestSoldiers = friendly.Soldiers;
                }

                if (target != null)
                    newFlow = target.Position;
            }

            // ownByPosition indexe exactement ce que SetCityFlow retrouvait par FirstOrDefault sur
            // civ.MilitaryVertices — un scan complet par emplacement, donc un produit cartésien.
            if (newFlow != null && ownByPosition.TryGetValue(newFlow, out var allyTarget)
                && _productionEngine!.GetMaximumSoldierCapacity(allyTarget) == 0)
                newFlow = null;
            vertex.FlowTarget = newFlow;
        }
    }

    /// <summary>
    /// Méthode plutôt que fonction locale : capturer <paramref name="source"/>, la portée et
    /// l'ensemble atteignable allouait une classe de fermeture à chaque emplacement traité, sur un
    /// chemin parcouru à chaque tour d'IA de chaque civilisation PNJ.
    /// </summary>
    private bool IsEligibleTarget(
        IMilitaryVertex friendly, IMilitaryVertex source, Civilization civ, int z, int range, HashSet<Vertex> reachable,
        bool roadless)
    {
        if (friendly == source) return false;
        if (friendly.Position.Z != z) return false;

        int tCap = _productionEngine!.GetMaximumSoldierCapacity(friendly);
        int effectiveFriendly = friendly.Soldiers + friendly.IncomingSoldiers.Count;
        if (tCap == 0 || effectiveFriendly * 2 > tCap) return false;
        if (friendly.Soldiers + 2 >= source.Soldiers) return false;

        // Avec le renfort aérien (roadless), la seule portée suffit : inutile d'exiger que la cible
        // soit atteignable par le réseau routier, les soldats la rejoignent en vol.
        if (friendly.Position.EdgeDistanceTo(source.Position) <= range
            && (roadless || reachable.Contains(friendly.Position)))
            return true;

        // Hors de portée normale, ou sans route jusqu'ici : encore éligible si l'Arbre-Cœur relie
        // ces deux villes par la Forêt.
        return HasUnlimitedRangeReinforcementLink(civ, source, friendly);
    }

    internal bool IsEnemyCityAt(Vertex target, Civilization civ)
        => _state!.Civilizations.Any(c => c.Index != civ.Index && c.MilitaryVertices.Any(v => v.Position.Equals(target)));

    internal void SetCityFlow(IMilitaryVertex vertex, Vertex? target)
    {
        if (target != null && _state != null)
        {
            var sourceCiv = _state.GetCivilization(vertex.CivilizationIndex);
            var allyTarget = sourceCiv?.MilitaryVertices.FirstOrDefault(v => v.Position.Equals(target));
            // Capacité effective : un emplacement n'est refusé comme cible que s'il ne peut accueillir
            // aucun soldat, bonus civ-wide compris — pas parce qu'il lui manque tel ou tel bâtiment.
            if (allyTarget != null && _productionEngine!.GetMaximumSoldierCapacity(allyTarget) == 0)
                target = null;
        }
        vertex.FlowTarget = target;
    }

    internal void ClearReinforcementFlows(Civilization civ)
    {
        foreach (var vertex in civ.MilitaryVertices)
            if (vertex.FlowTarget != null && !IsEnemyCityAt(vertex.FlowTarget, civ))
                SetCityFlow(vertex, null);
    }

    internal void ClearAttackFlows(Civilization civ)
    {
        foreach (var vertex in civ.MilitaryVertices)
            if (vertex.FlowTarget != null && IsEnemyCityAt(vertex.FlowTarget, civ))
                SetCityFlow(vertex, null);
    }
}
