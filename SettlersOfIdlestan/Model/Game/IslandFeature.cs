namespace SettlersOfIdlestan.Model.Game
{
    public enum IslandFeaturePlacement
    {
        CloseToPlayer,
        FarFromPlayer,
        Random
    }

    public class IslandFeature
    {
        public int BanditCount { get; set; }
        public IslandFeaturePlacement Placement { get; set; }

        public IslandFeature(int banditCount, IslandFeaturePlacement placement)
        {
            BanditCount = banditCount;
            Placement = placement;
        }
    }
}
