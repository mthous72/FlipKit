using FlipKit.Core.Services.Export;

namespace FlipKit.Core.Tests.Services;

public class ShippingProfileNormalizerTests
{
    private static ShippingProfileNormalizer CreateNormalizer() =>
        new(new WhatnotValuesProvider());

    // NormalizeForWhatnot: pass-through valid buckets, normalize legacy weight strings.

    [Fact]
    public void Should_DefaultToSmallestBucket_When_InputIsNullOrWhitespace()
    {
        var n = CreateNormalizer();
        Assert.Equal("0-1 oz", n.NormalizeForWhatnot(null));
        Assert.Equal("0-1 oz", n.NormalizeForWhatnot("  "));
    }

    [Fact]
    public void Should_PassThroughKnownBucket_When_InputAlreadyMatchesAValidProfile()
    {
        var n = CreateNormalizer();
        Assert.Equal("4-7 oz", n.NormalizeForWhatnot("4-7 oz"));
    }

    [Fact]
    public void Should_BucketByOunces_When_InputIsLegacyOzString()
    {
        var n = CreateNormalizer();
        // "4 oz" → falls into the 4-7 oz bucket per BucketByOunces.
        Assert.Equal("4-7 oz", n.NormalizeForWhatnot("4 oz"));
    }

    [Fact]
    public void Should_BucketByPounds_When_InputIsLegacyLbsString()
    {
        var n = CreateNormalizer();
        // "2 lbs" → exactly 2, hits "1-2 lbs" bucket.
        Assert.Equal("1-2 lbs", n.NormalizeForWhatnot("2 lbs"));
    }

    [Fact]
    public void Should_ConvertGramsToOunces_When_InputIsGrams()
    {
        var n = CreateNormalizer();
        // 100g ≈ 3.527 oz → "4-7 oz" bucket.
        Assert.Equal("4-7 oz", n.NormalizeForWhatnot("100 grams"));
    }

    [Fact]
    public void Should_PassThroughUnrecognizedString_When_InputDoesntMatchAnyPattern()
    {
        var n = CreateNormalizer();
        // A custom seller profile name shouldn't be force-bucketed — pass through and let
        // the validator emit a warning.
        Assert.Equal("My Custom Profile", n.NormalizeForWhatnot("My Custom Profile"));
    }

    // ResolveEbayShipping: weight → service + cost + type.

    [Fact]
    public void Should_ReturnCalculatedShipping_When_ProfileHasUnknownWeight()
    {
        var n = CreateNormalizer();
        // Custom profile name → ApproximateOuncesFromProfile returns null → Calculated.
        var (svc, cost, type) = n.ResolveEbayShipping("Some Custom Whatnot Profile");
        Assert.Equal("USPSGroundAdvantage", svc);
        Assert.Equal(4.50m, cost);
        Assert.Equal("Calculated", type);
    }

    [Fact]
    public void Should_PickFirstClass_When_WeightIsUnderThreeOunces()
    {
        var n = CreateNormalizer();
        // "0-1 oz" bucket → 1 oz upper bound → ≤3 oz arm of the cost ladder.
        var (svc, cost, type) = n.ResolveEbayShipping("0-1 oz");
        Assert.Equal("USPSFirstClass", svc);
        Assert.Equal(1.00m, cost);
        Assert.Equal("Flat", type);
    }

    [Fact]
    public void Should_PickGroundAdvantage_When_WeightIsBetweenEightAndFifteenOunces()
    {
        var n = CreateNormalizer();
        // "12-15 oz" bucket → 15 oz upper bound → ≤15 oz arm.
        var (svc, cost, type) = n.ResolveEbayShipping("12-15 oz");
        Assert.Equal("USPSGroundAdvantage", svc);
        Assert.Equal(5.50m, cost);
        Assert.Equal("Flat", type);
    }

    [Fact]
    public void Should_RecognizeSportsSinglesProfile_When_ResolvingEbayShipping()
    {
        // Whatnot's "Sports singles (3oz)" custom profile name needs to map to flat-rate.
        var n = CreateNormalizer();
        var (svc, cost, type) = n.ResolveEbayShipping("Sports singles (3oz)");
        Assert.Equal("USPSFirstClass", svc);
        Assert.Equal(1.00m, cost);
        Assert.Equal("Flat", type);
    }

    [Fact]
    public void Should_PickHighestTier_When_WeightExceedsThreePounds()
    {
        var n = CreateNormalizer();
        // "10-14 lbs" bucket → 224 oz upper bound → falls through to >48 oz arm.
        var (svc, cost, type) = n.ResolveEbayShipping("10-14 lbs");
        Assert.Equal("USPSGroundAdvantage", svc);
        Assert.Equal(15.00m, cost);
        Assert.Equal("Flat", type);
    }
}
