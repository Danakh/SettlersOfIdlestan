using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Military;

/// <summary>
/// Gère l'action Raid : redirige tous les flux de la civilisation du joueur vers une ville ennemie cible.
/// Les villes à portée d'attaque attaquent directement; les autres renforcent l'alliée la plus proche de la cible.
/// </summary>
internal class RaidEngine
{
    private WorldState? _state;
    private CityAttackEngine? _cityAttackEngine;
    private ReinforcementEngine? _reinforcementEngine;

    private const long RaidCheckIntervalTicks = 100L;
    private long _lastRaidCheckTick = 0;

    internal void Initialize(WorldState? state, CityAttackEngine cityAttackEngine, ReinforcementEngine reinforcementEngine)
    {
        _state = state;
        _cityAttackEngine = cityAttackEngine;
        _reinforcementEngine = reinforcementEngine;
    }

    internal bool IsRaidUnlocked(Civilization civ)
        => civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_RAID);

    internal bool IsRaidActive()
        => _state?.AutomationSettings.RaidTargetVertex != null;

    internal Vertex? GetRaidTarget()
        => _state?.AutomationSettings.RaidTargetVertex;

    internal List<Vertex> GetSelectableTargets(Civilization playerCiv)
    {
        if (_state == null) return new List<Vertex>();
        int currentLayer = _state.CurrentViewedLayer;
        var targets = new List<Vertex>();
        foreach (var civ in _state.Civilizations)
        {
            if (civ.Index == playerCiv.Index) continue;
            foreach (var city in civ.Cities)
            {
                if (city.Position.Z == currentLayer && IsCityVisibleTo(city, playerCiv))
                    targets.Add(city.Position);
            }
        }
        return targets;
    }

    private bool IsCityVisibleTo(City city, Civilization civ)
    {
        var visibleMaps = _state!.Visibility.GetForZ(city.Position.Z);
        if (!visibleMaps.TryGetValue(civ.Index, out var visibleMap)) return true;
        return city.Position.GetHexes().Any(h => visibleMap.HasTile(h));
    }

    internal void StartRaid(Civilization civ, Vertex targetCityVertex)
    {
        if (_state == null) return;
        _state.AutomationSettings.RaidTargetVertex = targetCityVertex;
        _state.AutomationSettings.RaidCurrentUpkeep = 10;
        ApplyRaidFlows(civ, targetCityVertex);
    }

    internal void StopRaid(Civilization civ)
    {
        if (_state == null) return;
        var target = _state.AutomationSettings.RaidTargetVertex;
        _state.AutomationSettings.RaidTargetVertex = null;
        _state.AutomationSettings.RaidCurrentUpkeep = 0;
        if (target == null) return;

        int z = target.Z;
        foreach (var city in civ.Cities)
        {
            if (city.Position.Z == z)
                _reinforcementEngine!.SetCityFlow(city, null);
        }
    }

    internal void Update(long currentTick)
    {
        if (_state == null) return;
        if (currentTick - _lastRaidCheckTick < RaidCheckIntervalTicks) return;
        _lastRaidCheckTick = currentTick;

        var target = _state.AutomationSettings.RaidTargetVertex;
        if (target == null) return;

        var playerCiv = _state.PlayerCivilization;

        bool targetExists = _state.Civilizations.Any(c => c.Index != playerCiv.Index && c.Cities.Any(city => city.Position.Equals(target)));
        if (!targetExists)
        {
            StopRaid(playerCiv);
            return;
        }

        bool hasAttackFlow = playerCiv.Cities.Any(c => c.FlowTarget != null && c.FlowTarget.Equals(target));
        if (!hasAttackFlow)
        {
            StopRaid(playerCiv);
            return;
        }

        // Upkeep : coût en or croissant ; arrêt du raid si fonds insuffisants
        int upkeep = _state.AutomationSettings.RaidCurrentUpkeep;
        if (playerCiv.GetResourceQuantity(Resource.Gold) < upkeep)
        {
            StopRaid(playerCiv);
            return;
        }
        playerCiv.RemoveResource(Resource.Gold, upkeep);
        _state.AutomationSettings.RaidCurrentUpkeep = upkeep + 2;

        ApplyRaidFlows(playerCiv, target);
    }

    private void ApplyRaidFlows(Civilization civ, Vertex target)
    {
        if (_cityAttackEngine == null || _reinforcementEngine == null) return;

        int attackRange = _cityAttackEngine.CityAttackRange(civ);
        int reinforcementRange = _reinforcementEngine.ReinforcementRange(civ);
        int targetZ = target.Z;
        var citiesInLayer = civ.Cities.Where(c => c.Position.Z == targetZ).ToList();

        foreach (var city in citiesInLayer)
        {
            int distToTarget = city.Position.EdgeDistanceTo(target);
            if (distToTarget <= attackRange)
            {
                _reinforcementEngine.SetCityFlow(city, target);
            }
            else
            {
                // Renforce l'alliée la plus proche de la cible qui est aussi à portée de renfort
                var nearestAlly = citiesInLayer
                    .Where(a => a != city
                             && a.Position.EdgeDistanceTo(target) < distToTarget
                             && city.Position.EdgeDistanceTo(a.Position) <= reinforcementRange)
                    .OrderBy(a => a.Position.EdgeDistanceTo(target))
                    .FirstOrDefault();

                _reinforcementEngine.SetCityFlow(city, nearestAlly?.Position);
            }
        }
    }
}
