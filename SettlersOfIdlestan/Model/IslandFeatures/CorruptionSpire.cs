using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Spire de Corruption — Monument de l'Inframonde, plaçable uniquement sur une zone corrompue.
/// Construite par investissement progressif comme tout Monument. Une fois bâtie,
/// multiplie les points de prestige par 2 × le niveau de corruption courant.
/// </summary>
public class CorruptionSpire : Monument
{
    public override string? SvgIconResourceName => "Resources.icons.features.crystaltower.svg";
    public override float SvgIconSize => 40f;

    public override LocalizedEntry GetTooltipEntry() => new(Built ? "hex_tooltip_corruption_spire_built" : "hex_tooltip_corruption_spire");

    /// <summary>True une fois l'investissement terminé : la Spire amplifie alors les points de prestige.</summary>
    public bool Built { get; set; } = false;

    public static ResourceSet GetSpireCost() => new ResourceSet
    {
        { Resource.Stone,   20000 },
        { Resource.Gold,    20000 },
        { Resource.Steel,    2000 },
        { Resource.Crystal,  1000 },
        { Resource.Mithril,   200 },
    };

    public override ResourceSet GetInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv) => GetSpireCost();

    [JsonIgnore]
    public override string PanelTitleKey => "corruption_spire_panel_title";

    [JsonIgnore]
    public override string? PanelTitleSuffix => null;

    public CorruptionSpire(HexCoord position) : base(position) { }

    [JsonConstructor]
    public CorruptionSpire() : base() { }
}
