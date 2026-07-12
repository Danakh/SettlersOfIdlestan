using System;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

public class Wonder : Monument
{
    public override LocalizedEntry GetTooltipEntry() => new("hex_tooltip_wonder", new object[] { Level });

    public override string? SvgIconResourceName => $"Resources.icons.features.wonder_{Level}.svg";
    public override float SvgIconSize => 50f;

    public Wonder(HexCoord position) : base(position) { }
    public int Level { get; set; } = 0;

    /// <summary>Niveau maximum — correspond au dernier icône disponible (wonder_4.svg) et à l'achievement WonderLevel4.</summary>
    public const int MaxLevel = 4;

    [JsonIgnore]
    public bool IsMaxLevel => Level >= MaxLevel;

    /// <summary>Coût statique du level-up (non modifié à la création, modifié lors de l'appel à GetInvestmentCost)</summary>
    public static ResourceSet GetLevelCost(int level)
    {
        var cost = new ResourceSet
        {
            { Resource.Food,  5000 * level * level },
            { Resource.Wood,  5000 * level * level },
            { Resource.Brick, 5000 * level * level },
            { Resource.Stone, 5000 * level * level },
            { Resource.Gold,  10000 * level * level },
            { Resource.Ore,   2000 * level * level },
        };
        if (level >= 3)
        {
            cost[Resource.Glass] = 500 * level * level;
        }
        if (level >= 4)
        {
            cost[Resource.Steel] = 300 * level * level;
        }
        return cost;
    }

    /// <summary>Coût applique WonderCostReduction de la civilisation</summary>
    public override ResourceSet GetInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv)
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
    public override string PanelTitleKey => "wonder_panel_title";

    [JsonIgnore]
    public override string? PanelTitleSuffix => IsMaxLevel ? Level.ToString() : (Level + 1).ToString();

    [JsonConstructor]
    public Wonder() : base() { }
}
