using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Spire de Corruption — Monument de l'Inframonde, plaçable uniquement sur une Source de Corruption
/// (voir <see cref="CorruptionSource"/>). Sa raison d'être est de détruire cette Source : achever sa
/// construction la supprime définitivement et convertit son niveau en bonus de prestige permanent
/// (voir CorruptionSpireController.OnInvestmentCycleCompleted et
/// PrestigeController.GetCorruptionClearBonusMultiplier). Construite par investissement progressif
/// comme tout Monument, mais d'un seul palier : une fois bâtie elle n'a plus rien à recevoir, son
/// niveau n'étant plus améliorable. Elle réduit alors la corruption dans un rayon fixe (voir
/// <see cref="DecayRadius"/>) autour de son hex, sans pour autant le protéger des mécaniques de
/// Temple/débordement, qui peuvent toujours y agir normalement (voir CorruptionController).
/// </summary>
public class CorruptionSpire : Monument
{
    /// <summary>Rayon (en hexes) sur lequel une Spire bâtie réduit la corruption à chaque intervalle — fixe : la Spire n'a plus de niveau à monter.</summary>
    public const int DecayRadius = 1;

    public override string? SvgIconResourceName => "Resources.icons.features.crystaltower.svg";
    public override float SvgIconSize => 40f;

    public override LocalizedEntry GetTooltipEntry()
        => new(Built ? "hex_tooltip_corruption_spire_built" : "hex_tooltip_corruption_spire", new object[] { DecayRadius });

    /// <summary>True une fois l'investissement de construction terminé : la Source a alors été détruite et le bonus de prestige acquis.</summary>
    public bool Built { get; set; } = false;

    public static ResourceSet GetSpireCost() => new ResourceSet
    {
        { Resource.Stone,   20000 },
        { Resource.Gold,    20000 },
        { Resource.Steel,    2000 },
        { Resource.Crystal,  1000 },
        { Resource.Mithril,   200 },
    };

    public override ResourceSet GetBaseInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv)
        => GetSpireCost();

    [JsonIgnore]
    public override string PanelTitleKey => "corruption_spire_panel_title";

    [JsonIgnore]
    public override string? PanelTitleSuffix => null;

    public CorruptionSpire(HexCoord position) : base(position) { }

    [JsonConstructor]
    public CorruptionSpire() : base() { }
}
