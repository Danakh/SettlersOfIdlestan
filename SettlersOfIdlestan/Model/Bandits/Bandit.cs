using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Services.Localization;

namespace SettlersOfIdlestan.Model.Bandits;

[Serializable]
public class Bandit : IslandFeature
{
    public const int MaxHp = 20;
    public const long RaidIntervalTicks = 100L;

    public long LastMovedTick { get; set; }
    public int Hp { get; set; } = MaxHp;
    public long LastRaidTick { get; set; } = 0;
    public Vertex? LastRaidTargetVertex { get; set; } = null;
    public string? LastStolenResource { get; set; } = null;

    public override bool BlocksHarvest => true;
    public override GameEventType DiscoveredEventType => GameEventType.BanditDiscovered;
    public override GameEventType RemovedEventType    => GameEventType.BanditDefeated;

    public override LocalizedEntry GetTooltipEntry() => new("hex_tooltip_bandit_info", [Hp, MaxHp]);

    public Bandit(HexCoord position, long lastMovedTick = 0) : base(position)
    {
        LastMovedTick = lastMovedTick;
        Hp = MaxHp;
    }

    [JsonConstructor]
    public Bandit() : base()
    {
        Hp = MaxHp;
    }
}
