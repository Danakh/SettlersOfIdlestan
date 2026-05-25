namespace SettlersOfIdlestan.Model.Game
{
    public enum IslandFeatureType
    {
        Bandit,
        TreasureTrove
    }

    public enum IslandFeaturePlacement
    {
        CloseToPlayer,
        FarFromPlayer,
        Random
    }

    public class IslandFeature
    {
        public IslandFeatureType Type { get; set; }
        public IslandFeaturePlacement Placement { get; set; }

        public IslandFeature(IslandFeatureType type, IslandFeaturePlacement placement)
        {
            Type = type;
            Placement = placement;
        }
    }
}
