using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services.Export;

namespace FlipKit.Core.Tests.Services;

public class EbayExporterTests
{
    private static EbayExporter CreateExporter()
    {
        var shipping = new ShippingProfileNormalizer(new WhatnotValuesProvider());
        return new EbayExporter(shipping);
    }

    private static EbayExportOptions DefaultOptions() => new()
    {
        SellerLocation = "10001",
    };

    private static Card SampleCard() => new()
    {
        Id = 7,
        PlayerName = "Mike Trout",
        Year = 2026,
        Manufacturer = "Topps",
        Brand = "Bowman",
        SetName = "Bowman Chrome",
        CardNumber = "BCP-1",
        Team = "Angels",
        ParallelName = "Refractor",
        Sport = Sport.Baseball,
        ListingPrice = 25.50m,
        Quantity = 1,
        Condition = "Near Mint",
        ShippingProfile = "1-3 oz",
        Sku = "FK-000007",
        ImageUrl1 = "https://i.ibb.co/xyz/front.jpg",
    };

    private static string TitleFor(Card c) => $"Title for {c.PlayerName}";
    private static string DescFor(Card c) => $"Description for {c.PlayerName}";

    // === SerializeRow ===

    [Fact]
    public void Should_SetActionAdd_When_VerifyAddIsFalse()
    {
        var ex = CreateExporter();
        var template = new EbayTemplateProvider();
        var actionCol = template.FindColumnStartingWith("*Action");
        Assert.NotNull(actionCol);

        var row = ex.SerializeRow(SampleCard(), TitleFor, DescFor, DefaultOptions());
        Assert.Equal("Add", row[actionCol!]);
    }

    [Fact]
    public void Should_SetActionVerifyAdd_When_VerifyAddIsTrue()
    {
        var ex = CreateExporter();
        var template = new EbayTemplateProvider();
        var actionCol = template.FindColumnStartingWith("*Action");
        Assert.NotNull(actionCol);

        var opts = DefaultOptions();
        opts.UseVerifyAdd = true;
        var row = ex.SerializeRow(SampleCard(), TitleFor, DescFor, opts);
        Assert.Equal("VerifyAdd", row[actionCol!]);
    }

    [Fact]
    public void Should_FormatStartPriceToTwoDecimals_When_SerializingRow()
    {
        var ex = CreateExporter();
        var card = SampleCard();
        card.ListingPrice = 12m;
        var row = ex.SerializeRow(card, TitleFor, DescFor, DefaultOptions());
        Assert.Equal("12.00", row["*StartPrice"]);
    }

    [Fact]
    public void Should_TruncateTitleToEightyChars_When_TitleIsLong()
    {
        var ex = CreateExporter();
        var longTitle = new string('A', 200);
        var row = ex.SerializeRow(SampleCard(), _ => longTitle, DescFor, DefaultOptions());
        Assert.Equal(80, row["*Title"].Length);
    }

    [Fact]
    public void Should_MapSportToEbayLabel_When_SportIsSet()
    {
        // eBay uses "Ice Hockey" not "Hockey"; verify the mapping.
        var ex = CreateExporter();
        var card = SampleCard();
        card.Sport = Sport.Hockey;
        var row = ex.SerializeRow(card, TitleFor, DescFor, DefaultOptions());
        Assert.Equal("Ice Hockey", row["*C:Sport"]);
        Assert.Equal("NHL", row["C:League"]);
    }

    [Fact]
    public void Should_PopulateGradedFields_When_CardIsGraded()
    {
        var ex = CreateExporter();
        var card = SampleCard();
        card.IsGraded = true;
        card.GradeCompany = "PSA";
        card.GradeValue = "10";
        card.CertNumber = "12345678";
        var row = ex.SerializeRow(card, TitleFor, DescFor, DefaultOptions());
        Assert.Equal("Yes", row["C:Graded"]);
        // C:Professional Grader gets eBay's verbose label, not the raw company code.
        Assert.Equal("Professional Sports Authenticator (PSA)", row["C:Professional Grader"]);
        Assert.Equal("10", row["C:Grade"]);
    }

