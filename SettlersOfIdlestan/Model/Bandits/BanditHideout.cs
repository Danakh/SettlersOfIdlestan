using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Services.Localization;

namespace SettlersOfIdlestan.Model.Bandits;

[Serializable]
public class BanditHideout : IslandFeature
{
    public const int MaxHp = 50;
    public const long SpawnIntervalTicks = 5_000L;
    public const int MaxBanditsOnIsland = 10;

    public int Hp { get; set; } = MaxHp;
    public long LastSpawnTick { get; set; } = 0;
    public long LastAttackedTick { get; set; } = 0;

    public override bool BlocksHarvest => true;
    public override GameEventType DiscoveredEventType => GameEventType.BanditHideoutDiscovered;
    public override GameEventType RemovedEventType    => GameEventType.BanditHideoutDestroyed;

    public override LocalizedEntry? GetTooltipEntry() =>
        Found ? new("hex_tooltip_bandit_hideout_info", [Hp, MaxHp]) : null;

    public BanditHideout(HexCoord position) : base(position) { }

    [JsonConstructor]
    public BanditHideout() : base() { }
}
