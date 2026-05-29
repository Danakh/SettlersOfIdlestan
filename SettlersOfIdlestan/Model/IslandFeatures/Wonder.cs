using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.IslandFeatures;

public class Wonder : IslandFeature
{
    public override bool BlocksHarvest => true;

    public override string? SvgIconResourceName => "Resources.icons.features.wonder_0.svg";
    public override float SvgIconSize => 50f;

    public Wonder(HexCoord position) : base(position) { }

    [JsonConstructor]
    public Wonder() : base() { }
}
