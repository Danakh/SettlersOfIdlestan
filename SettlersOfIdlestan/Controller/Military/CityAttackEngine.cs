using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Military;

/// <summary>
/// Gère les attaques militaires entre villes de civilisations adverses.
/// </summary>
internal class CityAttackEngine
{
    private WorldState? _state;
    private CityBuilderController? _cityBuilderController;
    private GamePRNG? _prng;

    private const int DefaultCityAttackRange = 3;

    internal void Initialize(WorldState? state, CityBuilderController? cityBuilderController, GamePRNG? prng = null)
    {
        _state = state;
        _cityBuilderController = cityBuilderController;
        _prng = prng;
    }

    internal int CityAttackRange(Civilization civ)
        => civ.ModifierAggregator.ApplyModifiers(ECategory.CITY_ATTACK_RANGE, "", DefaultCityAttackRange);

    internal void ResolveCityAttacks(long currentTick,
        Action<CityAttackEventArgs> onSoldierAttackedCity,
        Action<CityBuildingDestroyedEventArgs> onCityBuildingDestroyed)
    {
        var citiesToDestroy = new List<(Civilization civ, City city)>();

        foreach (var attackerCiv in _state!.Civilizations)
        {
            foreach (var attackerCity in attackerCiv.Cities.ToList())
            {
                var raidTarget = _state!.AutomationSettings.RaidTargetVertex;
                bool isRaidAttack = raidTarget != null && attackerCity.FlowTarget?.Equals(raidTarget) == true;
                long baseInterval = isRaidAttack
                    ? MilitaryController.CityAttackIntervalTicks / 2
                    : MilitaryController.CityAttackIntervalTicks;
                double speed = attackerCiv.ModifierAggregator.ApplyModifiers(ECategory.ATTACK_SPEED, "", 1.0);
                long effectiveInterval = Math.Max(1L, (long)(baseInterval / speed));
                if (currentTick - attackerCity.LastAttackTick < effectiveInterval) continue;
                if (attackerCity.Soldiers == 0) continue;
                if (attackerCity.FlowTarget == null) continue;

                var targetCity = FindEnemyCityAt(attackerCity.FlowTarget, attackerCiv);
                if (targetCity == null) continue;
                if (attackerCity.Position.EdgeDistanceTo(targetCity.Position) > CityAttackRange(attackerCiv)) continue;

                // Armes en Acier : consomme 1 ArmeAcier pour infliger 1 dégât supplémentaire
                bool hasSteelWeapon = attackerCiv.ModifierAggregator.HasModifier(ECategory.UNLOCK_STEEL_WEAPONS)
                    && attackerCiv.GetResourceQuantity(Resource.SteelWeapon) >= 1;
                if (hasSteelWeapon) attackerCiv.RemoveResource(Resource.SteelWeapon, 1);

                // Armures d'Acier : le soldat envoyé peut survivre en consommant 1 ArmureAcier
                if (SteelArmorEngine.TrySaveSoldiers(attackerCiv, attackerCity, 1, _prng!) == 0)
                    attackerCity.Soldiers--;
                attackerCity.LastAttackTick = currentTick;

                var path = HexGridPathfinder.FindVertexPath(attackerCity.Position, targetCity.Position);
                onSoldierAttackedCity(new CityAttackEventArgs(attackerCity.Position, targetCity.Position, path));

                bool destroyed = ApplyAttackToCity(targetCity, onCityBuildingDestroyed);
                if (!destroyed && hasSteelWeapon)
                    destroyed = ApplyAttackToCity(targetCity, onCityBuildingDestroyed);
                if (destroyed)
                {
                    var ownerCiv = _state.Civilizations.FirstOrDefault(c => c.Index == targetCity.CivilizationIndex);
                    if (ownerCiv != null)
                        citiesToDestroy.Add((ownerCiv, targetCity));
                }
            }
        }

        foreach (var (civ, city) in citiesToDestroy)
        {
            ClearFlowsTargeting(city.Position);
            _cityBuilderController?.DestroyCity(city, CityDestructionCause.Combat);
        }
    }

