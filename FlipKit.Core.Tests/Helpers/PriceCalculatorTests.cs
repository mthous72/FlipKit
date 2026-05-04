using FlipKit.Core.Helpers;

namespace FlipKit.Core.Tests.Helpers;

public class PriceCalculatorTests
{
    // CalculateNet: salePrice * (1 - feePercent/100) - 0.30, clamped at 0.

    [Fact]
    public void Should_SubtractElevenPercentAndThirtyCents_When_CalculatingNetWithDefaultFee()
    {
        var net = PriceCalculator.CalculateNet(100m);
        Assert.Equal(88.70m, net);
    }

    [Fact]
    public void Should_UseProvidedRate_When_CalculatingNetWithCustomFee()
    {
        var net = PriceCalculator.CalculateNet(100m, feePercent: 20m);
        Assert.Equal(79.70m, net);
    }

    [Fact]
    public void Should_ReturnSalePriceMinusFlatFee_When_FeePercentIsZero()
    {
        var net = PriceCalculator.CalculateNet(10m, feePercent: 0m);
        Assert.Equal(9.70m, net);
    }

    [Fact]
    public void Should_ClampAtZero_When_FlatFeeExceedsRevenue()
    {
        // $0.20 sale - $0.30 flat fee would be negative; should clamp to 0.
        var net = PriceCalculator.CalculateNet(0.20m, feePercent: 0m);
        Assert.Equal(0m, net);
    }

    [Fact]
    public void Should_ClampNetAtZero_When_FeePercentIsOneHundred()
    {
        var net = PriceCalculator.CalculateNet(100m, feePercent: 100m);
        Assert.Equal(0m, net);
    }

    // CalculateBreakEven: rounds up to nearest cent.

    [Fact]
    public void Should_RoundUpToCent_When_CalculatingBreakEvenWithDefaultFee()
    {
        // (10.00 + 0.30) / 0.89 = 11.5730... → ceil to 11.58
        var be = PriceCalculator.CalculateBreakEven(10m);
        Assert.Equal(11.58m, be);
    }

    [Fact]
    public void Should_ReturnMinimumToCoverFlatFee_When_CostBasisIsZero()
    {
        // (0 + 0.30) / 0.89 = 0.3370... → ceil to 0.34
        var be = PriceCalculator.CalculateBreakEven(0m);
        Assert.Equal(0.34m, be);
    }

    [Fact]
    public void Should_ReturnCostBasisUnchanged_When_FeePercentIsOneHundred()
    {
        // feeRate <= 0 path: returns costBasis directly (cannot break even at 100% fee).
        var be = PriceCalculator.CalculateBreakEven(15m, feePercent: 100m);
        Assert.Equal(15m, be);
    }

    // CalculateFees: salePrice * (feePercent/100) + 0.30.

    [Fact]
    public void Should_AddPercentAndFlatFee_When_CalculatingFeesWithDefault()
    {
        var fees = PriceCalculator.CalculateFees(100m);
        Assert.Equal(11.30m, fees);
    }

    [Fact]
    public void Should_ReconstructSalePrice_When_FeesPlusNetAreSummed()
    {
        // Round-trip: fees + net should equal sale price (only when net isn't clamped).
        const decimal sale = 50m;
        var fees = PriceCalculator.CalculateFees(sale);
        var net = PriceCalculator.CalculateNet(sale);
        Assert.Equal(sale, fees + net);
    }
}
