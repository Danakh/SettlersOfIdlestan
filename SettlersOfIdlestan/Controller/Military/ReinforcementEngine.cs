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
/// Gère les renforts entre villes alliées et les automatisations d'attaque/renfort du joueur.
/// Les soldats expédiés réservent immédiatement un slot dans la ville cible et suivent les routes
/// de la civilisation. Ils arrivent après un délai de ReinforcementTicksPerRoadSegment × nbSegments.
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

    /// <summary>Intervalle effectif entre deux expéditions depuis la même ville, après REINFORCEMENT_SPEED.</summary>
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
            foreach (var city in civ.Cities)
            {
                for (int i = city.IncomingSoldiers.Count - 1; i >= 0; i--)
                {
                    if (city.IncomingSoldiers[i].ArrivalTick > currentTick) continue;
                    city.IncomingSoldiers.RemoveAt(i);
                    int max = _productionEngine!.GetMaximumSoldierCapacity(city);
                    if (city.Soldiers < max)
                        city.Soldiers++;
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

            // Lookup O(1) ville par position — évite FirstOrDefault O(n) pour chaque ville source
            var cityByPos = new Dictionary<Vertex, City>(civ.Cities.Count);
            foreach (var c in civ.Cities) cityByPos[c.Position] = c;

            foreach (var sourceCity in civ.Cities)
            {
                if (currentTick - sourceCity.LastReinforcementTick < interval) continue;
                if (sourceCity.Soldiers == 0) continue;
                if (sourceCity.FlowTarget == null) continue;

                if (!cityByPos.TryGetValue(sourceCity.FlowTarget, out var targetCity) || targetCity == sourceCity) continue;

                var adj = GetAdjacency(civ, sourceCity.Position.Z);
                var roadPath = RoadPathfinder.FindPathInGraph(adj, sourceCity.Position, targetCity.Position);
                if (roadPath == null) continue;

                int roadSegments = roadPath.Count - 1;
                if (roadSegments > range) continue;

                // Le slot est réservé immédiatement : garnison + en-transit ne doit pas dépasser la capacité max
                int effectiveTarget = targetCity.Soldiers + targetCity.IncomingSoldiers.Count;
                if (effectiveTarget >= _productionEngine!.GetMaximumSoldierCapacity(targetCity)) continue;

                sourceCity.Soldiers--;
                sourceCity.LastReinforcementTick = currentTick;

                long arrivalTick = currentTick + roadSegments * MilitaryController.ReinforcementTicksPerRoadSegment;
                targetCity.IncomingSoldiers.Add(new InTransitSoldier(arrivalTick));

                onReinforcementSent(new ReinforcementEventArgs(sourceCity.Position, targetCity.Position, roadPath));
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

        foreach (var city in playerCiv.Cities)
        {
            if (city.FlowTarget != null && IsEnemyCityAt(city.FlowTarget, playerCiv)) continue;
            var enemy = _cityAttackEngine!.FindNearbyEnemyCity(city);
            if (enemy != null)
                SetCityFlow(city, enemy.Position);
        }
    }

    internal void UpdateCivilizationReinforcementFlows(Civilization civ)
    {
        // HashSet des positions ennemies — évite le double Any() pour chaque ville
        var enemyPositions = new HashSet<Vertex>();
        foreach (var otherCiv in _state!.Civilizations)
            if (otherCiv.Index != civ.Index)
                foreach (var ec in otherCiv.Cities)
                    enemyPositions.Add(ec.Position);

        int range = ReinforcementRange(civ);

        foreach (var city in civ.Cities)
        {
            if (city.FlowTarget != null && enemyPositions.Contains(city.FlowTarget)) continue;
            if (city.MonsterAttackTarget != null) continue;

            Vertex? newFlow = null;
            int capacity = city.MaxSoldiers;
            if (capacity > 0
                && city.Soldiers * 4 >= capacity
                && _cityAttackEngine!.FindNearbyEnemyCity(city) == null)
            {
                int z = city.Position.Z;
                var adj = GetAdjacency(civ, z);
                City? target = null;
                int fewestSoldiers = city.Soldiers;

                foreach (var friendly in civ.Cities)
                {
                    if (friendly == city) continue;
                    if (friendly.Position.Z != z) continue;

                    int tCap = friendly.MaxSoldiers;
                    int effectiveFriendly = friendly.Soldiers + friendly.IncomingSoldiers.Count;
                    if (tCap == 0 || effectiveFriendly * 2 > tCap) continue;
                    if (friendly.Soldiers + 2 >= city.Soldiers) continue;

                    if (friendly.Position.EdgeDistanceTo(city.Position) > range) continue;

                    var roadPath = RoadPathfinder.FindPathInGraph(adj, city.Position, friendly.Position);
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

            SetCityFlow(city, newFlow);
        }
    }

    internal bool IsEnemyCityAt(Vertex target, Civilization civ)
        => _state!.Civilizations.Any(c => c.Index != civ.Index && c.Cities.Any(cc => cc.Position.Equals(target)));

    internal void SetCityFlow(City city, Vertex? target)
    {
        if (target != null && _state != null)
        {
            var sourceCiv = _state.Civilizations.FirstOrDefault(c => c.Index == city.CivilizationIndex);
            var allyTarget = sourceCiv?.Cities.FirstOrDefault(c => c.Position.Equals(target));
            if (allyTarget != null && allyTarget.MaxSoldiers == 0)
                target = null;
        }
        city.FlowTarget = target;
    }

    internal void ClearReinforcementFlows(Civilization civ)
    {
        foreach (var city in civ.Cities)
            if (city.FlowTarget != null && !IsEnemyCityAt(city.FlowTarget, civ))
                SetCityFlow(city, null);
    }

    internal void ClearAttackFlows(Civilization civ)
    {
        foreach (var city in civ.Cities)
            if (city.FlowTarget != null && IsEnemyCityAt(city.FlowTarget, civ))
                SetCityFlow(city, null);
    }
}
