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

    public override GameEventType DiscoveredEventType => GameEventType.NoEvent;
    public override GameEventType RemovedEventType => GameEventType.NoEvent;

    /// <summary>Bonus intrinsèque de vitesse de récolte : +20% par niveau de Dominion.</summary>
    public const double IntrinsicHarvestBonusPerLevel = 0.20;

    public override LocalizedEntry? GetTooltipEntry() =>
        new("hex_tooltip_dominion_info", new object[] { Level, (int)(IntrinsicHarvestBonusPerLevel * 100 * Level) });

    /// <summary>
    /// Accélère la récolte : +20% de vitesse par niveau de Dominion, amplifié par le bonus de
    /// prestige DOMINION_HARVEST_SPEED_PER_LEVEL (+10% du bonus par vertex acheté autour de l'hex
    /// de prestige). Ex. niveau 5 avec 2 vertex (0.2) : 100% × 1.2 = +120% ⇒ délai de récolte ÷ 2.2.
    /// </summary>
    public override double GetHarvestTimeMultiplier(SettlersOfIdlestan.Model.Civilization.Civilization civ)
    {
        double prestigeAmplifier = civ.ModifierAggregator.ApplyModifiers(ECategory.DOMINION_HARVEST_SPEED_PER_LEVEL, "", 0.0);
        double bonus = IntrinsicHarvestBonusPerLevel * Level * (1.0 + prestigeAmplifier);
        return 1.0 / (1.0 + bonus);
    }

    public Dominion() { }

    [JsonConstructor]
    public Dominion(HexCoord position, int level = 1) : base(position)
    {
        Level = level;
    }
}
