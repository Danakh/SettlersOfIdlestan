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

    /// <summary>
    /// Dégâts d'une attaque de soldat avant réduction d'armure : 1 de base, majoré par
    /// SOLDIER_ATTACK_DAMAGE (Bras de Dieu), plus 1 si une Arme en Acier est consommée.
    /// </summary>
    private static int SoldierDamage(Civilization civ)
        => civ.ModifierAggregator.ApplyModifiers(ECategory.SOLDIER_ATTACK_DAMAGE, "", 1);

    internal void ResolveMonsterCombat(long currentTick,
        Action<SoldierAttackEventArgs> onSoldierAttackedMonster,
        Action<ConsumableConsumedEventArgs> onConsumableConsumed)
    {
        if (_state == null) return;

        // Liste allouée seulement si un monstre meurt réellement — le cas rare, alors que cette
        // méthode tourne à chaque événement d'horloge.
        List<MonsterFeature>? deadMonsters = null;
        var features = _state.Features;
        for (int i = 0; i < features.Count; i++)
        {
            if (features[i] is not MonsterFeature monster) continue;
            if (AttackMonsterWithSoldiers(monster, currentTick, onSoldierAttackedMonster, onConsumableConsumed) && monster.Hp <= 0)
                (deadMonsters ??= new List<MonsterFeature>()).Add(monster);
        }

        if (deadMonsters == null) return;
        foreach (var m in deadMonsters)
        {
            _state.RemoveFeature(m);
            _state.EventLog.Add(m.RemovedEventType);
        }
    }

    internal bool AttackMonsterWithSoldiers(MonsterFeature monster, long currentTick,
        Action<SoldierAttackEventArgs> onSoldierAttackedMonster,
        Action<ConsumableConsumedEventArgs> onConsumableConsumed)
    {
        if (_state == null) return false;
        if (monster.AttacksOtherMonsters) return false; // monstres "amis" (ex. Aventurier) : jamais ciblés par les soldats

        // Délégué construit une fois par appel, hors des boucles : il ne capture plus que le rappel
        // reçu en paramètre, l'emplacement lui étant transmis par TrySaveSoldiers.
        Action<IMilitaryVertex, Resource> onConsumed =
            (v, res) => onConsumableConsumed(new ConsumableConsumedEventArgs(v.Position, res));

        bool didAttack = false;
        var civilizations = _state.Civilizations;
        for (int c = 0; c < civilizations.Count; c++)
        {
            var civ = civilizations[c];
            if (monster.Hp <= 0) break;

            // Une seule agrégation de modifiers par civilisation : cette boucle imbriquée est
            // parcourue pour chaque monstre, chaque civilisation et chaque emplacement militaire —
            // en fin de partie, des dizaines de milliers de tours par événement d'horloge.
            long combatInterval = EffectiveCombatInterval(civ);

            // Grâce après déplacement : la cible ne peut pas être attaquée juste après s'être déplacée.
            if (currentTick - monster.LastAttackedByMilitaryTick < combatInterval) continue;

            bool steelWeaponsUnlocked = civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_STEEL_WEAPONS);
            // Comme l'intervalle de combat : agrégé une fois par civilisation, pas par emplacement.
            int soldierDamage = SoldierDamage(civ);

            var vertices = civ.MilitaryVertices;
            for (int i = 0; i < vertices.Count; i++)
            {
                var vertex = vertices[i];
                if (monster.Hp <= 0) break;
                if (vertex.Soldiers == 0) continue;
                // Cooldown porté par l'emplacement : commun aux attaques de ville/flotte et de monstre, pour qu'un même
                // emplacement ne puisse pas frapper deux cibles différentes trop vite — mais plusieurs peuvent attaquer en simultané.
                if (currentTick - vertex.LastAttackTick < combatInterval) continue;

                // IsAdjacentTo compare les trois hexs sans passer par GetHexes().Any(h => ...), dont
                // le lambda capturait le monstre et allouait donc une fermeture par emplacement.
                if (!vertex.Position.IsAdjacentTo(monster.Position)) continue;

                // Armes en Acier : consomme 1 ArmeAcier pour infliger 1 dégât supplémentaire
                bool hasSteelWeapon = steelWeaponsUnlocked
                    && civ.GetResourceQuantity(Resource.SteelWeapon) >= 1;
                if (hasSteelWeapon) civ.RemoveResource(Resource.SteelWeapon, 1);

                // Armures d'Acier : le soldat peut survivre à l'assaut en consommant 1 Acier
                if (SteelArmorEngine.TrySaveSoldiers(civ, vertex, 1, _prng!, onConsumed) == 0)
                    vertex.Soldiers--;
                int rawDamage = soldierDamage + (hasSteelWeapon ? 1 : 0);
                monster.Hp -= MonsterFeature.ApplyArmorReduction(rawDamage, monster.Armor, _prng!);
                if (monster.Hp <= 0) monster.KilledByCivilizationIndex = civ.Index;
                vertex.LastAttackTick = currentTick;
                onSoldierAttackedMonster(new SoldierAttackEventArgs(vertex.Position, monster.Position));
                didAttack = true;
            }
        }
        return didAttack;
    }

    // ── Attaque à distance (Surveillance + Tour de guet) ────────────────────

    private const int MeleeRange = 1;
    private const int MaxRangedAttackDistance = 2;

    /// <summary>
    /// Distance entre l'emplacement et le monstre, au sens le plus strict : la distance depuis le hex
    /// le plus ÉLOIGNÉ du monstre. Utiliser le minimum sur les 3 hexes permettrait d'attaquer 1 hex plus
    /// loin que prévu (les 3 hexes d'un vertex sont mutuellement adjacents, donc le hex le plus proche
    /// peut être à 1 de moins que les deux autres) ; le maximum garantit que la portée affichée au joueur
    /// (« distance 2 ») n'est jamais dépassée, quel que soit le coin depuis lequel on compte.
    /// </summary>
    private static int DistanceTo(IMilitaryVertex vertex, MonsterFeature monster)
        => vertex.Position.GetHexes().Max(h => h.DistanceTo(monster.Position));

    /// <summary>
    /// Détermine si un emplacement militaire peut attaquer une MonsterFeature : toujours possible à
    /// distance ≤ 1 (corps-à-corps automatique), possible à distance 2 avec la techno Surveillance et
    /// une Tour de guet active (uniquement pour une ville — une Flotte de Guerre n'a pas de bâtiments,
    /// voir WarFleet), sinon trop loin.
    /// </summary>
    internal MonsterAttackAvailability GetAttackAvailability(IMilitaryVertex vertex, MonsterFeature monster)
    {
        if (monster.AttacksOtherMonsters) return MonsterAttackAvailability.TooFar; // monstres "amis" : jamais attaquables

        int distance = DistanceTo(vertex, monster);
        if (distance <= MeleeRange) return MonsterAttackAvailability.Available;

        var civ = _state?.GetCivilization(vertex.CivilizationIndex);
        bool hasSurveillance = civ != null && civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_RANGED_MONSTER_ATTACK);
        if (distance > MaxRangedAttackDistance || !hasSurveillance) return MonsterAttackAvailability.TooFar;

        bool hasWatchtower = vertex is City city && city.FindBuilding(BuildingType.Watchtower) is { Level: > 0 };
        return hasWatchtower ? MonsterAttackAvailability.Available : MonsterAttackAvailability.RequiresWatchtower;
    }

    /// <summary>
    /// Résout les attaques à distance (distance 2) initiées par un flux joueur sur
    /// <see cref="IMilitaryVertex.MonsterAttackTarget"/>. Le corps-à-corps (distance ≤ 1) reste géré
    /// par <see cref="ResolveMonsterCombat"/>.
    /// </summary>
    internal void ResolveRangedAttacks(long currentTick,
        Action<SoldierAttackEventArgs> onSoldierAttackedMonster,
        Action<ConsumableConsumedEventArgs> onConsumableConsumed)
    {
        if (_state == null) return;

        // Même motif que ResolveMonsterCombat : délégué unique hors des boucles, liste des morts
        // allouée seulement s'il y en a.
        Action<IMilitaryVertex, Resource> onConsumed =
            (v, res) => onConsumableConsumed(new ConsumableConsumedEventArgs(v.Position, res));

        List<MonsterFeature>? deadMonsters = null;
        foreach (var civ in _state.Civilizations)
        {
            var vertices = civ.MilitaryVertices;
            for (int i = 0; i < vertices.Count; i++)
            {
                var vertex = vertices[i];
                if (vertex.MonsterAttackTarget == null) continue;
                if (vertex.Soldiers == 0) continue;

                var monster = _state.Features.OfType<MonsterFeature>().FirstOrDefault(m => m.Position.Equals(vertex.MonsterAttackTarget));
                if (monster == null) { vertex.MonsterAttackTarget = null; continue; }
                if (monster.Hp <= 0) continue;

                int distance = DistanceTo(vertex, monster);
                if (distance <= MeleeRange) continue; // déjà géré par le combat de corps-à-corps automatique

                // Grâce après déplacement : la cible ne peut pas être attaquée juste après s'être déplacée.
                if (currentTick - monster.LastAttackedByMilitaryTick < EffectiveCombatInterval(civ)) continue;
                // Cooldown porté par l'emplacement : commun aux attaques de ville/flotte et de monstre (cf. AttackMonsterWithSoldiers).
                if (currentTick - vertex.LastAttackTick < EffectiveCombatInterval(civ)) continue;
                if (GetAttackAvailability(vertex, monster) != MonsterAttackAvailability.Available) continue;

                bool hasSteelWeapon = civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_STEEL_WEAPONS)
                    && civ.GetResourceQuantity(Resource.SteelWeapon) >= 1;
                if (hasSteelWeapon) civ.RemoveResource(Resource.SteelWeapon, 1);

                if (SteelArmorEngine.TrySaveSoldiers(civ, vertex, 1, _prng!, onConsumed) == 0)
                    vertex.Soldiers--;
                int rawDamage = SoldierDamage(civ) + (hasSteelWeapon ? 1 : 0);
                monster.Hp -= MonsterFeature.ApplyArmorReduction(rawDamage, monster.Armor, _prng!);
                vertex.LastAttackTick = currentTick;
                onSoldierAttackedMonster(new SoldierAttackEventArgs(vertex.Position, monster.Position));

                if (monster.Hp <= 0)
                {
                    monster.KilledByCivilizationIndex = civ.Index;
                    (deadMonsters ??= new List<MonsterFeature>()).Add(monster);
                }
            }
        }

        if (deadMonsters == null) return;
        foreach (var m in deadMonsters.Distinct())
        {
            _state.RemoveFeature(m);
            _state.EventLog.Add(m.RemovedEventType);
        }
    }
}
