using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
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
    /// Vrai si l'Arbre-Cœur relie ces deux villes par la Forêt (UNLOCK_FOREST_REINFORCEMENT_LINK) :
    /// deux villes de la civilisation, sur le même plan, toutes deux adjacentes à une case Forêt, et
    /// reliées par une route — sans limite de longueur, contrairement à REINFORCEMENT_RANGE. Seule la
    /// portée est ignorée, pas le réseau routier : deux villes forestières sans route entre elles ne
    /// sont toujours pas éligibles.
    /// </summary>
    internal bool HasUnlimitedRangeReinforcementLink(Civilization civ, IMilitaryVertex source, IMilitaryVertex target)
    {
        if (source is not City || target is not City) return false;
        if (source.Position.Z != target.Position.Z) return false;
        if (!civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_FOREST_REINFORCEMENT_LINK)) return false;

        var map = _state?.GetMapForZ(source.Position.Z);
        if (map == null) return false;
        if (!map.VertexHasTerrainType(source.Position, TerrainType.Forest) ||
            !map.VertexHasTerrainType(target.Position, TerrainType.Forest))
            return false;

        var adj = GetAdjacency(civ, source.Position.Z);
        return RoadPathfinder.HasPathInGraph(adj, source.Position, target.Position);
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

    internal void ResolveReinforcements(long currentTick, Action<ReinforcementEventArgs> onReinforcementSent)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
        {
            long interval = EffectiveReinforcementInterval(civ);
            int range = ReinforcementRange(civ);

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

                var adj = GetAdjacency(civ, sourceVertex.Position.Z);
                var roadPath = RoadPathfinder.FindPathInGraph(adj, sourceVertex.Position, targetVertex.Position, range);
                if (roadPath == null && HasUnlimitedRangeReinforcementLink(civ, sourceVertex, targetVertex))
                    roadPath = RoadPathfinder.FindPathInGraph(adj, sourceVertex.Position, targetVertex.Position);
                if (roadPath == null) continue;

                int roadSegments = roadPath.Count - 1;

                // Le slot est réservé immédiatement : garnison + en-transit ne doit pas dépasser la capacité max
                int effectiveTarget = targetVertex.Soldiers + targetVertex.IncomingSoldiers.Count;
                if (effectiveTarget >= _productionEngine!.GetMaximumSoldierCapacity(targetVertex)) continue;

                sourceVertex.Soldiers--;
                sourceVertex.LastReinforcementTick = currentTick;

                long arrivalTick = currentTick + roadSegments * MilitaryController.ReinforcementTicksPerRoadSegment;
                targetVertex.IncomingSoldiers.Add(new InTransitSoldier(arrivalTick));

                onReinforcementSent(new ReinforcementEventArgs(sourceVertex.Position, targetVertex.Position, roadPath));
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

        foreach (var vertex in civ.MilitaryVertices)
        {
            if (vertex.FlowTarget != null && enemyPositions.Contains(vertex.FlowTarget)) continue;
            if (vertex.MonsterAttackTarget != null) continue;

            Vertex? newFlow = null;
            int capacity = vertex.MaxSoldiers;
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

                IMilitaryVertex? target = currentTarget != null && IsEligibleTarget(currentTarget, vertex, civ, z, range, reachable)
                    ? currentTarget : null;
                int fewestSoldiers = target?.Soldiers ?? vertex.Soldiers;

                var candidates = civ.MilitaryVertices;
                for (int i = 0; i < candidates.Count; i++)
                {
                    var friendly = candidates[i];
                    if (friendly == target) continue;
                    if (friendly.Soldiers > fewestSoldiers) continue;
                    if (!IsEligibleTarget(friendly, vertex, civ, z, range, reachable)) continue;

                    target = friendly;
                    fewestSoldiers = friendly.Soldiers;
                }

                if (target != null)
                    newFlow = target.Position;
            }

            // ownByPosition indexe exactement ce que SetCityFlow retrouvait par FirstOrDefault sur
            // civ.MilitaryVertices — un scan complet par emplacement, donc un produit cartésien.
            if (newFlow != null && ownByPosition.TryGetValue(newFlow, out var allyTarget) && allyTarget.MaxSoldiers == 0)
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
        IMilitaryVertex friendly, IMilitaryVertex source, Civilization civ, int z, int range, HashSet<Vertex> reachable)
    {
        if (friendly == source) return false;
        if (friendly.Position.Z != z) return false;

        int tCap = friendly.MaxSoldiers;
        int effectiveFriendly = friendly.Soldiers + friendly.IncomingSoldiers.Count;
        if (tCap == 0 || effectiveFriendly * 2 > tCap) return false;
        if (friendly.Soldiers + 2 >= source.Soldiers) return false;

        if (friendly.Position.EdgeDistanceTo(source.Position) <= range)
            return reachable.Contains(friendly.Position);

        // Hors de portée normale : encore éligible si l'Arbre-Cœur relie ces deux villes par la Forêt.
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
            if (allyTarget != null && allyTarget.MaxSoldiers == 0)
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
