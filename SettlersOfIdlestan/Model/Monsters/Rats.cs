using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.Monsters;

[Serializable]
public class Rats : MonsterFeature
{
    public const int RatsMaxHp = 5;
    public const long RatsMovementIntervalTicks = 300L;
    public const long RatsDepartureCooldownTicks = 500L;

    public override int MaxHp => RatsMaxHp;
    public override bool BlocksHarvest => true;
    public override bool CanMove => true;
    public override long MovementIntervalTicks => RatsMovementIntervalTicks;
    public override long DepartureCooldownTicks => RatsDepartureCooldownTicks;

    public override string? SvgIconResourceName => "Resources.icons.military.monster-rats.svg";

    public override GameEventType DiscoveredEventType => GameEventType.RatsDiscovered;
    public override GameEventType RemovedEventType => GameEventType.RatsDefeated;

    public override LocalizedEntry? GetTooltipEntry() => new("hex_tooltip_rats_info", [Hp, MaxHp]);

    public Rats(HexCoord position) : base(position) { Hp = MaxHp; }

    [JsonConstructor]
    public Rats() : base() { Hp = MaxHp; }
}