    internal City? FindEnemyCityAt(Vertex target, Civilization attackerCiv)
    {
        foreach (var civ in _state!.Civilizations)
        {
            if (civ.Index == attackerCiv.Index) continue;
            var city = civ.Cities.FirstOrDefault(c => c.Position.Equals(target));
            if (city != null) return city;
        }
        return null;
    }

    internal void ClearFlowsTargeting(Vertex position)
    {
        if (_state == null) return;
        foreach (var city in _state.Civilizations.SelectMany(c => c.Cities))
            if (city.FlowTarget != null && city.FlowTarget.Equals(position))
                city.FlowTarget = null;
    }

    internal City? FindNearbyEnemyCity(City attackerCity, IReadOnlyCollection<int>? targetCivIndices = null)
    {
        var attackerCiv = _state!.Civilizations.FirstOrDefault(c => c.Index == attackerCity.CivilizationIndex);
        if (attackerCiv == null) return null;
        int range = CityAttackRange(attackerCiv);
        City? closest = null;
        int closestDist = int.MaxValue;

        foreach (var defenderCiv in _state.Civilizations)
        {
            if (defenderCiv.Index == attackerCiv.Index) continue;
            if (targetCivIndices != null && targetCivIndices.Count > 0 && !targetCivIndices.Contains(defenderCiv.Index)) continue;
            foreach (var defenderCity in defenderCiv.Cities)
            {
                if (defenderCity.Position.Z != attackerCity.Position.Z) continue;
                if (!IsCityVisibleTo(defenderCity, attackerCiv)) continue;
                int dist = attackerCity.Position.EdgeDistanceTo(defenderCity.Position);
                if (dist <= range && dist < closestDist)
                {
                    closest = defenderCity;
                    closestDist = dist;
                }
            }
        }

        return closest;
    }

    private bool IsCityVisibleTo(City city, Civilization civ)
    {
        var visibleMaps = _state!.Visibility.GetForZ(city.Position.Z);
        if (!visibleMaps.TryGetValue(civ.Index, out var visibleMap)) return true;
        return city.Position.GetHexes().Any(h => visibleMap.HasTile(h));
    }

    private bool ApplyAttackToCity(City targetCity, Action<CityBuildingDestroyedEventArgs> onCityBuildingDestroyed)
    {
        // Les soldats défenseurs absorbent l'attaque : les deux soldats meurent, la défense est intacte.
        if (targetCity.Soldiers > 0)
        {
            var defenderCiv = _state!.Civilizations.FirstOrDefault(c => c.Index == targetCity.CivilizationIndex);
            // Barbacane : si la défense est > 20, perd 1 défense au lieu d'un soldat.
            if (defenderCiv != null
                && defenderCiv.ModifierAggregator.HasModifier(ECategory.CITY_DEFENSE_PROTECTS_SOLDIERS)
                && targetCity.CurrentDefense > 20)
            {
                targetCity.CurrentDefense--;
                return false;
            }
            // Armures d'Acier : le défenseur peut survivre en consommant 1 Acier (l'attaque reste absorbée)
            if (SteelArmorEngine.TrySaveSoldiers(defenderCiv, targetCity, 1, _prng!) == 0)
                targetCity.Soldiers--;
            return false;
        }

        if (targetCity.CurrentDefense > 0)
        {
            targetCity.CurrentDefense--;
            return false;
        }

        var townHall = targetCity.Buildings.OfType<TownHall>().FirstOrDefault();
        if (townHall != null)
        {
            townHall.Level--;
            if (townHall.Level <= 0)
            {
                targetCity.Buildings.Remove(townHall);
                targetCity.InvalidateLevelCache();
                onCityBuildingDestroyed(new CityBuildingDestroyedEventArgs(targetCity.Position));
            }
            var defenderCivAfterAttack = _state!.Civilizations.FirstOrDefault(c => c.Index == targetCity.CivilizationIndex);
            if (defenderCivAfterAttack != null)
            {
                BuildingController.RecalculateStorageCapacity(defenderCivAfterAttack);
                defenderCivAfterAttack.TrimResourcesToMax();
            }
            return false;
        }

        return true;
    }
}
