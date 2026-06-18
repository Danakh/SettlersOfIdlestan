using System.Collections.Generic;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Spire de Corruption — merveille de l'Inframonde, plaçable uniquement sur une zone corrompue.
/// Construite par investissement progressif comme une Merveille / Mine Profonde. Une fois bâtie,
/// multiplie les points de prestige par 2 × le niveau de corruption courant.
/// </summary>
public class CorruptionSpire : IslandFeature, IInvestableFeature
{
    public override bool BlocksHarvest => true;
    public override bool IsDiscoverable => false;

    public override GameEventType DiscoveredEventType => GameEventType.NoEvent;
    public override GameEventType RemovedEventType => GameEventType.NoEvent;

    public override string? SvgIconResourceName => "Resources.icons.features.crystaltower.svg";
    public override float SvgIconSize => 40f;

    public override LocalizedEntry GetTooltipEntry() => new(Built ? "hex_tooltip_corruption_spire_built" : "hex_tooltip_corruption_spire");

    /// <summary>True une fois l'investissement terminé : la Spire amplifie alors les points de prestige.</summary>
    public bool Built { get; set; } = false;

    public Dictionary<Resource, long> InvestedResources { get; set; } = new();
    public List<Resource> InvestmentEnabled { get; set; } = new();
    public long LastInvestmentTick { get; set; } = 0;

    public static ResourceSet GetSpireCost() => new ResourceSet
    {
        { Resource.Stone,   20000 },
        { Resource.Gold,    20000 },
        { Resource.Steel,    2000 },
        { Resource.Crystal,  1000 },
        { Resource.Mithril,   200 },
    };

    public ResourceSet GetInvestmentCost() => GetSpireCost();

    [JsonIgnore]
    public string PanelTitleKey => "corruption_spire_panel_title";

    [JsonIgnore]
    public string? PanelTitleSuffix => null;

    public CorruptionSpire(HexCoord position) : base(position) { Found = true; }

    [JsonConstructor]
    public CorruptionSpire() : base() { }
}
