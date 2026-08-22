using System;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Observatoire — Monument débloqué par la recherche Cartes des Étoiles, placé uniquement sur une
/// Montagne de la surface. Chaque niveau abaisse le multiplicateur exponentiel du coût en points de
/// recherche des routes du Vide, de ×3 sans Observatoire à ×2 une fois complet (voir
/// <see cref="GetVoidRouteCostMultiplierForLevel"/> et RoadController.GetVoidRouteResearchCost).
/// L'investissement combine des ressources (Verre et Acier en masse, un peu de Mithril) et des
/// points de recherche (voir <see cref="GetLevelResearchCost"/>).
/// </summary>
public class Observatory : Monument
{
    private static readonly string[] IconStages = { "01-fondation", "02-socle", "03-tour", "04-coupole" };

    public override string? SvgIconResourceName => $"Resources.icons.features.observatoire-{IconStages[Math.Clamp(Level, 0, IconStages.Length - 1)]}.svg";
    public override float SvgIconSize => 50f;

    public override LocalizedEntry GetTooltipEntry() => new("hex_tooltip_observatory", new object[] { Level });

    public int Level { get; set; } = 0;

    /// <summary>Niveau maximum — dernière étape d'icône (coupole) et multiplicateur minimal des routes du Vide.</summary>
    public const int MaxLevel = 3;

    /// <summary>Multiplicateur du coût des routes du Vide sans Observatoire.</summary>
    public const double BaseVoidRouteCostMultiplier = 3.0;

    /// <summary>Multiplicateur atteint quand l'Observatoire est complet.</summary>
    public const double CompletedVoidRouteCostMultiplier = 2.0;

    [JsonIgnore]
    public bool IsMaxLevel => Level >= MaxLevel;

    /// <summary>
    /// Multiplicateur appliqué au coût exponentiel de la prochaine route du Vide : ×3 au niveau 0
    /// (aucun effet), puis un pas de 1/<see cref="MaxLevel"/> par niveau jusqu'à ×2 une fois
    /// l'Observatoire complet.
    /// </summary>
    public static double GetVoidRouteCostMultiplierForLevel(int level)
    {
        int clamped = Math.Clamp(level, 0, MaxLevel);
        return BaseVoidRouteCostMultiplier
             - (BaseVoidRouteCostMultiplier - CompletedVoidRouteCostMultiplier) * clamped / MaxLevel;
    }

    [JsonIgnore]
    public double VoidRouteCostMultiplier => GetVoidRouteCostMultiplierForLevel(Level);

    /// <summary>Coût statique du level-up — principalement Verre et Acier, un peu de Mithril.</summary>
    public static ResourceSet GetLevelCost(int level) => new()
    {
        { Resource.Glass,   3000 * level * level },
        { Resource.Steel,   2000 * level * level },
        { Resource.Mithril,  200 * level * level },
    };

    /// <summary>Coût en points de recherche du level-up : 1 000 000 × niveau visé.</summary>
    public static long GetLevelResearchCost(int level) => 1_000_000L * level;

    public override ResourceSet GetInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv)
        => GetLevelCost(Level + 1);

    /// <summary>Chaque niveau consomme aussi des points de recherche, jusqu'au niveau maximum.</summary>
    [JsonIgnore]
    public override bool UsesResearchInvestment => !IsMaxLevel;

    public override long GetRequiredResearch(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv)
        => GetLevelResearchCost(Level + 1);

    [JsonIgnore]
    public override string PanelTitleKey => "observatory_panel_title";

    [JsonIgnore]
    public override string? PanelTitleSuffix => IsMaxLevel ? Level.ToString() : (Level + 1).ToString();

    public Observatory(HexCoord position) : base(position) { }

    [JsonConstructor]
    public Observatory() : base() { }
}
