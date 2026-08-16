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
/// </summary>
[Serializable]
public class DemonGod : MonsterFeature
{
    public const int DemonGodMaxHp = MajorDemon.MajorDemonMaxHp * 5;
    public const int DemonGodMaxHpPerLevel = 100;
    public const int DemonGodAttackDamagePerLevel = 5;
    public const double DemonGodHpRegenPerLevel = 2;
    public const long DemonGodAttackIntervalTicks = MajorDemon.MajorDemonAttackIntervalTicks;

    public override int MaxHp => DemonGodMaxHp + DemonGodMaxHpPerLevel * (Level - 1);
    public override bool BlocksHarvest => true;
    public override double Armor => 4;

    public override bool CanMove => false;

    public override double HpRegenAmount => 10 + DemonGodHpRegenPerLevel * (Level - 1);
    public override long HpRegenIntervalTicks => Dragon.DragonHpRegenIntervalTicks;

    public override int AttackRangeInHexes => 2;
    public override long AttackIntervalTicks => DemonGodAttackIntervalTicks;
    public override bool IgnoresPalisade => true;
    public override int AttackDamage => 25 + DemonGodAttackDamagePerLevel * (Level - 1);
    public override int AttackResources => 25;

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
