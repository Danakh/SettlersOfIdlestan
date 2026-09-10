namespace SettlersOfIdlestan.Model.Monsters;

/// <summary>Forme d'une attaque déclarée par un monstre — voir <see cref="MonsterAttack"/>.</summary>
public enum MonsterAttackPattern
{
    /// <summary>Une seule cible, <see cref="MonsterAttack.Strikes"/> coups dessus. Le cas de tous les monstres ordinaires (un coup, une cible).</summary>
    Focused,

    /// <summary>Un coup sur CHACUNE des cibles à portée : emplacements militaires et monstres « amis » du joueur.</summary>
    AreaSweep,

    /// <summary>
    /// Alterne <see cref="AreaSweep"/> et <see cref="Focused"/> à chaque déclenchement, via
    /// <see cref="MonsterFeature.NextAttackIsAreaSweep"/> : une seule cadence, deux salves qui se
    /// succèdent. C'est le motif de la Tentacule.
    /// </summary>
    Alternating,
}

/// <summary>
/// Une attaque déclarée par un monstre : sa forme, sa cadence, sa portée et ce qu'elle inflige.
/// Un monstre en déclare une ou plusieurs (voir <see cref="MonsterFeature.AttackCount"/> et
/// <see cref="MonsterFeature.GetAttack"/>), chacune avec son propre compte à rebours —
/// <see cref="MonsterFeature.GetAttackSlotTick"/> — donc totalement indépendantes les unes des
/// autres : le Dieu démon frappe ainsi au corps-à-corps ET balaie ses cibles de boules de feu à une
/// cadence différente.
///
/// <para><c>readonly record struct</c> et non une classe : le contrôleur redemande sa description à
/// chaque attaque de chaque monstre à chaque pas de simulation — un type valeur n'alloue rien, et
/// les valeurs dépendantes du niveau (dégâts) restent recalculées à la volée sans cache à invalider.</para>
/// </summary>
/// <param name="Pattern">Zone, cible unique, ou alternance des deux.</param>
/// <param name="IntervalTicks">Cadence propre à cette attaque.</param>
/// <param name="RangeInHexes">Portée : 0 = pas d'attaque, 1 = hex propre, 2 = hex propre + voisins (le rayon balayé vaut toujours <c>RangeInHexes - 1</c>).</param>
/// <param name="Damage">Dégâts d'un coup, en cascade sur soldats → défense → Hôtel de Ville.</param>
/// <param name="Strikes">Nombre de coups d'une salve concentrée (ignoré par <see cref="MonsterAttackPattern.AreaSweep"/>).</param>
/// <param name="Resources">Ressources volées à chaque cible touchée.</param>
/// <param name="IsRanged">Frappe de loin : ne subit pas le coup en retour d'un monstre balayé, et s'affiche en boules de feu au lieu d'une ruée de l'icône.</param>
/// <param name="IgnoresPalisade">Traverse la Palissade de la ville visée.</param>
public readonly record struct MonsterAttack(
    MonsterAttackPattern Pattern,
    long IntervalTicks,
    int RangeInHexes,
    int Damage,
    int Strikes,
    int Resources,
    bool IsRanged,
    bool IgnoresPalisade);
