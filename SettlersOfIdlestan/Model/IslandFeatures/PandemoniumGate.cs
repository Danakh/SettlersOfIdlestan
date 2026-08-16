using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Portail du Pandémonium — apparaît non construit sur l'hex d'une Tentacule de l'Abysse abattue
/// (voir <see cref="Controller.Expand.PandemoniumGateController"/>), là où la Faille des Abysses,
/// elle, est une évolution volontaire de la Spire. Se bâtit ensuite par investissement progressif
/// comme tout Monument, pour exactement le même prix que la Faille (<see cref="AbyssGate.GetGateCost"/>) :
/// les deux portails ouvrent une couche, seul le chemin qui y mène diffère. Une fois bâti, ouvre le
/// Pandémonium (couche <see cref="LayerState.PandemoniumZ"/>).
/// </summary>
public class PandemoniumGate : Monument
{
    // Pas d'icône SVG statique : rendu comme un portail tourbillonnant procédural, au même titre
    // que la Faille des Abysses (voir GameBoardRenderer.DrawAbyssGatePortal).
    public override bool ShouldRenderIcon => false;

    public override LocalizedEntry GetTooltipEntry() => new(Built ? "hex_tooltip_pandemonium_gate_built" : "hex_tooltip_pandemonium_gate");

    /// <summary>True une fois l'investissement terminé.</summary>
    public bool Built { get; set; } = false;

    /// <summary>Même coût que la Faille des Abysses — voir le commentaire de classe.</summary>
    public override ResourceSet GetInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv) => AbyssGate.GetGateCost();

    [JsonIgnore]
    public override string PanelTitleKey => "pandemonium_gate_panel_title";

    [JsonIgnore]
    public override string? PanelTitleSuffix => null;

    public PandemoniumGate(HexCoord position) : base(position) { }

    [JsonConstructor]
    public PandemoniumGate() : base() { }
}
