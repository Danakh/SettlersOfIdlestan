using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.Monsters;

[Serializable]
public class Dragon : MonsterFeature
{
    public const int DragonMaxHp = 100;
    public const int DragonMaxHpPerLevel = 20;
    public const int DragonAttackDamagePerLevel = 2;
    public const double DragonHpRegenPerLevel = 0.5;
    public const long DragonAttackIntervalTicks = 100L;
    public const long DragonHpRegenIntervalTicks = 100L;

    public override int MaxHp => DragonMaxHp + DragonMaxHpPerLevel * (Level - 1);
    public override bool BlocksHarvest => true;
    public override double Armor => Level;

    public override double HpRegenAmount => 1 + DragonHpRegenPerLevel * (Level - 1);
    public override long HpRegenIntervalTicks => DragonHpRegenIntervalTicks;

    public override int AttackRangeInHexes => 2;
    public override long AttackIntervalTicks => DragonAttackIntervalTicks;
    public override bool IgnoresPalisade => true;
    public override int AttackDamage => 5 + DragonAttackDamagePerLevel * (Level - 1);
    public override int AttackResources => 1;

    public override GameEventType DiscoveredEventType => GameEventType.DragonDiscovered;
    public override GameEventType RemovedEventType => GameEventType.DragonDefeated;

    public override string? SvgIconResourceName => "Resources.icons.military.monster-dragon.svg";
    public override float IconSizeFactor => 2.5f;

    public override LocalizedEntry? GetTooltipEntry() => Level > 1
        ? new("hex_tooltip_dragon_info_leveled", [Hp, MaxHp, Level])
        : new("hex_tooltip_dragon_info", [Hp, MaxHp]);

    public Dragon(HexCoord position, int level = 1) : base(position) { Level = level; Hp = MaxHp; }

    [JsonConstructor]
    public Dragon() : base() { Hp = MaxHp; }
}
