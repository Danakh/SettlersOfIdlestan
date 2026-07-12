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

    public override LocalizedEntry? GetTooltipEntry() =>
        new("hex_tooltip_dominion_info", new object[] { Level });

    /// <summary>Accélère la récolte selon le bonus de prestige DOMINION_HARVEST_SPEED_PER_LEVEL et le niveau du Dominion.</summary>
    public override double GetHarvestTimeMultiplier(SettlersOfIdlestan.Model.Civilization.Civilization civ)
    {
        double perLevel = civ.ModifierAggregator.ApplyModifiers(ECategory.DOMINION_HARVEST_SPEED_PER_LEVEL, "", 0.0);
        if (perLevel <= 0) return 1.0;
        return 1.0 / (1.0 + perLevel * Level);
    }

    public Dominion() { }

    [JsonConstructor]
    public Dominion(HexCoord position, int level = 1) : base(position)
    {
        Level = level;
    }
}
