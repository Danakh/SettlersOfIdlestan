using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Services.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures
{
    public class TreasureTrove : IslandFeature
    {
        public override GameEventType DiscoveredEventType => GameEventType.TreasureTroveDiscovered;
        public override GameEventType RemovedEventType    => GameEventType.TreasureTroveClaimed;

        public override bool IsDiscoverable => !Found;

        public override LocalizedEntry? GetTooltipEntry() => new("hex_tooltip_treasure_trove");

        public override string? SvgIconResourceName => "Resources.icons.features.chest.svg";
        public override float SvgIconSize => 18f;
        public override bool ShouldRenderIcon => true;

        public TreasureTrove(HexCoord position) : base(position) { }

        [JsonConstructor]
        public TreasureTrove() : base() { }
    }
}
