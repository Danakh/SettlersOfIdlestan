using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;
using SettlersOfIdlestan.Model.Civilization;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.IslandFeatures;

public class Dominion : IslandFeature
{
    public int Level { get; set; } = 1;

    public override bool BlocksHarvest => false;
    public override bool IsDiscoverable => false;
    public override bool ShouldRenderIcon => false; // cercle doré rendu par le renderer, pas une icône
    public override bool BlocksMonumentPlacement => false;

    public override GameEventType DiscoveredEventType => GameEventType.NoEvent;
    public override GameEventType RemovedEventType => GameEventType.NoEvent;

    /// <summary>Bonus intrinsèque de vitesse de récolte : +10% par niveau de Dominion.</summary>
    public const double IntrinsicHarvestBonusPerLevel = 0.10;

    public override LocalizedEntry? GetTooltipEntry() =>
        new("hex_tooltip_dominion_info", new object[] { Level, (int)(IntrinsicHarvestBonusPerLevel * 100 * Level) });

    public override LocalizedEntry? GetTooltipEntry(SettlersOfIdlestan.Model.Civilization.Civilization civ) =>
        new("hex_tooltip_dominion_info", new object[] { Level, (int)(GetTotalHarvestBonus(civ) * 100) });

    /// <summary>
    /// Bonus total de vitesse de récolte sur cet hex : +10% par niveau de Dominion, amplifié par le
    /// bonus de prestige DOMINION_HARVEST_SPEED_PER_LEVEL (+10% du bonus par vertex acheté autour de
    /// l'hex de prestige), puis doublé si la civilisation possède la Ziggourat
    /// (DOMINION_HARVEST_SPEED_DOUBLED). Ex. niveau 5 avec 2 vertex (0.2) : 50% × 1.2 = +60%
    /// (+120% avec Ziggourat).
    /// </summary>
    private double GetTotalHarvestBonus(SettlersOfIdlestan.Model.Civilization.Civilization civ)
    {
        double prestigeAmplifier = civ.ModifierAggregator.ApplyModifiers(ECategory.DOMINION_HARVEST_SPEED_PER_LEVEL, "", 0.0);
        double bonus = IntrinsicHarvestBonusPerLevel * Level * (1.0 + prestigeAmplifier);
        if (civ.ModifierAggregator.HasModifier(ECategory.DOMINION_HARVEST_SPEED_DOUBLED))
            bonus *= 2.0;
        return bonus;
    }

    /// <summary>Accélère la récolte selon <see cref="GetTotalHarvestBonus"/> : délai de récolte ÷ (1 + bonus).</summary>
    public override double GetHarvestTimeMultiplier(SettlersOfIdlestan.Model.Civilization.Civilization civ) =>
        1.0 / (1.0 + GetTotalHarvestBonus(civ));

    public Dominion() { }

    [JsonConstructor]
    public Dominion(HexCoord position, int level = 1) : base(position)
    {
        Level = level;
    }
}
