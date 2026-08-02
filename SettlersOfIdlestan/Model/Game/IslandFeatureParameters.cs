using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Game
{
    [JsonConverter(typeof(JsonStringEnumConverter<IslandFeatureType>))]
    public enum IslandFeatureType
    {
        Bandit,
        TreasureTrove,
        BanditHideout,
        Dragon,
        Rats,
    }

    [JsonConverter(typeof(JsonStringEnumConverter<IslandFeaturePlacement>))]
    public enum IslandFeaturePlacement
    {
        CloseToPlayer,
        FarFromPlayer,
        FarFromAllCivilization,
        Random
    }

    public class IslandFeatureParameters
    {
        public IslandFeatureType Type { get; set; }
        public IslandFeaturePlacement Placement { get; set; }

        public IslandFeatureParameters(IslandFeatureType type, IslandFeaturePlacement placement)
        {
            Type = type;
            Placement = placement;
        }
    }
}
