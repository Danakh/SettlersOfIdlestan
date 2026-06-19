using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Mine Profonde — Monument placé uniquement sur une Montagne.
/// Le joueur investit progressivement des ressources pour la creuser ; une fois creusée,
/// elle ouvre un avant-poste dans l'Inframonde.
/// </summary>
public class DeepestMine : Monument
{
    public override string? SvgIconResourceName => "Resources.icons.features.rockcave.svg";
    public override float SvgIconSize => 40f;

    public override LocalizedEntry GetTooltipEntry() => new(WasEverDug ? "hex_tooltip_deepest_mine_dug" : "hex_tooltip_deepest_mine");

    /// <summary>True quand le creusement est terminé et que l'Inframonde est accessible.</summary>
    public bool Dug { get; set; } = false;

    /// <summary>Reste true une fois que la mine a été creusée pour la première fois ; pilote l'icône.</summary>
    public bool WasEverDug { get; set; } = false;

    /// <summary>Coût statique du creusement jusqu'à l'Inframonde (pas de modificateur)</summary>
    public static ResourceSet GetDigCost() => new ResourceSet
    {
        { Resource.Stone, 1000 },
        { Resource.Ore,   2000 },
        { Resource.Gold,  2000 },
    };

    public override ResourceSet GetInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv) => GetDigCost();

    [JsonIgnore]
    public override string PanelTitleKey => "deepest_mine_panel_title";

    [JsonIgnore]
    public override string? PanelTitleSuffix => null;

    public DeepestMine(HexCoord position) : base(position) { }

    [JsonConstructor]
    public DeepestMine() : base() { }
}
