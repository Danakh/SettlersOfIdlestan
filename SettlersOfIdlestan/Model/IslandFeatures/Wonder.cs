using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

public class Wonder : IslandFeature, IInvestableFeature
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

    public static ResourceSet GetLevelCost(int level) => new ResourceSet
    {
        { Resource.Food,  5000 * level * level },
        { Resource.Wood,  5000 * level * level },
        { Resource.Brick, 5000 * level * level },
        { Resource.Stone, 5000 * level * level },
        { Resource.Gold,  10000 * level * level },
        { Resource.Ore,   2000 * level * level },
    };

    public ResourceSet GetInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv)
    {
        var baseCost = GetLevelCost(Level + 1);
        double reduction = playerCiv.WonderCostReduction;
        if (reduction <= 0) return baseCost;

        var reduced = new ResourceSet();
        foreach (var kvp in baseCost)
            reduced.Add(kvp.Key, Math.Max(1, (int)(kvp.Value * (1.0 - reduction))));
        return reduced;
    }

    [JsonIgnore]
    public string PanelTitleKey => "wonder_panel_title";

    [JsonIgnore]
    public string? PanelTitleSuffix => (Level + 1).ToString();

    [JsonConstructor]
    public Wonder() : base() { }
}
