using Xunit;

namespace SOITests.IslandMapTests.FullIslandTest
{
    /// <summary>
    /// All FullIslandTest classes write to the same saves/current folder, so they must run
    /// sequentially rather than in parallel (xunit parallelizes across collections by default).
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class FullIslandTestCollection
    {
        public const string Name = "FullIslandTest";
    }
}
