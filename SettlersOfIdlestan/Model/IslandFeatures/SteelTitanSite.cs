using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Socle du Titan d'Acier — Monument à palier unique : une fois son investissement couvert, le
/// colosse allié (<see cref="SettlersOfIdlestan.Model.Monsters.SteelTitan"/>) apparaît sur son hex
/// et le socle, lui, reste posé. Voir SteelTitanController.
///
/// <para>Débloqué par le vertex de prestige Titan d'Acier (UNLOCK_STEEL_TITAN). Un seul à la fois :
/// tant que le colosse vit, le socle n'a plus rien à recevoir (<see cref="TitanForged"/>) et aucun
/// autre socle ne peut être posé. Perdre le colosse — au combat ou en le démantelant depuis le
/// panneau du socle — rouvre la fonte sur place ; démanteler le socle lui-même rouvre la pose
/// ailleurs.</para>
/// </summary>
public class SteelTitanSite : Monument
{
    public override string? SvgIconResourceName => "Resources.icons.features.colosse-01-socle.svg";
    public override float SvgIconSize => 40f;

    /// <summary>
    /// True tant que le colosse fondu depuis ce socle est en vie. Persisté plutôt que déduit de la
    /// présence d'un <see cref="SettlersOfIdlestan.Model.Monsters.SteelTitan"/> sur la carte : c'est
    /// la comparaison des deux qui permet à SteelTitanController.ReopenSiteAfterTitanLoss de
    /// reconnaître la mort du colosse d'un tick à l'autre, rechargement de partie compris.
    /// </summary>
    public bool TitanForged { get; set; } = false;

    public override LocalizedEntry GetTooltipEntry()
        => new(TitanForged ? "hex_tooltip_steel_titan_site_forged" : "hex_tooltip_steel_titan_site");

    /// <summary>Coût unique de la fonte du Titan : beaucoup de Pierre, puis du Minerai, puis du Mithril.</summary>
    public static ResourceSet GetForgingCost() => new ResourceSet
    {
        { Resource.Stone,   50000 },
        { Resource.Ore,     20000 },
        { Resource.Mithril,  5000 },
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
