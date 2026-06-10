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
/// </summary>
internal class ReinforcementEngine
{
    private WorldState? _state;
    private CityAttackEngine? _cityAttackEngine;
    private SoldierProductionEngine? _productionEngine;

    private long _lastPlayerAutoReinforcementTick = 0;
    private long _lastPlayerAutoAttackTick = 0;

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

    /// <summary>Intervalle effectif entre deux renforts, après application du modificateur REINFORCEMENT_SPEED.</summary>
    internal static long EffectiveReinforcementInterval(Civilization civ)
    {
        double speed = civ.ModifierAggregator.ApplyModifiers(ECategory.REINFORCEMENT_SPEED, "", 1.0);
        return Math.Max(1L, (long)(MilitaryController.ReinforcementIntervalTicks / speed));
    }

    internal void ResolveReinforcements(long currentTick, Action<ReinforcementEventArgs> onReinforcementSent)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
        {
            foreach (var sourceCity in civ.Cities.ToList())
            {
                if (currentTick - sourceCity.LastReinforcementTick < EffectiveReinforcementInterval(civ)) continue;
                if (sourceCity.Soldiers == 0) continue;
                if (sourceCity.FlowTarget == null) continue;

                var targetCity = civ.Cities.FirstOrDefault(c => c != sourceCity && c.Position.Equals(sourceCity.FlowTarget));
                if (targetCity == null) continue;
                if (sourceCity.Position.EdgeDistanceTo(targetCity.Position) > ReinforcementRange(civ)) continue;
                if (targetCity.Soldiers >= _productionEngine!.GetMaximumSoldierCapacity(targetCity)) continue;

                sourceCity.Soldiers--;
                targetCity.Soldiers++;
                sourceCity.LastReinforcementTick = currentTick;

                var path = HexGridPathfinder.FindVertexPath(sourceCity.Position, targetCity.Position);
                onReinforcementSent(new ReinforcementEventArgs(sourceCity.Position, targetCity.Position, path));
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
        foreach (var city in civ.Cities)
        {
            if (city.FlowTarget != null && IsEnemyCityAt(city.FlowTarget, civ)) continue;

            Vertex? newFlow = null;
            int capacity = city.MaxSoldiers;
            if (capacity > 0
                && city.Soldiers * 4 >= capacity
                && _cityAttackEngine!.FindNearbyEnemyCity(city) == null)
            {
                int range = ReinforcementRange(civ);
                City? target = null;
                int fewestSoldiers = city.Soldiers;

                foreach (var friendly in civ.Cities)
                {
                    if (friendly == city) continue;
                    if (friendly.Position.Z != city.Position.Z) continue;
                    int dist = city.Position.EdgeDistanceTo(friendly.Position);
                    if (dist > range) continue;

                    int tCap = friendly.MaxSoldiers;
                    if (tCap == 0 || friendly.Soldiers * 2 > tCap) continue;
                    if (friendly.Soldiers + 2 >= city.Soldiers) continue;

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
}
