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
/// Gère les attaques militaires entre emplacements militaires (villes et Flottes de Guerre — voir
/// IMilitaryVertex) de civilisations adverses.
/// </summary>
internal class CityAttackEngine
{
    private WorldState? _state;
    private CityBuilderController? _cityBuilderController;
    private WarFleetController? _warFleetController;
    private MobileCampController? _mobileCampController;
    private GamePRNG? _prng;

    private const int DefaultCityAttackRange = 3;

    internal void Initialize(WorldState? state, CityBuilderController? cityBuilderController, WarFleetController? warFleetController = null, MobileCampController? mobileCampController = null, GamePRNG? prng = null)
    {
        _state = state;
        _cityBuilderController = cityBuilderController;
        _warFleetController = warFleetController;
        _mobileCampController = mobileCampController;
        _prng = prng;
    }

    internal int CityAttackRange(Civilization civ)
        => civ.ModifierAggregator.ApplyModifiers(ECategory.CITY_ATTACK_RANGE, "", DefaultCityAttackRange);

    internal void ResolveCityAttacks(long currentTick,
        Action<CityAttackEventArgs> onSoldierAttackedCity,
        Action<CityBuildingDestroyedEventArgs> onCityBuildingDestroyed)
    {
        var toDestroy = new List<(Civilization civ, IMilitaryVertex vertex)>();

        foreach (var attackerCiv in _state!.Civilizations)
        {
            foreach (var attackerVertex in attackerCiv.MilitaryVertices.ToList())
            {
                var raidTarget = _state!.AutomationSettings.RaidTargetVertex;
                bool isRaidAttack = raidTarget != null && attackerVertex.FlowTarget?.Equals(raidTarget) == true;
                long baseInterval = isRaidAttack
                    ? MilitaryController.CityAttackIntervalTicks / 2
                    : MilitaryController.CityAttackIntervalTicks;
                double speed = attackerCiv.ModifierAggregator.ApplyModifiers(ECategory.ATTACK_SPEED, "", 1.0);
                long effectiveInterval = Math.Max(1L, (long)(baseInterval / speed));
                if (currentTick - attackerVertex.LastAttackTick < effectiveInterval) continue;
                if (attackerVertex.Soldiers == 0) continue;
                if (attackerVertex.FlowTarget == null) continue;

                var targetVertex = FindEnemyCityAt(attackerVertex.FlowTarget, attackerCiv);
                if (targetVertex == null) continue;
                if (attackerVertex.Position.EdgeDistanceTo(targetVertex.Position) > CityAttackRange(attackerCiv)) continue;

                // Armes en Acier : consomme 1 ArmeAcier pour infliger 1 dégât supplémentaire
                bool hasSteelWeapon = attackerCiv.ModifierAggregator.HasModifier(ECategory.UNLOCK_STEEL_WEAPONS)
                    && attackerCiv.GetResourceQuantity(Resource.SteelWeapon) >= 1;
                if (hasSteelWeapon) attackerCiv.RemoveResource(Resource.SteelWeapon, 1);

                // Armures d'Acier : le soldat envoyé peut survivre en consommant 1 ArmureAcier
                if (SteelArmorEngine.TrySaveSoldiers(attackerCiv, attackerVertex, 1, _prng!) == 0)
                    attackerVertex.Soldiers--;
                attackerVertex.LastAttackTick = currentTick;

                var path = HexGridPathfinder.FindVertexPath(attackerVertex.Position, targetVertex.Position);
                onSoldierAttackedCity(new CityAttackEventArgs(attackerVertex.Position, targetVertex.Position, path));

                // Vendetta : une civilisation qui attaque le joueur devient la cible des raids automatiques
                // (voir ReinforcementEngine.ResolvePlayerAutoVendetta).
                var playerCiv = _state.PlayerCivilization;
                if (targetVertex.CivilizationIndex == playerCiv.Index && attackerCiv.Index != playerCiv.Index
                    && playerCiv.ModifierAggregator.HasModifier(ECategory.UNLOCK_VENDETTA))
                {
                    _state.AutomationSettings.VendettaTargetCivIndex = attackerCiv.Index;
                }

                bool destroyed = ApplyAttackToCity(targetVertex, onCityBuildingDestroyed);
                if (!destroyed && hasSteelWeapon)
                    destroyed = ApplyAttackToCity(targetVertex, onCityBuildingDestroyed);
                if (destroyed)
                {
                    var ownerCiv = _state.Civilizations.FirstOrDefault(c => c.Index == targetVertex.CivilizationIndex);
                    if (ownerCiv != null)
                        toDestroy.Add((ownerCiv, targetVertex));
                }
            }
        }

        foreach (var (civ, vertex) in toDestroy)
        {
            ClearFlowsTargeting(vertex.Position);
            if (vertex is City city)
                _cityBuilderController?.DestroyCity(city, CityDestructionCause.Combat);
            else if (vertex is WarFleet fleet)
                _warFleetController?.DestroyFleet(fleet);
            else if (vertex is MobileCamp camp)
                _mobileCampController?.DestroyMobileCamp(camp);
        }
    }

