using System;
using Xunit;
using SettlersOfIdlestan.Model.HexGrid;

namespace SOITests.HexGridTests;

public class SecondaryHexDirectionTests
{
    [Fact]
    public void AllSecondaryDirections_ContainsAllDirections()
    {
        var expected = new[] { SecondaryHexDirection.N, SecondaryHexDirection.EN, SecondaryHexDirection.ES, SecondaryHexDirection.S, SecondaryHexDirection.WS, SecondaryHexDirection.WN };
        Assert.Equal(expected, SecondaryHexDirectionUtils.AllSecondaryDirections);
    }

    [Theory]
    [InlineData(SecondaryHexDirection.N, SecondaryHexDirection.S)]
    [InlineData(SecondaryHexDirection.S, SecondaryHexDirection.N)]
    [InlineData(SecondaryHexDirection.EN, SecondaryHexDirection.WS)]
    [InlineData(SecondaryHexDirection.WS, SecondaryHexDirection.EN)]
    [InlineData(SecondaryHexDirection.ES, SecondaryHexDirection.WN)]
    [InlineData(SecondaryHexDirection.WN, SecondaryHexDirection.ES)]
    public void InverseSecondaryHexDirection_ReturnsCorrectInverse(SecondaryHexDirection direction, SecondaryHexDirection expected)
    {
        var result = SecondaryHexDirectionUtils.InverseSecondaryHexDirection(direction);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void InverseSecondaryHexDirection_InvalidDirection_ThrowsArgumentOutOfRangeException()
    {
        var invalidDirection = (SecondaryHexDirection)999;
        Assert.Throws<ArgumentOutOfRangeException>(() => SecondaryHexDirectionUtils.InverseSecondaryHexDirection(invalidDirection));
    }

    [Theory]
    [InlineData(SecondaryHexDirection.N, HexDirection.NW, HexDirection.NE)]
    [InlineData(SecondaryHexDirection.EN, HexDirection.NE, HexDirection.E)]
    [InlineData(SecondaryHexDirection.ES, HexDirection.E, HexDirection.SE)]
    [InlineData(SecondaryHexDirection.S, HexDirection.SE, HexDirection.SW)]
    [InlineData(SecondaryHexDirection.WS, HexDirection.SW, HexDirection.W)]
    [InlineData(SecondaryHexDirection.WN, HexDirection.W, HexDirection.NW)]
    public void SecondaryToMainDirectionPairs_ReturnsCorrectPairs(SecondaryHexDirection secondary, HexDirection expected1, HexDirection expected2)
    {
        var (dir1, dir2) = SecondaryHexDirectionUtils.GetMainDirectionPair(secondary);
        Assert.Equal(expected1, dir1);
        Assert.Equal(expected2, dir2);
    }
}