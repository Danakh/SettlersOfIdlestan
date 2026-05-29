using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.IslandFeatures;

public class Wonder : IslandFeature
{
    public override bool BlocksHarvest => true;
    public override GameEventType DiscoveredEventType => GameEventType.WonderDiscovered;
    public override GameEventType RemovedEventType => GameEventType.WonderDiscovered;

    public Wonder(HexCoord position) : base(position) { }

    [JsonConstructor]
    public Wonder() : base() { }
}
