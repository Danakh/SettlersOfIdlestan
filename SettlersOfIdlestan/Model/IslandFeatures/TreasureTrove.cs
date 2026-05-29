using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.IslandFeatures
{
    public class TreasureTrove : IslandFeature
    {
        public bool Claimed { get; set; }

        public override GameEventType DiscoveredEventType => GameEventType.TreasureTroveDiscovered;
        public override GameEventType RemovedEventType    => GameEventType.TreasureTroveClaimed;

        public override bool IsDiscoverable => !Found && !Claimed;

        public TreasureTrove(HexCoord position) : base(position) { }

        [JsonConstructor]
        public TreasureTrove() : base() { }
    }
}
