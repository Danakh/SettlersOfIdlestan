using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Chantier du Titan d'Acier — Monument à palier unique : une fois son investissement couvert, il
/// disparaît et laisse sur son hex le colosse allié
/// (<see cref="SettlersOfIdlestan.Model.Monsters.SteelTitan"/>). Voir SteelTitanController.
///
/// <para>Débloqué par le vertex de prestige Titan d'Acier (UNLOCK_STEEL_TITAN). Un seul à la fois :
/// tant que le colosse vit, aucun nouveau chantier ne peut être posé — il faut donc le perdre pour
/// en refondre un.</para>
/// </summary>
public class SteelTitanSite : Monument
{
    public override string? SvgIconResourceName => "Resources.icons.features.colosse-01-socle.svg";
    public override float SvgIconSize => 40f;

    public override LocalizedEntry GetTooltipEntry() => new("hex_tooltip_steel_titan_site");

    /// <summary>Coût unique de la fonte du Titan : beaucoup de Pierre, puis du Minerai, puis du Mithril.</summary>
    public static ResourceSet GetForgingCost() => new ResourceSet
    {
        { Resource.Stone,   50000 },
        { Resource.Ore,     20000 },
        { Resource.Mithril,  1000 },
    };

    public override ResourceSet GetBaseInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv)
        => GetForgingCost();

    [JsonIgnore]
    public override string PanelTitleKey => "steel_titan_panel_title";

    [JsonIgnore]
    public override string? PanelTitleSuffix => null;

    public SteelTitanSite(HexCoord position) : base(position) { }

    [JsonConstructor]
    public SteelTitanSite() : base() { }
}
