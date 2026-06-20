using Xunit;

namespace SOITests.IslandMapTests.StepIslandTest
{
    /// <summary>
    /// All StepIslandTest classes write to the same saves/current folder, so they must run
    /// sequentially rather than in parallel (xunit parallelizes across collections by default).
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class StepIslandTestCollection
    {
        public const string Name = "StepIslandTest";
    }
}
