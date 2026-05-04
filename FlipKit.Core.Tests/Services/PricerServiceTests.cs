using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using NSubstitute;

namespace FlipKit.Core.Tests.Services;

public class PricerServiceTests
{
    private static ISettingsService Settings(AppSettings? overrides = null)
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(overrides ?? new AppSettings());
        return settings;
    }

    private static Card SampleCard() => new()
    {
        Year = 2026,
        PlayerName = "Mike Trout",
        Sport = Sport.Baseball,
        Brand = "Bowman",
        Team = "Angels",
        ParallelName = "Refractor",
        IsRookie = true,
    };

    // === URL builders ===

    [Fact]
    public void Should_BuildTerapeakUrlWithEscapedQuery_When_Card()
    {
        var svc = new PricerService(Settings());

        var url = svc.BuildTerapeakUrl(SampleCard());

        Assert.StartsWith("https://www.ebay.com/sh/research", url);
        Assert.Contains("tabName=SOLD", url);
        Assert.Contains("Mike%20Trout", url);
    }

    [Fact]
    public void Should_BuildSmartEbayUrl_When_SmartQueryIsEnabled()
    {
        // Smart query is on by default per AppSettings.UseSmartEbayQuery = true.
        var svc = new PricerService(Settings());

        var url = svc.BuildEbaySoldUrl(SampleCard());

        Assert.Contains("ebay.com/sch/i.html", url);
        Assert.Contains("LH_Sold=1", url);
        Assert.Contains("261328", url); // Sports Trading Cards leaf category
    }

    [Fact]
    public void Should_BuildTemplateBasedUrl_When_SmartQueryIsDisabled()
    {
        var svc = new PricerService(Settings(new AppSettings { UseSmartEbayQuery = false }));

        var url = svc.BuildEbaySoldUrl(SampleCard());

        Assert.Contains("ebay.com/sch/i.html", url);
        Assert.Contains("Mike%20Trout", url);
    }

    // === Smart query construction ===

    [Fact]
    public void Should_IncludeCorePieces_When_BuildingSmartQuery()
    {
        var svc = new PricerService(Settings());

        var query = svc.BuildSmartEbayQuery(SampleCard());

        Assert.Contains("2026", query);
        Assert.Contains("Mike Trout", query);
        Assert.Contains("Bowman", query);
        Assert.Contains("Angels", query);
        Assert.Contains("Refractor", query);
        Assert.Contains("RC", query); // attribute for IsRookie
    }

    [Fact]
    public void Should_NotDuplicateBrandWhenSetNameMatches_When_BuildingSmartQuery()
    {
        var svc = new PricerService(Settings());
        var card = SampleCard();
        card.SetName = "Bowman"; // same as Brand

        var query = svc.BuildSmartEbayQuery(card);

        // Brand "Bowman" should appear once, not twice.
        var occurrences = query.Split("Bowman").Length - 1;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void Should_OmitBaseParallelAndVariation_When_BuildingSmartQuery()
    {
        var svc = new PricerService(Settings());
        var card = SampleCard();
        card.Sport = Sport.Football; // avoid "Baseball" matching the substring check below
        card.ParallelName = "Base";
        card.VariationType = "Base";

        var query = svc.BuildSmartEbayQuery(card);

        // Token-level check — ParallelName/VariationType "Base" should not appear as a word.
        var tokens = query.Split(' ');
        Assert.DoesNotContain("Base", tokens);
    }

    [Fact]
    public void Should_IncludeGradeInfo_When_CardIsGraded()
    {
        var svc = new PricerService(Settings());
        var card = SampleCard();
        card.IsGraded = true;
        card.GradeCompany = "PSA";
        card.GradeValue = "10";

        var query = svc.BuildSmartEbayQuery(card);

        Assert.Contains("PSA", query);
        Assert.Contains("10", query);
    }

    // === SuggestPrice ladder ===

    [Fact]
    public void Should_DiscountToEightyPercent_When_VariationIsBase()
    {
        var svc = new PricerService(Settings());
        var card = SampleCard();
        card.VariationType = "Base";
        card.IsRookie = false; // remove the rookie boost so we can check the base discount cleanly

        var price = svc.SuggestPrice(estimatedValue: 50m, card);

        // 50 * 0.80 = 40. Falls in the >= 20 bracket → rounded to whole dollar = 40.
        Assert.Equal(40m, price);
    }

    [Fact]
    public void Should_ApplyTighterDiscountForLowSerial_When_SerialIsTen()
    {
        var svc = new PricerService(Settings());
        var card = SampleCard();
        card.VariationType = "Refractor";
        card.IsRookie = false;
        card.SerialNumbered = "/10";

        var price = svc.SuggestPrice(estimatedValue: 100m, card);

        // 100 * 0.95 = 95. >= 100 bucket only fires at >= 100, so 95 is in the >= 20 bucket
        // → rounded to nearest dollar = 95.
        Assert.Equal(95m, price);
    }

    [Fact]
    public void Should_ApplyRookieAndAutoBoosts_When_FlagsAreSet()
    {
        var svc = new PricerService(Settings());
        var card = SampleCard();
        card.VariationType = "Base"; // 0.80
        card.IsRookie = true;        // *1.05
        card.IsAuto = true;          // *1.02

        var price = svc.SuggestPrice(estimatedValue: 100m, card);

        // 100 * 0.80 * 1.05 * 1.02 = 85.68 → rounded (>=20 bucket) to 86.
        Assert.Equal(86m, price);
    }

    [Fact]
    public void Should_ClampToNinetyNineCents_When_PriceWouldBeBelow()
    {
        var svc = new PricerService(Settings());
        var card = SampleCard();
        card.VariationType = "Base";
        card.IsRookie = false;

        var price = svc.SuggestPrice(estimatedValue: 0.50m, card);

        Assert.Equal(0.99m, price);
    }

    [Fact]
    public void Should_DelegateToPriceCalculator_When_CalculatingNet()
    {
        // Thin pass-through to PriceCalculator.CalculateNet — covered exhaustively in
        // PriceCalculatorTests; just verify the delegation works.
        var svc = new PricerService(Settings());

        Assert.Equal(88.70m, svc.CalculateNet(100m));
    }
}
