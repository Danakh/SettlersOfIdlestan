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
    private CityAttackEngine? _cityAttackEngine;
    private SoldierProductionEngine? _productionEngine;

    private long _lastPlayerAutoReinforcementTick = 0;
    private long _lastPlayerAutoAttackTick = 0;

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
    private const long AutoAttackIntervalTicks = 100L;

    internal void Initialize(WorldState? state, CityAttackEngine cityAttackEngine, SoldierProductionEngine productionEngine)
    {
        _state = state;
        _cityAttackEngine = cityAttackEngine;
        _productionEngine = productionEngine;
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

            // Lookup O(1) par position — évite FirstOrDefault O(n) pour chaque source
            var vertexByPos = new Dictionary<Vertex, IMilitaryVertex>();
            foreach (var v in civ.MilitaryVertices) vertexByPos[v.Position] = v;

            foreach (var sourceVertex in civ.MilitaryVertices)
            {
                if (currentTick - sourceVertex.LastReinforcementTick < interval) continue;
                if (sourceVertex.Soldiers == 0) continue;
                if (sourceVertex.FlowTarget == null) continue;

                if (!vertexByPos.TryGetValue(sourceVertex.FlowTarget, out var targetVertex) || targetVertex == sourceVertex) continue;

                var adj = GetAdjacency(civ, sourceVertex.Position.Z);
                var roadPath = RoadPathfinder.FindPathInGraph(adj, sourceVertex.Position, targetVertex.Position);
                if (roadPath == null) continue;

                int roadSegments = roadPath.Count - 1;
                if (roadSegments > range) continue;

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
        if (!_state.AutomationSettings.MilitaryReinforcementAutomationEnabled) return;
        if (currentTick - _lastPlayerAutoReinforcementTick < AutoReinforcementIntervalTicks) return;
        _lastPlayerAutoReinforcementTick = currentTick;

        var playerCiv = _state.PlayerCivilization;
        if (!playerCiv.ModifierAggregator.HasModifier(ECategory.UNLOCK_AUTO_REINFORCEMENT)) return;

        UpdateCivilizationReinforcementFlows(playerCiv);
    }

    internal void ResolvePlayerAutoAttack(long currentTick)
    {
        if (_state == null) return;
        if (!_state.AutomationSettings.MilitaryAttackAutomationEnabled) return;
        if (currentTick - _lastPlayerAutoAttackTick < AutoAttackIntervalTicks) return;
        _lastPlayerAutoAttackTick = currentTick;

        var playerCiv = _state.PlayerCivilization;
        if (!playerCiv.ModifierAggregator.HasModifier(ECategory.UNLOCK_AUTO_ATTACK)) return;

        foreach (var vertex in playerCiv.MilitaryVertices)
        {
            if (vertex.FlowTarget != null && IsEnemyCityAt(vertex.FlowTarget, playerCiv)) continue;
            var enemy = _cityAttackEngine!.FindNearbyEnemyCity(vertex);
            if (enemy != null)
                SetCityFlow(vertex, enemy.Position);
        }
    }

    internal void UpdateCivilizationReinforcementFlows(Civilization civ)
    {
        // HashSet des positions ennemies — évite le double Any() pour chaque emplacement
        var enemyPositions = new HashSet<Vertex>();
        foreach (var otherCiv in _state!.Civilizations)
            if (otherCiv.Index != civ.Index)
                foreach (var ev in otherCiv.MilitaryVertices)
                    enemyPositions.Add(ev.Position);

        int range = ReinforcementRange(civ);

        foreach (var vertex in civ.MilitaryVertices)
        {
            if (vertex.FlowTarget != null && enemyPositions.Contains(vertex.FlowTarget)) continue;
            if (vertex.MonsterAttackTarget != null) continue;

            Vertex? newFlow = null;
            int capacity = vertex.MaxSoldiers;
            if (capacity > 0
                && vertex.Soldiers * 4 >= capacity
                && _cityAttackEngine!.FindNearbyEnemyCity(vertex) == null)
            {
                int z = vertex.Position.Z;
                var adj = GetAdjacency(civ, z);
                IMilitaryVertex? target = null;
                int fewestSoldiers = vertex.Soldiers;

                foreach (var friendly in civ.MilitaryVertices)
                {
                    if (friendly == vertex) continue;
                    if (friendly.Position.Z != z) continue;

                    int tCap = friendly.MaxSoldiers;
                    int effectiveFriendly = friendly.Soldiers + friendly.IncomingSoldiers.Count;
                    if (tCap == 0 || effectiveFriendly * 2 > tCap) continue;
                    if (friendly.Soldiers + 2 >= vertex.Soldiers) continue;

                    if (friendly.Position.EdgeDistanceTo(vertex.Position) > range) continue;

                    var roadPath = RoadPathfinder.FindPathInGraph(adj, vertex.Position, friendly.Position);
                    if (roadPath == null || roadPath.Count - 1 > range) continue;

                    if (friendly.Soldiers < fewestSoldiers)
                    {
                        target = friendly;
                        fewestSoldiers = friendly.Soldiers;
                    }
                }

                if (target != null)
                    newFlow = target.Position;
            }

            SetCityFlow(vertex, newFlow);
        }
    }

    internal bool IsEnemyCityAt(Vertex target, Civilization civ)
        => _state!.Civilizations.Any(c => c.Index != civ.Index && c.MilitaryVertices.Any(v => v.Position.Equals(target)));

    internal void SetCityFlow(IMilitaryVertex vertex, Vertex? target)
    {
        if (target != null && _state != null)
        {
            var sourceCiv = _state.Civilizations.FirstOrDefault(c => c.Index == vertex.CivilizationIndex);
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
