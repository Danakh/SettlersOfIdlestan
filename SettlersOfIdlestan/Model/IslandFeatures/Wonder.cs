using System.Collections.Generic;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

public class Wonder : IslandFeature
{
    public override bool BlocksHarvest => true;
    public override bool IsDiscoverable => false;

    public override LocalizedEntry GetTooltipEntry() => new("hex_tooltip_wonder");

    public override GameEventType DiscoveredEventType => GameEventType.NoEvent;
    public override GameEventType RemovedEventType => GameEventType.NoEvent;

    public override string? SvgIconResourceName => $"Resources.icons.features.wonder_{Level}.svg";
    public override float SvgIconSize => 50f;

    public Wonder(HexCoord position) : base(position) { Found = true; }
    public int Level { get; set; } = 0;

    public Dictionary<Resource, long> InvestedResources { get; set; } = new();
    public List<Resource> InvestmentEnabled { get; set; } = new();
    public long LastInvestmentTick { get; set; } = 0;

    [JsonConstructor]
    public Wonder() : base() { }
}
