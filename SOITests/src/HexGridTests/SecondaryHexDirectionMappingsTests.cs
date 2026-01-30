using Xunit;
using SettlersOfIdlestan.Model.HexGrid;

namespace SOITests.HexGridTests;

public class SecondaryHexDirectionMappingsTests
{
    [Theory]
    [InlineData(SecondaryHexDirection.N, HexDirection.NW, HexDirection.NE)]
    [InlineData(SecondaryHexDirection.EN, HexDirection.NE, HexDirection.E)]
    [InlineData(SecondaryHexDirection.ES, HexDirection.E, HexDirection.SE)]
    [InlineData(SecondaryHexDirection.S, HexDirection.SE, HexDirection.SW)]
    [InlineData(SecondaryHexDirection.WS, HexDirection.SW, HexDirection.W)]
    [InlineData(SecondaryHexDirection.WN, HexDirection.W, HexDirection.NW)]
    public void SecondaryToMainDirectionPairs_ReturnsCorrectPairs(SecondaryHexDirection secondary, HexDirection expected1, HexDirection expected2)
    {
        var (dir1, dir2) = SecondaryHexDirectionMappings.SecondaryToMainDirectionPairs[secondary];
        Assert.Equal(expected1, dir1);
        Assert.Equal(expected2, dir2);
    }
}