using System;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

public class Observatory : Monument
{
    public override LocalizedEntry GetTooltipEntry() => new("hex_tooltip_observatory");

    public override string? TextIcon => "🔭";

    public Observatory(HexCoord position) : base(position) { }
    public int Level { get; set; } = 0;

    public const int MaxLevel = 3;

    [JsonIgnore]
    public bool IsMaxLevel => Level >= MaxLevel;

    /// <summary>Coût statique du level-up — Verre/Minerai/Or, à l'échelle des derniers niveaux de la Merveille.</summary>
    public static ResourceSet GetLevelCost(int level) => new()
    {
        { Resource.Glass, 1000 * level * level },
        { Resource.Ore,   3000 * level * level },
        { Resource.Gold, 20000 * level * level },
    };

    public override ResourceSet GetInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv)
        => GetLevelCost(Level + 1);

    [JsonIgnore]
    public override string PanelTitleKey => "observatory_panel_title";

    [JsonIgnore]
    public override string? PanelTitleSuffix => IsMaxLevel ? Level.ToString() : (Level + 1).ToString();

    [JsonConstructor]
    public Observatory() : base() { }
}
