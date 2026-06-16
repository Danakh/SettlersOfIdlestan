using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Military;

/// <summary>
/// Gère le combat des soldats contre les monstres (bandits, dragons, etc.).
/// </summary>
internal class MonsterCombatEngine
{
    private WorldState? _state;
    private readonly GamePRNG _prng = new();

    internal void Initialize(WorldState? state)
    {
        _state = state;
    }

    internal void ResolveMonsterCombat(long currentTick,
        Action<SoldierAttackEventArgs> onSoldierAttackedMonster)
    {
        if (_state == null) return;

        var deadMonsters = new List<MonsterFeature>();
        foreach (var monster in _state.Features.OfType<MonsterFeature>())
        {
            if (currentTick - monster.LastAttackedByMilitaryTick < MilitaryController.CombatIntervalTicks) continue;

            if (AttackMonsterWithSoldiers(monster, currentTick, onSoldierAttackedMonster) && monster.Hp <= 0)
                deadMonsters.Add(monster);
        }

        foreach (var m in deadMonsters)
        {
            _state.RemoveFeature(m);
            _state.EventLog.Add(m.RemovedEventType);
        }
    }

    internal bool AttackMonsterWithSoldiers(MonsterFeature monster, long currentTick,
        Action<SoldierAttackEventArgs> onSoldierAttackedMonster)
    {
        if (_state == null) return false;

        bool didAttack = false;
        foreach (var civ in _state.Civilizations)
        {
            if (monster.Hp <= 0) break;
            foreach (var city in civ.Cities)
            {
                if (monster.Hp <= 0) break;
                if (city.Soldiers == 0) continue;

                var cityHexes = city.Position.GetHexes();
                if (!cityHexes.Any(h => h.Equals(monster.Position))) continue;

                // Armes en Acier : consomme 1 ArmeAcier pour infliger 1 dégât supplémentaire
                bool hasSteelWeapon = civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_STEEL_WEAPONS)
                    && civ.GetResourceQuantity(Resource.SteelWeapon) >= 1;
                if (hasSteelWeapon) civ.RemoveResource(Resource.SteelWeapon, 1);

                // Armures d'Acier : le soldat peut survivre à l'assaut en consommant 1 Acier
                if (SteelArmorEngine.TrySaveSoldiers(civ, city, 1, _prng) == 0)
                    city.Soldiers--;
                monster.Hp--;
                if (hasSteelWeapon) monster.Hp--;
                monster.LastAttackedByMilitaryTick = currentTick;
                onSoldierAttackedMonster(new SoldierAttackEventArgs(city.Position, monster.Position));
                didAttack = true;
            }
        }
        return didAttack;
    }
}
