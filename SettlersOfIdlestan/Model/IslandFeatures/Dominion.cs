using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

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

    public Dominion() { }

    [JsonConstructor]
    public Dominion(HexCoord position, int level = 1) : base(position)
    {
        Level = level;
    }
}
