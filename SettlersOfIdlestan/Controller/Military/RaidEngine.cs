using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Military;

/// <summary>
/// Gère l'action Raid : redirige tous les flux de la civilisation du joueur vers une cible — un
/// emplacement militaire ennemi (ville ou Flotte de Guerre — voir IMilitaryVertex) ou une
/// MonsterFeature. Les emplacements à portée d'attaque attaquent directement; les autres renforcent
/// l'allié le plus proche de la cible.
/// </summary>
internal class RaidEngine
{
    private WorldState? _state;
    private CityAttackEngine? _cityAttackEngine;
    private ReinforcementEngine? _reinforcementEngine;
    private MonsterCombatEngine? _monsterCombatEngine;

    private const long RaidCheckIntervalTicks = 100L;
    private long _lastRaidCheckTick = 0;

    internal void Initialize(WorldState? state, CityAttackEngine cityAttackEngine, ReinforcementEngine reinforcementEngine, MonsterCombatEngine monsterCombatEngine)
    {
        _state = state;
        _cityAttackEngine = cityAttackEngine;
        _reinforcementEngine = reinforcementEngine;
        _monsterCombatEngine = monsterCombatEngine;
    }

    internal bool IsRaidUnlocked(Civilization civ)
        => civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_RAID);

    internal bool IsWarHeraldUnlocked(Civilization civ)
        => civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_WAR_HERALD);

    internal bool IsRaidActive()
        => _state?.AutomationSettings.RaidTargetVertex != null || _state?.AutomationSettings.RaidTargetHex != null;

    internal Vertex? GetRaidTarget()
        => _state?.AutomationSettings.RaidTargetVertex;

    internal HexCoord? GetRaidTargetHex()
        => _state?.AutomationSettings.RaidTargetHex;

    internal List<Vertex> GetSelectableTargets(Civilization playerCiv)
    {
        if (_state == null) return new List<Vertex>();
        int currentLayer = _state.CurrentViewedLayer;
        var targets = new List<Vertex>();
        foreach (var civ in _state.Civilizations)
        {
            if (civ.Index == playerCiv.Index) continue;
            foreach (var vertex in civ.MilitaryVertices)
            {
                if (vertex.Position.Z == currentLayer && IsCityVisibleTo(vertex, playerCiv))
                    targets.Add(vertex.Position);
            }
        }
        return targets;
    }

    /// <summary>Villes de la civilisation elle-même, ciblables par War Herald.</summary>
    internal List<Vertex> GetSelectableAlliedTargets(Civilization civ)
        => civ.Cities.Select(c => c.Position).ToList();

    internal List<HexCoord> GetSelectableMonsterTargets()
    {
        if (_state == null) return new List<HexCoord>();
        int currentLayer = _state.CurrentViewedLayer;
        return _state.Features.OfType<MonsterFeature>()
            .Where(m => m.Found && m.Position.Z == currentLayer)
            .Select(m => m.Position)
            .ToList();
    }

    private bool IsCityVisibleTo(IMilitaryVertex vertex, Civilization civ)
    {
        var visibleMaps = _state!.Visibility.GetForZ(vertex.Position.Z);
        if (!visibleMaps.TryGetValue(civ.Index, out var visibleMap)) return true;
        return visibleMap.IsVertexVisible(vertex.Position);
    }

    private const int NearestCitiesCheckedForBarracks = 3;

    internal void StartRaid(Civilization civ, Vertex targetCityVertex)
    {
        if (_state == null) return;
        _state.AutomationSettings.RaidTargetHex = null;
        _state.AutomationSettings.RaidTargetVertex = targetCityVertex;
        _state.AutomationSettings.RaidCurrentUpkeep = 10;
        ApplyRaidFlows(civ, targetCityVertex);

        // Vendetta : un raid manuel du joueur sur une ville ennemie met à jour la civilisation ciblée
        // par les raids automatiques (voir ReinforcementEngine.ResolvePlayerAutoVendetta).
        if (civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_VENDETTA))
        {
            var targetCiv = _state.Civilizations.FirstOrDefault(c => c.MilitaryVertices.Any(v => v.Position.Equals(targetCityVertex)));
            if (targetCiv != null)
                _state.AutomationSettings.VendettaTargetCivIndex = targetCiv.Index;
        }

        var nearestCities = civ.Cities
            .Where(c => c.Position.Z == targetCityVertex.Z)
            .OrderBy(c => c.Position.EdgeDistanceTo(targetCityVertex));
        WarnIfMissingBarracksNearTarget(nearestCities);
    }

    internal void StartMonsterRaid(Civilization civ, HexCoord targetHex)
    {
        if (_state == null) return;
        _state.AutomationSettings.RaidTargetVertex = null;
        _state.AutomationSettings.RaidTargetHex = targetHex;
        _state.AutomationSettings.RaidCurrentUpkeep = 10;
        ApplyMonsterRaidFlows(civ, targetHex);

        var nearestCities = civ.Cities
            .Where(c => c.Position.Z == targetHex.Z)
            .OrderBy(c => c.Position.GetHexes().Max(h => h.DistanceTo(targetHex)));
        WarnIfMissingBarracksNearTarget(nearestCities);
    }

    /// <summary>
    /// War Herald : raid gratuit et instantané sur une ville alliée (de la civilisation elle-même).
    /// Redirige le flux de chaque emplacement militaire de la civilisation (sur le même layer que la
    /// cible) vers la ville ciblée, sauf ceux ayant un flux d'attaque actif (ville ennemie ou
    /// monstre) — contrairement au Raid classique, aucun upkeep et aucun suivi dans le temps.
    /// </summary>
    internal void StartWarHeraldRaid(Civilization civ, Vertex target)
    {
        if (_reinforcementEngine == null) return;
        int targetZ = target.Z;

        foreach (var vertex in civ.MilitaryVertices)
        {
            if (vertex.Position.Z != targetZ) continue;
            if (vertex.Position.Equals(target)) continue;

            bool hasActiveAttackFlow = vertex.MonsterAttackTarget != null
                || (vertex.FlowTarget != null && _reinforcementEngine.IsEnemyCityAt(vertex.FlowTarget, civ));
            if (hasActiveAttackFlow) continue;

            _reinforcementEngine.SetCityFlow(vertex, target);
        }
    }

    /// <summary>
    /// Avertit le joueur si une des villes les plus proches de la cible n'a pas de Barracks (vulnérable
    /// en cas de contre-attaque). Ne concerne que les villes — une Flotte de Guerre n'a jamais de
    /// bâtiment (voir WarFleet) donc n'est pas prise en compte par cet avertissement.
    /// </summary>
    private void WarnIfMissingBarracksNearTarget(IEnumerable<City> citiesOrderedByDistance)
    {
        if (_state == null) return;
        bool missingBarracks = citiesOrderedByDistance
            .Take(NearestCitiesCheckedForBarracks)
            .Any(c => !c.Buildings.Any(b => b.Type == BuildingType.Barracks));
        if (missingBarracks)
            _state.EventLog.Add(GameEventType.RaidMissingBarracks, toast: true);
    }

    internal void StopRaid(Civilization civ)
    {
        if (_state == null) return;
        var target = _state.AutomationSettings.RaidTargetVertex;
        var targetHex = _state.AutomationSettings.RaidTargetHex;
        _state.AutomationSettings.RaidTargetVertex = null;
        _state.AutomationSettings.RaidTargetHex = null;
        _state.AutomationSettings.RaidCurrentUpkeep = 0;

        if (target != null)
        {
            int z = target.Z;
            foreach (var vertex in civ.MilitaryVertices)
                if (vertex.Position.Z == z)
                    _reinforcementEngine!.SetCityFlow(vertex, null);
        }
        else if (targetHex != null)
        {
            int z = targetHex.Value.Z;
            foreach (var vertex in civ.MilitaryVertices)
            {
                if (vertex.Position.Z != z) continue;
                vertex.MonsterAttackTarget = null;
                _reinforcementEngine!.SetCityFlow(vertex, null);
            }
        }
    }

    internal void Update(long currentTick)
    {
        if (_state == null) return;
        if (currentTick - _lastRaidCheckTick < RaidCheckIntervalTicks) return;
        _lastRaidCheckTick = currentTick;

        var target = _state.AutomationSettings.RaidTargetVertex;
        var targetHex = _state.AutomationSettings.RaidTargetHex;
        if (target == null && targetHex == null) return;

        var playerCiv = _state.PlayerCivilization;

        if (target != null)
        {
            bool targetExists = _state.Civilizations.Any(c => c.Index != playerCiv.Index && c.MilitaryVertices.Any(v => v.Position.Equals(target)));
            if (!targetExists)
            {
                StopRaid(playerCiv);
                return;
            }

            bool hasAttackFlow = playerCiv.MilitaryVertices.Any(v => v.FlowTarget != null && v.FlowTarget.Equals(target));
            if (!hasAttackFlow)
            {
                StopRaid(playerCiv);
                return;
            }

            if (!PayUpkeep(playerCiv)) return;
            ApplyRaidFlows(playerCiv, target);
        }
        else
        {
            var monster = _state.Features.OfType<MonsterFeature>().FirstOrDefault(m => m.Position.Equals(targetHex));
            if (monster == null)
            {
                StopRaid(playerCiv);
                return;
            }

            bool hasAttackFlow = playerCiv.MilitaryVertices.Any(v => v.MonsterAttackTarget != null && v.MonsterAttackTarget.Equals(targetHex));
            if (!hasAttackFlow)
            {
                StopRaid(playerCiv);
                return;
            }

            if (!PayUpkeep(playerCiv)) return;
            ApplyMonsterRaidFlows(playerCiv, targetHex!.Value);
        }
    }

    /// <summary>Débite l'upkeep courant et l'augmente pour le prochain cycle. Retourne false (et arrête le raid) si les fonds sont insuffisants.</summary>
    private bool PayUpkeep(Civilization playerCiv)
    {
        int upkeep = _state!.AutomationSettings.RaidCurrentUpkeep;
        if (playerCiv.GetResourceQuantity(Resource.Gold) < upkeep)
        {
            StopRaid(playerCiv);
            return false;
        }
        playerCiv.RemoveResource(Resource.Gold, upkeep);
        _state.AutomationSettings.RaidCurrentUpkeep = upkeep + 2;
        return true;
    }

    private void ApplyRaidFlows(Civilization civ, Vertex target)
    {
        if (_cityAttackEngine == null || _reinforcementEngine == null) return;

        int attackRange = _cityAttackEngine.CityAttackRange(civ);
        int reinforcementRange = _reinforcementEngine.ReinforcementRange(civ);
        int targetZ = target.Z;
        var verticesInLayer = civ.MilitaryVertices.Where(v => v.Position.Z == targetZ).ToList();

        foreach (var vertex in verticesInLayer)
        {
            int distToTarget = vertex.Position.EdgeDistanceTo(target);
            if (distToTarget <= attackRange)
            {
                _reinforcementEngine.SetCityFlow(vertex, target);
            }
            else
            {
                // Renforce l'allié le plus proche de la cible qui est aussi à portée de renfort
                var nearestAlly = verticesInLayer
                    .Where(a => a != vertex
                             && a.Position.EdgeDistanceTo(target) < distToTarget
                             && vertex.Position.EdgeDistanceTo(a.Position) <= reinforcementRange)
                    .OrderBy(a => a.Position.EdgeDistanceTo(target))
                    .FirstOrDefault();

                _reinforcementEngine.SetCityFlow(vertex, nearestAlly?.Position);
            }
        }
    }

    private static int DistanceToMonster(IMilitaryVertex vertex, MonsterFeature monster)
        => vertex.Position.GetHexes().Max(h => h.DistanceTo(monster.Position));

    private void ApplyMonsterRaidFlows(Civilization civ, HexCoord targetHex)
    {
        if (_reinforcementEngine == null || _monsterCombatEngine == null || _state == null) return;

        var monster = _state.Features.OfType<MonsterFeature>().FirstOrDefault(m => m.Position.Equals(targetHex));
        if (monster == null) return;

        int reinforcementRange = _reinforcementEngine.ReinforcementRange(civ);
        int targetZ = targetHex.Z;
        var verticesInLayer = civ.MilitaryVertices.Where(v => v.Position.Z == targetZ).ToList();

        foreach (var vertex in verticesInLayer)
        {
            bool canAttack = _monsterCombatEngine.GetAttackAvailability(vertex, monster) == MonsterAttackAvailability.Available;
            if (canAttack)
            {
                vertex.MonsterAttackTarget = targetHex;
                _reinforcementEngine.SetCityFlow(vertex, null);
            }
            else
            {
                // Renforce l'allié le plus proche du monstre qui est aussi à portée de renfort
                int distToTarget = DistanceToMonster(vertex, monster);
                var nearestAlly = verticesInLayer
                    .Where(a => a != vertex
                             && DistanceToMonster(a, monster) < distToTarget
                             && vertex.Position.EdgeDistanceTo(a.Position) <= reinforcementRange)
                    .OrderBy(a => DistanceToMonster(a, monster))
                    .FirstOrDefault();

                vertex.MonsterAttackTarget = null;
                _reinforcementEngine.SetCityFlow(vertex, nearestAlly?.Position);
            }
        }
    }
}