    internal IMilitaryVertex? FindEnemyCityAt(Vertex target, Civilization attackerCiv)
    {
        foreach (var civ in _state!.Civilizations)
        {
            if (civ.Index == attackerCiv.Index) continue;
            var vertex = civ.MilitaryVertices.FirstOrDefault(v => v.Position.Equals(target));
            if (vertex != null) return vertex;
        }
        return null;
    }

    internal void ClearFlowsTargeting(Vertex position)
    {
        if (_state == null) return;
        foreach (var vertex in _state.Civilizations.SelectMany(c => c.MilitaryVertices))
            if (vertex.FlowTarget != null && vertex.FlowTarget.Equals(position))
                vertex.FlowTarget = null;
    }

    internal IMilitaryVertex? FindNearbyEnemyCity(IMilitaryVertex attackerVertex, IReadOnlyCollection<int>? targetCivIndices = null)
    {
        var attackerCiv = _state!.Civilizations.FirstOrDefault(c => c.Index == attackerVertex.CivilizationIndex);
        if (attackerCiv == null) return null;
        int range = CityAttackRange(attackerCiv);
        IMilitaryVertex? closest = null;
        int closestDist = int.MaxValue;

        foreach (var defenderCiv in _state.Civilizations)
        {
            if (defenderCiv.Index == attackerCiv.Index) continue;
            if (targetCivIndices != null && targetCivIndices.Count > 0 && !targetCivIndices.Contains(defenderCiv.Index)) continue;
            foreach (var defenderVertex in defenderCiv.MilitaryVertices)
            {
                if (defenderVertex.Position.Z != attackerVertex.Position.Z) continue;
                if (!IsCityVisibleTo(defenderVertex, attackerCiv)) continue;
                int dist = attackerVertex.Position.EdgeDistanceTo(defenderVertex.Position);
                if (dist <= range && dist < closestDist)
                {
                    closest = defenderVertex;
                    closestDist = dist;
                }
            }
        }

        return closest;
    }

    private bool IsCityVisibleTo(IMilitaryVertex vertex, Civilization civ)
    {
        var visibleMaps = _state!.Visibility.GetForZ(vertex.Position.Z);
        if (!visibleMaps.TryGetValue(civ.Index, out var visibleMap)) return true;
        return visibleMap.IsVertexVisible(vertex.Position);
    }

    private bool ApplyAttackToCity(IMilitaryVertex targetVertex, Action<CityBuildingDestroyedEventArgs> onCityBuildingDestroyed)
    {
        // Les soldats défenseurs absorbent l'attaque : les deux soldats meurent, la défense est intacte.
        if (targetVertex.Soldiers > 0)
        {
            var defenderCiv = _state!.Civilizations.FirstOrDefault(c => c.Index == targetVertex.CivilizationIndex);
            // Barbacane : si la défense est > 20, perd 1 défense au lieu d'un soldat.
            if (defenderCiv != null
                && defenderCiv.ModifierAggregator.HasModifier(ECategory.CITY_DEFENSE_PROTECTS_SOLDIERS)
                && targetVertex.CurrentDefense > 20)
            {
                targetVertex.CurrentDefense--;
                return false;
            }
            // Armures d'Acier : le défenseur peut survivre en consommant 1 Acier (l'attaque reste absorbée)
            if (SteelArmorEngine.TrySaveSoldiers(defenderCiv, targetVertex, 1, _prng!) == 0)
                targetVertex.Soldiers--;
            return false;
        }

        if (targetVertex.CurrentDefense > 0)
        {
            targetVertex.CurrentDefense--;
            return false;
        }

        // Une Flotte de Guerre n'a pas de bâtiments (voir WarFleet) : une fois soldats et défense
        // épuisés, le coup suivant la détruit directement (pas d'étape "structurelle" façon TownHall).
        if (targetVertex is not City targetCity)
            return true;

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
