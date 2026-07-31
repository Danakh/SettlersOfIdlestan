using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.Monsters;

[Serializable]
public class Rats : MonsterFeature
{
    public const int RatsMaxHp = 5;
    public const int RatsMaxHpPerLevel = 5;
    public const long RatsMovementIntervalTicks = 300L;
    public const long RatsDepartureCooldownTicks = 500L;

    public override int MaxHp => RatsMaxHp + RatsMaxHpPerLevel * (Level - 1);
    public override bool BlocksHarvest => true;
    public override bool CanMove => true;
    public override long MovementIntervalTicks => RatsMovementIntervalTicks;
    public override long DepartureCooldownTicks => RatsDepartureCooldownTicks;

    public override string? SvgIconResourceName => "Resources.icons.military.monster-rats.svg";

    public override GameEventType DiscoveredEventType => GameEventType.RatsDiscovered;
    public override GameEventType RemovedEventType => GameEventType.RatsDefeated;

    public override LocalizedEntry? GetTooltipEntry() => Level > 1
        ? new("hex_tooltip_rats_info_leveled", [Hp, MaxHp, Level])
        : new("hex_tooltip_rats_info", [Hp, MaxHp]);

    public Rats(HexCoord position, int level = 1) : base(position) { Level = level; Hp = MaxHp; }

    [JsonConstructor]
    public Rats() : base() { Hp = MaxHp; }
}
