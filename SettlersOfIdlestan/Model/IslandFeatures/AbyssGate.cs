using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Faille des Abysses — évolution de la Spire de Corruption. Débloquée quand une Spire bâtie
/// repose sur une zone de corruption de niveau <see cref="RequiredCorruptionLevel"/> ou plus ;
/// remplace alors la Spire sur son hex. Construite par investissement progressif comme tout
/// Monument. Reprend pour l'instant le même bonus de prestige que la Spire de Corruption.
/// </summary>
public class AbyssGate : Monument
{
    /// <summary>Niveau de corruption minimum requis sur le hex de la Spire pour débloquer l'évolution.</summary>
    public const int RequiredCorruptionLevel = 4;

    // Pas d'icône SVG statique : rendue comme un portail tourbillonnant procédural
    // (voir GameBoardRenderer.DrawAbyssGatePortal), au même titre que le cercle de Corruption.
    public override bool ShouldRenderIcon => false;

    public override LocalizedEntry GetTooltipEntry() => new(Built ? "hex_tooltip_abyss_gate_built" : "hex_tooltip_abyss_gate");

    /// <summary>True une fois l'investissement terminé.</summary>
    public bool Built { get; set; } = false;

    public static ResourceSet GetGateCost() => new ResourceSet
    {
        { Resource.Gold,     50000 },
        { Resource.Crystal,   3000 },
        { Resource.Mithril,    500 },
    };

    public override ResourceSet GetInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv) => GetGateCost();

    [JsonIgnore]
    public override string PanelTitleKey => "abyss_gate_panel_title";

    [JsonIgnore]
    public override string? PanelTitleSuffix => null;

    public AbyssGate(HexCoord position) : base(position) { }

    [JsonConstructor]
    public AbyssGate() : base() { }
}
