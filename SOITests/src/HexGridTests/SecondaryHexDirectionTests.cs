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
}