using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.Monsters;

/// <summary>
/// Ogre des profondeurs — monstre errant de l'Inframonde. Lent mais dévastateur :
/// ses coups écrasent les palissades et démolissent les avant-postes mal défendus.
/// </summary>
[Serializable]
public class Ogre : MonsterFeature
{
    public const int OgreMaxHp = 100;
    public const int OgreMaxHpPerLevel = 10;
    public const int OgreAttackDamagePerLevel = 2;
    public const double OgreHpRegenPerLevel = 0.5;
    public const long OgreHpRegenIntervalTicks = 100L;

    public override int MaxHp => OgreMaxHp + OgreMaxHpPerLevel * (Level - 1);
    public override bool BlocksHarvest => true;

    public override bool CanMove => true;
    public override long MovementIntervalTicks => 6_000L;
    public override long DepartureCooldownTicks => 1_500L;

    public override double HpRegenAmount => OgreHpRegenPerLevel * (Level - 1);
    public override long HpRegenIntervalTicks => OgreHpRegenIntervalTicks;

    public override int AttackRangeInHexes => 1;
    public override long AttackIntervalTicks => 300L;
    public override bool IgnoresPalisade => true;
    public override int AttackDamage => 5 + OgreAttackDamagePerLevel * (Level - 1);
    public override int AttackResources => 5;

    public override GameEventType DiscoveredEventType => GameEventType.OgreDiscovered;
    public override GameEventType RemovedEventType => GameEventType.OgreDefeated;

    public override string? SvgIconResourceName => "Resources.icons.military.monster-ogre.svg";
    public override float IconSizeFactor => 1.8f;

    public override LocalizedEntry GetTooltipEntry() => Level > 1
        ? new("hex_tooltip_ogre_info_leveled", [Hp, MaxHp, Level])
        : new("hex_tooltip_ogre_info", [Hp, MaxHp]);

    public Ogre(HexCoord position, int level = 1) : base(position) { Level = level; Hp = MaxHp; }

    [JsonConstructor]
    public Ogre() : base() { Hp = MaxHp; }
}
