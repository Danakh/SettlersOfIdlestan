namespace SettlersOfIdlestan.Model.Game
{
    public enum IslandFeatureType
    {
        Bandit,
        TreasureTrove,
        BanditHideout,
        Dragon,
        Rats,
    }

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
