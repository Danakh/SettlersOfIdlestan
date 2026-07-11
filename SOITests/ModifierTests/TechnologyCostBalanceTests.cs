using System.Collections.Generic;
using Xunit;
using SettlersOfIdlestan.Model.Civilization;

namespace SOITests.ModifierTests;

/// <summary>
/// Enforces the tech-tree cost curve: every technology's cost must stay within ±25%
/// of 100 * 4^tier, so a future tier bump (or new technology) can't silently drift
/// out of the intended progression again.
/// </summary>
public class TechnologyCostBalanceTests
{
    public static IEnumerable<object[]> AllTechnologies()
    {
        foreach (var tech in TechnologyDefinitions.All)
            yield return new object[] { tech };
    }

    [Theory]
    [MemberData(nameof(AllTechnologies))]
    public void Cost_IsWithin25PercentOf_100Times4PowTier(Technology tech)
    {
        double expected = 100.0 * System.Math.Pow(4, tech.Tier);
        double ratio = tech.Cost / expected;

        Assert.True(ratio is >= 0.75 and <= 1.25,
            $"{tech.Id} (tier {tech.Tier}): cost {tech.Cost} is {ratio:P0} of the expected {expected:N0} (100*4^{tech.Tier}), outside the ±25% band.");
    }
}
