using GrpcServer.Models;

namespace GrpcServer.Tests.Models;

/// <summary>
/// <see cref="ProductColors"/> is pure logic with no dependencies, so these tests need
/// no arrange step at all — call and assert. Start a test suite here: the cheapest
/// tests to write are also the ones that break first when someone edits the severity table.
/// </summary>
public class ProductColorsTests
{
    [Theory]
    [InlineData(ProductColors.Green)]
    [InlineData(ProductColors.Orange)]
    [InlineData(ProductColors.Red)]
    public void IsValid_ReturnsTrue_ForKnownColors(string color)
        => Assert.True(ProductColors.IsValid(color));

    [Theory]
    [InlineData("GREEN")]
    [InlineData("Orange")]
    [InlineData("rEd")]
    public void IsValid_IgnoresCase(string color)
        => Assert.True(ProductColors.IsValid(color));

    [Theory]
    [InlineData("purple")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsValid_ReturnsFalse_ForUnknownOrMissingColors(string? color)
        => Assert.False(ProductColors.IsValid(color!));

    [Theory]
    [InlineData(ProductColors.Green, 0)]
    [InlineData(ProductColors.Orange, 1)]
    [InlineData(ProductColors.Red, 2)]
    public void Rank_OrdersColorsBySeverity(string color, int expectedRank)
        => Assert.Equal(expectedRank, ProductColors.Rank(color));

    [Fact]
    public void Rank_PutsUnknownColorsAboveNormalButBelowRed()
    {
        var unknown = ProductColors.Rank("purple");

        Assert.True(unknown > ProductColors.Rank(ProductColors.Green));
        Assert.True(unknown < ProductColors.Rank(ProductColors.Red));
    }

    [Fact]
    public void Rank_TreatsNullAsTheDefaultColor()
        => Assert.Equal(ProductColors.Rank(ProductColors.Default), ProductColors.Rank(null!));

    [Fact]
    public void Default_IsGreen()
        => Assert.Equal(ProductColors.Green, ProductColors.Default);

    [Fact]
    public void All_ExposesEveryKnownColor()
        => Assert.Equal(
            new[] { ProductColors.Green, ProductColors.Orange, ProductColors.Red }.Order(),
            ProductColors.All.Order());
}
