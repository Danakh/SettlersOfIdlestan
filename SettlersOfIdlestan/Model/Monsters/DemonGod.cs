using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.Monsters;

/// <summary>
/// Dieu démon — occupant unique du centre du Pandémonium (voir PandemoniumGenerator), entouré de ses
/// Tentacules. Immobile comme elles, mais d'un tout autre ordre de grandeur : cinq fois les PV d'un
/// démon majeur, une armure qui absorbe la moitié des coups de soldat et des dégâts capables de raser
/// une ville entière. C'est le boss de la couche : rien n'en fait apparaître d'autre.
///
/// <para>Seul monstre à mener deux attaques de front (voir <see cref="GetAttack"/>) : la ruée qui
/// projette son icône sur une ville unique, et un déluge de boules de feu qui frappe tout ce qui est
/// à portée. Là où la Tentacule alterne ses deux salves sur une seule cadence, lui les cumule sur
/// deux comptes à rebours séparés — on ne peut donc plus lire son rythme pour anticiper la
/// prochaine salve, et un front étalé n'est jamais à l'abri.</para>
/// </summary>
[Serializable]
public class DemonGod : MonsterFeature
{
    public const int DemonGodMaxHp = MajorDemon.MajorDemonMaxHp * 5;
    public const int DemonGodMaxHpPerLevel = 100;

    /// <summary>Dégâts de la ruée — le double de ceux du déluge de boules de feu, dont il a hérité sa valeur d'origine (voir <see cref="DemonGodAreaAttackDamage"/>).</summary>
    public const int DemonGodAttackDamage = DemonGodAreaAttackDamage * 2;
    public const int DemonGodAttackDamagePerLevel = DemonGodAreaAttackDamagePerLevel * 2;

    /// <summary>Dégâts du déluge de boules de feu, sur chaque cible à portée.</summary>
    public const int DemonGodAreaAttackDamage = 25;
    public const int DemonGodAreaAttackDamagePerLevel = 5;

    public const double DemonGodHpRegenPerLevel = 2;
    public const long DemonGodAttackIntervalTicks = MajorDemon.MajorDemonAttackIntervalTicks;

    /// <summary>Cadence du déluge de boules de feu : une fois et demie plus lente que la ruée.</summary>
    public const long DemonGodAreaAttackIntervalTicks = DemonGodAttackIntervalTicks * 3 / 2;

    public override int MaxHp => DemonGodMaxHp + DemonGodMaxHpPerLevel * (Level - 1);
    public override bool BlocksHarvest => true;
    public override double Armor => 4;

    public override bool CanMove => false;

    /// <summary>Enraciné dans la Corruption : fait monter d'un point celle de son hex toutes les 10 s, jusqu'à 2× le niveau de corruption de l'île (voir CorruptionController.ProcessMonsterCorruptionGrowth).</summary>
    public override bool GeneratesCorruption => true;

    public override double HpRegenAmount => 10 + DemonGodHpRegenPerLevel * (Level - 1);
    public override long HpRegenIntervalTicks => Dragon.DragonHpRegenIntervalTicks;

    public override int AttackRangeInHexes => 2;
    public override long AttackIntervalTicks => DemonGodAttackIntervalTicks;
    public override bool IgnoresPalisade => true;
    public override int AttackDamage => DemonGodAttackDamage + DemonGodAttackDamagePerLevel * (Level - 1);
    public override int AttackResources => 25;

    /// <summary>Dégâts du déluge de boules de feu au niveau courant — la moitié de ceux de la ruée.</summary>
    [JsonIgnore]
    public int AreaAttackDamage => DemonGodAreaAttackDamage + DemonGodAreaAttackDamagePerLevel * (Level - 1);

    /// <summary>Deux attaques indépendantes : la ruée (indice 0) et le déluge de boules de feu (indice 1).</summary>
    public override int AttackCount => 2;

    /// <summary>
    /// Indice 0 — la ruée : cible unique, corps-à-corps (l'icône se jette dessus), le vol de
    /// ressources, et des dégâts doublés depuis qu'elle n'est plus sa seule attaque.
    /// Indice 1 — le déluge : un coup sur CHAQUE cible à portée 2, emplacements militaires comme
    /// monstres du joueur, sur le modèle de la salve de zone de la Tentacule et comme elle à
    /// distance — boules de feu, et aucun coup encaissé en retour. Une fois et demie plus lent que
    /// la ruée : les deux se recouvrent un cycle sur trois, et cette salve-là frappe alors en plus
    /// de l'autre, pas à sa place.
    /// </summary>
    public override MonsterAttack GetAttack(int index) => index == 0
        ? base.GetAttack(index)
        : base.GetAttack(index) with
        {
            Pattern = MonsterAttackPattern.AreaSweep,
            IntervalTicks = DemonGodAreaAttackIntervalTicks,
            Damage = AreaAttackDamage,
            IsRanged = true,
        };

    public override GameEventType DiscoveredEventType => GameEventType.DemonGodDiscovered;
    public override GameEventType RemovedEventType => GameEventType.DemonGodDefeated;

    public override string? SvgIconResourceName => "Resources.icons.military.kraken.svg";
    public override float IconSizeFactor => 2.2f;

    public override LocalizedEntry GetTooltipEntry() => Level > 1
        ? new("hex_tooltip_demon_god_info_leveled", [Hp, MaxHp, Level])
        : new("hex_tooltip_demon_god_info", [Hp, MaxHp]);

    public DemonGod(HexCoord position, int level = 1) : base(position) { Level = level; Hp = MaxHp; }

    [JsonConstructor]
    public DemonGod() : base() { Hp = MaxHp; }
}
