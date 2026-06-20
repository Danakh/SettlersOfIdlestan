using SOITests.IslandMapTests.StepIslandTest;
using Xunit;

namespace SOITests.IslandMapTests.FullIslandTest
{
    /// <summary>
    /// One Fact per prestige cycle — each runs StepIslandScenarios' own (already-tuned) per-stage logic
    /// continuously in memory, from one prestige to the next, with no intermediate save/reload/assert
    /// checkpoint. See FullIslandScenarios for the chaining. Shares StepIslandTest's collection (and so
    /// never runs in parallel with it) since these read the saves/current files it produces.
    /// </summary>
    [Collection(StepIslandTestCollection.Name)]
    public class FullIslandCurrentTests
    {
        [Fact]
        public void Current_Island1_Prestige() => FullIslandScenarios.RunIsland1("current");

        [Fact]
        public void Current_Island2_Prestige() => FullIslandScenarios.RunIsland2("current");

        [Fact]
        public void Current_Island3_Prestige() => FullIslandScenarios.RunIsland3("current");

        [Fact]
        public void Current_Island4_Prestige() => FullIslandScenarios.RunIsland4("current");
    }
}
