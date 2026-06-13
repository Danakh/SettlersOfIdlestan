using System.Collections.Generic;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Mine Profonde — feature unique placée comme une Merveille, uniquement sur une Montagne.
/// Le joueur investit progressivement des ressources pour la creuser ; une fois creusée,
/// elle ouvre un avant-poste dans l'Inframonde.
/// </summary>
public class DeepestMine : IslandFeature, IInvestableFeature
{
    public override bool BlocksHarvest => true;
    public override bool IsDiscoverable => false;

    public override GameEventType DiscoveredEventType => GameEventType.NoEvent;
    public override GameEventType RemovedEventType => GameEventType.NoEvent;

    public override string? SvgIconResourceName => "Resources.icons.features.rockcave.svg";
    public override float SvgIconSize => 40f;

    public override LocalizedEntry GetTooltipEntry() => new(WasEverDug ? "hex_tooltip_deepest_mine_dug" : "hex_tooltip_deepest_mine");

    /// <summary>True quand le creusement est terminé et que l'Inframonde est accessible.</summary>
    public bool Dug { get; set; } = false;

    /// <summary>Reste true une fois que la mine a été creusée pour la première fois ; pilote l'icône.</summary>
    public bool WasEverDug { get; set; } = false;

    public Dictionary<Resource, long> InvestedResources { get; set; } = new();
    public List<Resource> InvestmentEnabled { get; set; } = new();
    public long LastInvestmentTick { get; set; } = 0;

    /// <summary>Coût total du creusement jusqu'à l'Inframonde.</summary>
    public static ResourceSet GetDigCost() => new ResourceSet
    {
        { Resource.Stone, 1000 },
        { Resource.Ore,    300 },
        { Resource.Gold,  1000 },
        { Resource.Steel, 1000 },
    };

    public ResourceSet GetInvestmentCost() => GetDigCost();

    [JsonIgnore]
    public string PanelTitleKey => "deepest_mine_panel_title";

    [JsonIgnore]
    public string? PanelTitleSuffix => null;

    public DeepestMine(HexCoord position) : base(position) { Found = true; }

    [JsonConstructor]
    public DeepestMine() : base() { }
}
