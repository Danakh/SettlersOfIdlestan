using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Percée de Surface — miroir exact de la <see cref="DeepestMine"/> pour les Elfes noirs, qui
/// commencent la partie dans l'Inframonde (voir RaceDefinition.StartsInUnderworld). Monument placé
/// sur une Montagne de l'Inframonde, creusé vers le haut par investissement progressif ; une fois
/// percée, elle pose la première ville de surface du joueur sur le vertex d'arrivée mémorisé à la
/// génération (LayerState.ArrivalVertex de la couche 0).
/// </summary>
public class SurfaceBreach : Monument
{
    public override string? SvgIconResourceName => "Resources.icons.features.rockcave.svg";
    public override float SvgIconSize => 40f;

    public override LocalizedEntry GetTooltipEntry() => new(WasEverDug ? "hex_tooltip_surface_breach_dug" : "hex_tooltip_surface_breach");

    /// <summary>True quand le percement est terminé et que la surface est accessible.</summary>
    public bool Dug { get; set; } = false;

    /// <summary>Reste true une fois la percée ouverte pour la première fois ; pilote l'icône.</summary>
    public bool WasEverDug { get; set; } = false;

    /// <summary>
    /// Coût statique du percement jusqu'à la surface (pas de modificateur) — miroir de
    /// <see cref="DeepestMine.GetDigCost"/>. Principal levier d'équilibrage de la race : c'est lui
    /// qui détermine combien de temps les Elfes noirs restent enfermés sous terre.
    /// </summary>
    public static ResourceSet GetDigCost() => new ResourceSet
    {
        { Resource.Stone, 1000 },
        { Resource.Ore,   2000 },
        { Resource.Gold,  2000 },
    };

    public override ResourceSet GetBaseInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv) => GetDigCost();

    [JsonIgnore]
    public override string PanelTitleKey => "surface_breach_panel_title";

    [JsonIgnore]
    public override string? PanelTitleSuffix => null;

    public SurfaceBreach(HexCoord position) : base(position) { }

    [JsonConstructor]
    public SurfaceBreach() : base() { }
}
