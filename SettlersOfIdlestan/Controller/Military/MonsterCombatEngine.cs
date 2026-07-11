using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
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
    private GamePRNG? _prng;

    internal void Initialize(WorldState? state, GamePRNG? prng = null)
    {
        _state = state;
        _prng = prng;
    }

    /// <summary>Intervalle effectif entre deux attaques contre un même monstre, après ATTACK_SPEED.</summary>
    private static long EffectiveCombatInterval(Civilization civ)
    {
        double speed = civ.ModifierAggregator.ApplyModifiers(ECategory.ATTACK_SPEED, "", 1.0);
        return Math.Max(1L, (long)(MilitaryController.CombatIntervalTicks / speed));
    }

    internal void ResolveMonsterCombat(long currentTick,
        Action<SoldierAttackEventArgs> onSoldierAttackedMonster)
    {
        if (_state == null) return;

        var deadMonsters = new List<MonsterFeature>();
        foreach (var monster in _state.Features.OfType<MonsterFeature>())
        {
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
        if (monster.AttacksOtherMonsters) return false; // monstres "amis" (ex. Aventurier) : jamais ciblés par les soldats

        bool didAttack = false;
        foreach (var civ in _state.Civilizations)
        {
            if (monster.Hp <= 0) break;
            // Grâce après déplacement : la cible ne peut pas être attaquée juste après s'être déplacée.
            if (currentTick - monster.LastAttackedByMilitaryTick < EffectiveCombatInterval(civ)) continue;
            foreach (var city in civ.Cities)
            {
                if (monster.Hp <= 0) break;
                if (city.Soldiers == 0) continue;
                // Cooldown porté par la ville : commun aux attaques de ville et de monstre, pour qu'une même ville
                // ne puisse pas frapper deux cibles différentes trop vite — mais plusieurs villes attaquent en simultané.
                if (currentTick - city.LastAttackTick < EffectiveCombatInterval(civ)) continue;

                var cityHexes = city.Position.GetHexes();
                if (!cityHexes.Any(h => h.Equals(monster.Position))) continue;

                // Armes en Acier : consomme 1 ArmeAcier pour infliger 1 dégât supplémentaire
                bool hasSteelWeapon = civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_STEEL_WEAPONS)
                    && civ.GetResourceQuantity(Resource.SteelWeapon) >= 1;
                if (hasSteelWeapon) civ.RemoveResource(Resource.SteelWeapon, 1);

                // Armures d'Acier : le soldat peut survivre à l'assaut en consommant 1 Acier
                if (SteelArmorEngine.TrySaveSoldiers(civ, city, 1, _prng!) == 0)
                    city.Soldiers--;
                monster.Hp--;
                if (hasSteelWeapon) monster.Hp--;
                if (monster.Hp <= 0) monster.KilledByCivilizationIndex = civ.Index;
                city.LastAttackTick = currentTick;
                onSoldierAttackedMonster(new SoldierAttackEventArgs(city.Position, monster.Position));
                didAttack = true;
            }
        }
        return didAttack;
    }

    // ── Attaque à distance (Surveillance + Tour de guet) ────────────────────

    private const int MeleeRange = 1;
    private const int MaxRangedAttackDistance = 2;

    /// <summary>
    /// Portée d'attaque à distance effective : +1 si l'Observatoire (niveau 1+) est bâti et que les
    /// Tours de Guet sont débloquées via le vertex de prestige (BUILDING_MAX_LEVEL "Watchtower" > 0
    /// — seule source de ce modificateur actuellement).
    /// </summary>
    private int GetMaxRangedAttackDistance(Civilization? civ)
    {
        if (civ == null) return MaxRangedAttackDistance;
        var observatory = _state?.Features.OfType<Observatory>().FirstOrDefault();
        if (observatory == null || observatory.Level < 1) return MaxRangedAttackDistance;
        bool watchtowerUnlocked = civ.ModifierAggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Watchtower", 0) > 0;
        return watchtowerUnlocked ? MaxRangedAttackDistance + 1 : MaxRangedAttackDistance;
    }

    /// <summary>
    /// Distance entre la ville et le monstre, au sens le plus strict : la distance depuis le hex
    /// de ville le plus ÉLOIGNÉ du monstre. Utiliser le minimum sur les 3 hexes de la ville permettrait
    /// d'attaquer 1 hex plus loin que prévu (les 3 hexes d'un vertex sont mutuellement adjacents, donc
    /// le hex le plus proche peut être à 1 de moins que les deux autres) ; le maximum garantit que la
    /// portée affichée au joueur (« distance 2 ») n'est jamais dépassée, quel que soit le coin de la
    /// ville depuis lequel on compte.
    /// </summary>
    private static int DistanceTo(City city, MonsterFeature monster)
        => city.Position.GetHexes().Max(h => h.DistanceTo(monster.Position));

    /// <summary>
    /// Détermine si une ville peut attaquer une MonsterFeature : toujours possible à distance ≤ 1
    /// (corps-à-corps automatique), possible à distance 2 avec la techno Surveillance et une Tour de
    /// guet active, sinon trop loin.
    /// </summary>
    internal MonsterAttackAvailability GetAttackAvailability(City city, MonsterFeature monster)
    {
        if (monster.AttacksOtherMonsters) return MonsterAttackAvailability.TooFar; // monstres "amis" : jamais attaquables

        int distance = DistanceTo(city, monster);
        if (distance <= MeleeRange) return MonsterAttackAvailability.Available;

        var civ = _state?.Civilizations.FirstOrDefault(c => c.Index == city.CivilizationIndex);
        bool hasSurveillance = civ != null && civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_RANGED_MONSTER_ATTACK);
        if (distance > GetMaxRangedAttackDistance(civ) || !hasSurveillance) return MonsterAttackAvailability.TooFar;

        bool hasWatchtower = city.Buildings.OfType<Watchtower>().Any(b => b.Level > 0);
        return hasWatchtower ? MonsterAttackAvailability.Available : MonsterAttackAvailability.RequiresWatchtower;
    }

    /// <summary>
    /// Résout les attaques à distance (distance 2) initiées par un flux joueur sur <see cref="City.MonsterAttackTarget"/>.
    /// Le corps-à-corps (distance ≤ 1) reste géré par <see cref="ResolveMonsterCombat"/>.
    /// </summary>
    internal void ResolveRangedAttacks(long currentTick, Action<SoldierAttackEventArgs> onSoldierAttackedMonster)
    {
        if (_state == null) return;

        var deadMonsters = new List<MonsterFeature>();
        foreach (var civ in _state.Civilizations)
        {
            foreach (var city in civ.Cities)
            {
                if (city.MonsterAttackTarget == null) continue;
                if (city.Soldiers == 0) continue;

                var monster = _state.Features.OfType<MonsterFeature>().FirstOrDefault(m => m.Position.Equals(city.MonsterAttackTarget));
                if (monster == null) { city.MonsterAttackTarget = null; continue; }
                if (monster.Hp <= 0) continue;

                int distance = DistanceTo(city, monster);
                if (distance <= MeleeRange) continue; // déjà géré par le combat de corps-à-corps automatique

                // Grâce après déplacement : la cible ne peut pas être attaquée juste après s'être déplacée.
                if (currentTick - monster.LastAttackedByMilitaryTick < EffectiveCombatInterval(civ)) continue;
                // Cooldown porté par la ville : commun aux attaques de ville et de monstre (cf. AttackMonsterWithSoldiers).
                if (currentTick - city.LastAttackTick < EffectiveCombatInterval(civ)) continue;
                if (GetAttackAvailability(city, monster) != MonsterAttackAvailability.Available) continue;

                bool hasSteelWeapon = civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_STEEL_WEAPONS)
                    && civ.GetResourceQuantity(Resource.SteelWeapon) >= 1;
                if (hasSteelWeapon) civ.RemoveResource(Resource.SteelWeapon, 1);

                if (SteelArmorEngine.TrySaveSoldiers(civ, city, 1, _prng!) == 0)
                    city.Soldiers--;
                monster.Hp--;
                if (hasSteelWeapon) monster.Hp--;
                city.LastAttackTick = currentTick;
                onSoldierAttackedMonster(new SoldierAttackEventArgs(city.Position, monster.Position));

                if (monster.Hp <= 0)
                {
                    monster.KilledByCivilizationIndex = civ.Index;
                    deadMonsters.Add(monster);
                }
            }
        }

        foreach (var m in deadMonsters.Distinct())
        {
            _state.RemoveFeature(m);
            _state.EventLog.Add(m.RemovedEventType);
        }
    }
}
