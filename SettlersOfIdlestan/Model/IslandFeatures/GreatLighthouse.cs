using System;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

public class GreatLighthouse : Monument
{
    public override LocalizedEntry GetTooltipEntry() => new("hex_tooltip_great_lighthouse", new object[] { Level });

    private static readonly string[] IconStages = { "01-fondation", "02-fut", "03-galerie", "04-fanal" };

    public override string? SvgIconResourceName => $"Resources.icons.features.grand-phare-{IconStages[Math.Clamp(Level, 0, IconStages.Length - 1)]}.svg";
    public override float SvgIconSize => 50f;

    public GreatLighthouse(HexCoord position) : base(position) { }
    public int Level { get; set; } = 0;

    public const int MaxLevel = 3;

    [JsonIgnore]
    public bool IsMaxLevel => Level >= MaxLevel;

    /// <summary>Coût statique du level-up — Verre/Brique/Pierre/Or, à l'échelle des derniers niveaux de la Merveille.</summary>
    public static ResourceSet GetLevelCost(int level) => new()
    {
        { Resource.Glass, 2000 * level * level },
        { Resource.Brick, 5000 * level * level },
        { Resource.Stone, 2000 * level * level },
        { Resource.Gold, 5000 * level * level },
    };

    public override ResourceSet GetInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv)
        => GetLevelCost(Level + 1);

    [JsonIgnore]
    public override string PanelTitleKey => "great_lighthouse_panel_title";

    [JsonIgnore]
    public override string? PanelTitleSuffix => IsMaxLevel ? Level.ToString() : (Level + 1).ToString();

    [JsonConstructor]
    public GreatLighthouse() : base() { }
}
