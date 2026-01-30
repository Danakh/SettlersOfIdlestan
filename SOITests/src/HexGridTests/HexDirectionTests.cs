using System;
using Xunit;
using SettlersOfIdlestan.Model.HexGrid;

namespace SOITests.HexGridTests;

public class HexDirectionTests
{
    [Fact]
    public void AllHexDirections_ContainsAllDirections()
    {
        var expected = new[] { HexDirection.W, HexDirection.E, HexDirection.NE, HexDirection.SE, HexDirection.NW, HexDirection.SW };
        Assert.Equal(expected, HexDirectionUtils.AllHexDirections);
    }

    [Theory]
    [InlineData(HexDirection.W, HexDirection.E)]
    [InlineData(HexDirection.E, HexDirection.W)]
    [InlineData(HexDirection.NE, HexDirection.SW)]
    [InlineData(HexDirection.SW, HexDirection.NE)]
    [InlineData(HexDirection.NW, HexDirection.SE)]
    [InlineData(HexDirection.SE, HexDirection.NW)]
    public void InverseHexDirection_ReturnsCorrectInverse(HexDirection direction, HexDirection expected)
    {
        var result = HexDirectionUtils.InverseHexDirection(direction);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void InverseHexDirection_InvalidDirection_ThrowsArgumentOutOfRangeException()
    {
        var invalidDirection = (HexDirection)999;
        Assert.Throws<ArgumentOutOfRangeException>(() => HexDirectionUtils.InverseHexDirection(invalidDirection));
    }
}