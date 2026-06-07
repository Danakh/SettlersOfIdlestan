using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Monsters;

namespace SettlersOfIdlestan.Controller.Military;

/// <summary>
/// Gère le combat des soldats contre les monstres (bandits, dragons, etc.).
/// </summary>
internal class MonsterCombatEngine
{
    private WorldState? _state;

    internal void Initialize(WorldState? state)
    {
        _state = state;
    }

    internal void ResolveMonsterCombat(long currentTick,
        Action<SoldierAttackEventArgs> onSoldierAttackedMonster,
        Action<SoldierAttackEventArgs> onSoldierAttackedBandit)
    {
        if (_state == null) return;

        var deadMonsters = new List<MonsterFeature>();
        foreach (var monster in _state.Features.OfType<MonsterFeature>())
        {
            if (currentTick - monster.LastAttackedByMilitaryTick < MilitaryController.CombatIntervalTicks) continue;

            if (AttackMonsterWithSoldiers(monster, currentTick, onSoldierAttackedMonster, onSoldierAttackedBandit) && monster.Hp <= 0)
                deadMonsters.Add(monster);
        }

        foreach (var m in deadMonsters)
        {
            _state.RemoveFeature(m);
            _state.EventLog.Add(m.RemovedEventType);
        }
    }

    internal bool AttackMonsterWithSoldiers(MonsterFeature monster, long currentTick,
        Action<SoldierAttackEventArgs> onSoldierAttackedMonster,
        Action<SoldierAttackEventArgs> onSoldierAttackedBandit)
    {
        if (_state == null) return false;

        foreach (var civ in _state.Civilizations)
        {
            foreach (var city in civ.Cities)
            {
                if (city.Soldiers == 0) continue;

                var cityHexes = city.Position.GetHexes();
                if (!cityHexes.Any(h => h.Equals(monster.Position))) continue;

                city.Soldiers--;
                monster.Hp--;
                monster.LastAttackedByMilitaryTick = currentTick;
                onSoldierAttackedMonster(new SoldierAttackEventArgs(city.Position, monster.Position));
                onSoldierAttackedBandit(new SoldierAttackEventArgs(city.Position, monster.Position));
                return true;
            }
        }
        return false;
    }
}
