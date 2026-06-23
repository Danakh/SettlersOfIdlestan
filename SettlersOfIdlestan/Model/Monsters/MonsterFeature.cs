using System.Collections.Generic;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;

namespace SettlersOfIdlestan.Model.Monsters;

/// <summary>
/// Classe de base pour toutes les features de type "monstre" : présence sur la carte,
/// points de vie, combat avec les soldats, capacité optionnelle d'attaquer les villes.
/// </summary>
[Serializable]
public abstract class MonsterFeature : IslandFeature
{
    public int Hp { get; set; }
    public abstract int MaxHp { get; }

    /// <summary>Tick du dernier combat initié par les soldats ennemis.</summary>
    public long LastAttackedByMilitaryTick { get; set; } = 0;

    public override float SvgIconSize => 24f;
    public override bool ShouldRenderIcon => false;

    // ── Mouvement (opt-in) ─────────────────────────────────────────────────
    public override bool CanMove => false;
    public virtual long MovementIntervalTicks => long.MaxValue;
    /// <summary>Nombre d'hexes parcourus à chaque déplacement (1 = un seul pas).</summary>
    public virtual int MovementRangeInHexes => 1;
    /// <summary>Ticks de blocage de récolte laissés sur l'ancien hex après départ. 0 = pas de cooldown.</summary>
    public virtual long DepartureCooldownTicks => 0L;
    /// <summary>Tick du dernier déplacement (utilisé pour la grâce après mouvement).</summary>
    public long LastMovedTick { get; set; } = 0;

    // ── Régénération de PV (opt-in) ────────────────────────────────────────
    public virtual int HpRegenAmount => 0;
    public virtual long HpRegenIntervalTicks => long.MaxValue;
    public long LastHpRegenTick { get; set; } = 0;

    // ── Attaque des villes (opt-in) ────────────────────────────────────────
    /// <summary>Portée en hexes : 0 = n'attaque pas, 1 = hex propre, 2 = hex propre + voisins.</summary>
    public virtual int AttackRangeInHexes => 0;
    public virtual long AttackIntervalTicks => long.MaxValue;
    public virtual bool IgnoresPalisade => false;
    /// <summary>
    /// Dégâts infligés en cascade : soldats d'abord, puis défense, puis niveaux de Townhall (1 par point).
    /// Quand le Townhall tombe à 0 il est détruit ; les dégâts restants détruisent la ville.
    /// </summary>
    public virtual int AttackDamage => 0;
    /// <summary>Ressources volées (une par tirage) lors d'une attaque réussie.</summary>
    public virtual int AttackResources => 0;
    public long LastAttackTick { get; set; } = 0;
    public Vertex? LastAttackTargetVertex { get; set; } = null;
    /// <summary>Noms des ressources volées lors de la dernière attaque (séparés par virgule), pour l'animation.</summary>
    public string? LastAttackResourcesString { get; set; } = null;

    /// <summary>
    /// Opt-in : ce monstre est « ami » et combat les autres monstres au lieu des villes
    /// (utilise quand même AttackRangeInHexes/AttackIntervalTicks/AttackDamage ci-dessus).
    /// Les monstres amis ne s'attaquent jamais entre eux et sont ignorés par le combat
    /// automatique des soldats (cf. MonsterCombatEngine).
    /// </summary>
    public virtual bool AttacksOtherMonsters => false;

    // ── Invocation de nouvelles créatures (opt-in) ─────────────────────────
    /// <summary>Tente de générer une nouvelle MonsterFeature. Retourne null si aucune invocation n'a lieu.</summary>
    public virtual MonsterFeature? TrySpawn(IReadOnlyList<MonsterFeature> existingMonsters, long tick) => null;
    public long LastSpawnTick { get; set; } = 0;

    protected MonsterFeature(HexCoord position) : base(position) { }
    protected MonsterFeature() : base() { }
}