    [Fact]
    public void Should_LeaveGraderColumnsBlank_When_CardIsNotGraded()
    {
        var ex = CreateExporter();
        var card = SampleCard();
        card.IsGraded = false;
        var row = ex.SerializeRow(card, TitleFor, DescFor, DefaultOptions());
        Assert.Equal("No", row["C:Graded"]);
        Assert.False(row.ContainsKey("C:Professional Grader"));
    }

    [Fact]
    public void Should_BuildFeaturesFromBoolFlags_When_SerializingRow()
    {
        var ex = CreateExporter();
        var card = SampleCard();
        card.IsRookie = true;
        card.IsAuto = true;
        card.IsRelic = false;
        card.SerialNumbered = "/199";
        card.IsShortPrint = true;
        var row = ex.SerializeRow(card, TitleFor, DescFor, DefaultOptions());
        // Features is semicolon-delimited.
        Assert.Contains("Rookie", row["C:Features"]);
        Assert.Contains("Autograph", row["C:Features"]);
        Assert.Contains("Serial Numbered", row["C:Features"]);
        Assert.Contains("Short Print", row["C:Features"]);
        Assert.DoesNotContain("Memorabilia", row["C:Features"]);
    }

    [Fact]
    public void Should_EncodeSpacesInImageUrls_When_BuildingPicURL()
    {
        // PicURL is pipe-delimited and spaces must be %20-encoded per spec §3.8.
        var ex = CreateExporter();
        var card = SampleCard();
        card.ImageUrl1 = "https://example.com/with space.jpg";
        var row = ex.SerializeRow(card, TitleFor, DescFor, DefaultOptions());
        Assert.Equal("https://example.com/with%20space.jpg", row["PicURL"]);
    }

    [Fact]
    public void Should_PipeDelimitMultipleImageUrls_When_BuildingPicURL()
    {
        var ex = CreateExporter();
        var card = SampleCard();
        card.ImageUrl1 = "https://example.com/a.jpg";
        card.ImageUrl2 = "https://example.com/b.jpg";
        var row = ex.SerializeRow(card, TitleFor, DescFor, DefaultOptions());
        Assert.Equal("https://example.com/a.jpg|https://example.com/b.jpg", row["PicURL"]);
    }

    [Fact]
    public void Should_PopulateReturnsBlock_When_ReturnsAccepted()
    {
        var ex = CreateExporter();
        var opts = DefaultOptions();
        opts.ReturnsAccepted = true;
        var row = ex.SerializeRow(SampleCard(), TitleFor, DescFor, opts);
        Assert.Equal("ReturnsAccepted", row["*ReturnsAcceptedOption"]);
        Assert.Equal("Days_30", row["ReturnsWithinOption"]);
    }

    [Fact]
    public void Should_OmitReturnsDetails_When_ReturnsNotAccepted()
    {
        var ex = CreateExporter();
        var opts = DefaultOptions();
        opts.ReturnsAccepted = false;
        var row = ex.SerializeRow(SampleCard(), TitleFor, DescFor, opts);
        Assert.Equal("ReturnsNotAccepted", row["*ReturnsAcceptedOption"]);
        Assert.False(row.ContainsKey("ReturnsWithinOption"));
    }

    [Fact]
    public void Should_DefaultDurationToGTC_When_OptionsDontOverride()
    {
        var ex = CreateExporter();
        var row = ex.SerializeRow(SampleCard(), TitleFor, DescFor, DefaultOptions());
        Assert.Equal("GTC", row["*Duration"]);
        Assert.Equal("FixedPrice", row["*Format"]);
    }

    [Fact]
    public void Should_DefaultCategoryToSportsTradingCards_When_OptionsDontOverride()
    {
        // Sports Trading Cards leaf category is 261328.
        var ex = CreateExporter();
        var row = ex.SerializeRow(SampleCard(), TitleFor, DescFor, DefaultOptions());
        Assert.Equal("261328", row["*Category"]);
    }
}
