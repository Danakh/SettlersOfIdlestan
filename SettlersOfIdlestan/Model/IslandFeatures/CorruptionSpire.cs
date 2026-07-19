using System;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Spire de Corruption — Monument de l'Inframonde, plaçable uniquement sur une zone corrompue.
/// Construite par investissement progressif comme tout Monument. Une fois bâtie, réduit
/// systématiquement la corruption dans un rayon (voir <see cref="Radius"/>) autour de son hex ;
/// son rayon peut ensuite être amélioré indéfiniment par investissement, à un coût croissant de
/// 50% par niveau (voir <see cref="GetRadiusUpgradeCost"/>). Ne protège pas son hex des mécaniques
/// de Temple/débordement, qui peuvent toujours y agir normalement (voir CorruptionController).
/// </summary>
public class CorruptionSpire : Monument
{
    public override string? SvgIconResourceName => "Resources.icons.features.crystaltower.svg";
    public override float SvgIconSize => 40f;

    public override LocalizedEntry GetTooltipEntry()
        => new(Built ? "hex_tooltip_corruption_spire_built" : "hex_tooltip_corruption_spire", new object[] { Radius });

    /// <summary>True une fois l'investissement de construction terminé : le rayon devient alors améliorable.</summary>
    public bool Built { get; set; } = false;

    /// <summary>Rayon (en hexes) sur lequel la Spire réduit la corruption à chaque intervalle. Base 1, améliorable indéfiniment.</summary>
    public int Radius { get; set; } = 1;

    public static ResourceSet GetSpireCost() => new ResourceSet
    {
        { Resource.Stone,   20000 },
        { Resource.Gold,    20000 },
        { Resource.Steel,    2000 },
        { Resource.Crystal,  1000 },
        { Resource.Mithril,   200 },
    };

    /// <summary>Coût pour porter le rayon de radius - 1 à radius : coût de base × 1.5^(radius - 2) (radius ≥ 2).</summary>
    public static ResourceSet GetRadiusUpgradeCost(int radius)
    {
        double multiplier = Math.Pow(1.5, radius - 2);
        var cost = new ResourceSet();
        foreach (var kvp in GetSpireCost())
            cost.Add(kvp.Key, Math.Max(1, (int)Math.Round(kvp.Value * multiplier)));
        return cost;
    }

    public override ResourceSet GetInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv)
        => Built ? GetRadiusUpgradeCost(Radius + 1) : GetSpireCost();

    [JsonIgnore]
    public override string PanelTitleKey => Built ? "corruption_spire_panel_title_radius" : "corruption_spire_panel_title";

    [JsonIgnore]
    public override string? PanelTitleSuffix => Built ? Radius.ToString() : null;

    public CorruptionSpire(HexCoord position) : base(position) { }

    [JsonConstructor]
    public CorruptionSpire() : base() { }
}
