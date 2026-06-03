using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Services.Localization;

namespace SettlersOfIdlestan.Model.Monsters;

[Serializable]
public class Dragon : MonsterFeature
{
    public const int DragonMaxHp = 100;
    public const long DragonAttackIntervalTicks = 100L;
    public const long DragonHpRegenIntervalTicks = 100L;

    public override int MaxHp => DragonMaxHp;
    public override bool BlocksHarvest => true;

    public override int HpRegenAmount => 1;
    public override long HpRegenIntervalTicks => DragonHpRegenIntervalTicks;

    public override int AttackRangeInHexes => 2;
    public override long AttackIntervalTicks => DragonAttackIntervalTicks;
    public override bool IgnoresPalisade => true;
    public override int AttackSoldiers => 1;
    public override int AttackDefense => 1;
    public override int AttackResources => 5;

    public override GameEventType DiscoveredEventType => GameEventType.DragonDiscovered;
    public override GameEventType RemovedEventType => GameEventType.DragonDefeated;

    public override string? SvgIconResourceName => "Resources.icons.military.monster-dragon.svg";
    public override float IconSizeFactor => 2.5f;

    public override LocalizedEntry? GetTooltipEntry() => new("hex_tooltip_dragon_info", [Hp, MaxHp]);

    public Dragon(HexCoord position) : base(position) { Hp = DragonMaxHp; }

    [JsonConstructor]
    public Dragon() : base() { Hp = DragonMaxHp; }
}
