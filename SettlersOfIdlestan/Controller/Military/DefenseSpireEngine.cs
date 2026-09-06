using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;

namespace SettlersOfIdlestan.Controller.Military;

/// <summary>
/// Tirs automatiques des Spires de Défense : une fois par seconde, chaque spire active frappe un
/// monstre à <see cref="DefenseSpire.AttackRangeInHexes"/> hexs ou moins de sa ville, pour
/// 1 Cristal et 1 dégât qui ignore l'armure.
///
/// <para>Volontairement séparé de <see cref="MonsterCombatEngine"/> : la spire ne consomme ni soldat
/// ni arme, ne partage pas le cooldown d'emplacement militaire (une ville peut donc tirer à la spire
/// et au soldat dans la même seconde) et n'est pas soumise à ATTACK_SPEED — son rythme est fixe.</para>
/// </summary>
internal sealed class DefenseSpireEngine
{
    private WorldState? _state;

    internal void Initialize(WorldState? state) => _state = state;

    /// <summary>
    /// Distance ville → monstre, comptée depuis le plus ÉLOIGNÉ des 3 hexs de la ville — même
    /// convention que <c>MonsterCombatEngine.DistanceTo</c>, pour que la portée 2 annoncée au joueur
    /// soit la même que celle des attaques à distance des soldats.
    /// </summary>
    private static int DistanceTo(City city, MonsterFeature monster)
    {
        var hexes = city.Position.GetHexes();
        int max = 0;
        for (int i = 0; i < hexes.Length; i++)
        {
            int d = hexes[i].DistanceTo(monster.Position);
            if (d > max) max = d;
        }
        return max;
    }

    /// <summary>
    /// Une passe de tirs. Comme les autres résolutions de combat (voir MilitaryController.Update), une
    /// seule par événement d'horloge : le tir choisit une cible, il ne fait pas progresser un taux, et
    /// le rejouer en rafale pendant un saut de temps ferait fondre les Cristaux sur des cibles qui
    /// n'ont pas eu l'occasion de bouger ni de mourir entre deux.
    /// </summary>
    internal void ResolveSpireAttacks(long currentTick, Action<SoldierAttackEventArgs> onSpireAttackedMonster)
    {
        if (_state == null) return;

        List<MonsterFeature>? deadMonsters = null;
        var civilizations = _state.Civilizations;
        for (int c = 0; c < civilizations.Count; c++)
        {
            var civ = civilizations[c];
            var cities = civ.GetCitiesWith(BuildingType.DefenseSpire);
            for (int i = 0; i < cities.Count; i++)
            {
                var city = cities[i];
                var spire = city.FindBuilding<DefenseSpire>(BuildingType.DefenseSpire);
                if (spire == null || spire.Level < 1) continue;
                if (spire.ActivationStatus != ActivationStatus.ACTIVE) continue;
                if (currentTick - spire.LastAttackTick < DefenseSpire.AttackIntervalTicks) continue;

                var target = FindTarget(city);
                if (target == null) continue;

                // Le Cristal n'est prélevé qu'une fois une cible trouvée : une spire qui ne voit aucun
                // monstre ne coûte rien. Le cooldown n'avance pas non plus faute de Cristal, pour que la
                // spire tire dès que le stock revient plutôt qu'à la prochaine seconde ronde.
                if (civ.GetResourceQuantity(Resource.Crystal) < DefenseSpire.CrystalCostPerAttack)
                {
                    civ.RaiseLowStock(Resource.Crystal);
                    continue;
                }
                civ.RemoveResource(Resource.Crystal, DefenseSpire.CrystalCostPerAttack);

                // Dégâts bruts : la Spire perce les armures (pas de MonsterFeature.ApplyArmorReduction).
                target.Hp -= DefenseSpire.DamagePerAttack;
                spire.LastAttackTick = currentTick;
                onSpireAttackedMonster(new SoldierAttackEventArgs(city.Position, target.Position));

                if (target.Hp <= 0)
                {
                    target.KilledByCivilizationIndex = civ.Index;
                    (deadMonsters ??= new List<MonsterFeature>()).Add(target);
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

    /// <summary>
    /// Monstre hostile le plus proche à portée de spire, sur la même couche.
    ///
    /// <para>Le filtre de découverte est <see cref="Model.IslandFeatures.IslandFeature.Found"/>, pas la
    /// carte de visibilité courante : sans Tour de guet une ville ne voit qu'à 1 hex, et exiger la
    /// visibilité réduirait la portée 2 annoncée à une portée 1 la plupart du temps. Found est aussi ce
    /// que le joueur voit réellement dessiné (MonsterRenderer) — la spire ne tire donc jamais sur un
    /// monstre encore inconnu de lui.</para>
    ///
    /// <para>Les monstres « amis » (l'Aventurier) ne sont jamais ciblés, comme pour les soldats.</para>
    /// </summary>
    private MonsterFeature? FindTarget(City city)
    {
        MonsterFeature? best = null;
        int bestDistance = int.MaxValue;

        var features = _state!.Features;
        for (int i = 0; i < features.Count; i++)
        {
            if (features[i] is not MonsterFeature monster) continue;
            if (monster.AttacksOtherMonsters) continue;
            if (!monster.Found) continue;
            if (monster.Hp <= 0) continue;
            if (monster.Position.Z != city.Position.Z) continue;

            int distance = DistanceTo(city, monster);
            if (distance > DefenseSpire.AttackRangeInHexes || distance >= bestDistance) continue;

            best = monster;
            bestDistance = distance;
        }

        return best;
    }
}
