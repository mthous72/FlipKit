using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services.Export;

namespace FlipKit.Core.Tests.Services;

public class WhatnotExporterTests
{
    private static WhatnotExporter CreateExporter()
    {
        var whatnot = new WhatnotValuesProvider();
        var shipping = new ShippingProfileNormalizer(whatnot);
        return new WhatnotExporter(whatnot, shipping);
    }

    private static Card SampleCard() => new()
    {
        Id = 42,
        PlayerName = "Mike Trout",
        ListingPrice = 12.99m,
        Quantity = 1,
        WhatnotCategory = "Sports Cards",
        WhatnotSubcategory = "Baseball Singles",
        ShippingProfile = "1-3 oz",
        Condition = "Near Mint",
        Sport = Sport.Baseball,
        ImageUrl1 = "https://i.ibb.co/xyz/front.jpg",
        Sku = "FK-000042",
        Offerable = true,
        ListingType = "Buy It Now",
        CostBasis = 4.50m,
    };

    private static string TitleFor(Card c) => $"Title for {c.PlayerName}";
    private static string DescFor(Card c) => $"Description for {c.PlayerName}";

    // === SerializeRow — pure transformation ===

    [Fact]
    public void Should_RoundPriceUpToInteger_When_SerializingRow()
    {
        // Spec §2.4 #2: Whatnot price must be a positive integer with no decimals.
        var ex = CreateExporter();
        var card = SampleCard();
        card.ListingPrice = 12.50m;
        var row = ex.SerializeRow(card, TitleFor, DescFor, new WhatnotExportOptions());
        Assert.Equal("13", row["Price"]); // away-from-zero rounding
    }

    [Fact]
    public void Should_ClampPriceToOne_When_ListingPriceIsZeroOrMissing()
    {
        var ex = CreateExporter();
        var card = SampleCard();
        card.ListingPrice = 0m;
        var row = ex.SerializeRow(card, TitleFor, DescFor, new WhatnotExportOptions());
        Assert.Equal("1", row["Price"]); // exporter clamps; validator should already have flagged
    }

    [Fact]
    public void Should_TruncateTitleToEightyChars_When_TitleIsLong()
    {
        // Spec §2.4 #7: Whatnot title hard-capped at 80 characters.
        var ex = CreateExporter();
        var longTitle = new string('A', 200);
        var row = ex.SerializeRow(
            SampleCard(),
            _ => longTitle,
            DescFor,
            new WhatnotExportOptions());
        Assert.Equal(80, row["Title"].Length);
    }

    [Fact]
    public void Should_NormalizeListingTypeToLowercaseIt_When_CardHasUppercaseListingType()
    {
        // Card model defaults to "Buy It Now" but Whatnot's enum is "Buy it Now" (lowercase 'it').
        var ex = CreateExporter();
        var card = SampleCard();
        card.ListingType = "Buy It Now";
        var row = ex.SerializeRow(card, TitleFor, DescFor, new WhatnotExportOptions());
        Assert.Equal("Buy it Now", row["Type"]);
    }

    [Fact]
    public void Should_BlankOfferableField_When_ListingTypeIsAuction()
    {
        // Offerable is meaningful only for Buy it Now per spec §2.3.
        var ex = CreateExporter();
        var row = ex.SerializeRow(
            SampleCard(),
            TitleFor,
            DescFor,
            new WhatnotExportOptions { DefaultListingType = "Auction" });
        Assert.Equal(string.Empty, row["Offerable"]);
        Assert.Equal("Auction", row["Type"]);
    }

    [Fact]
    public void Should_EmitTrueOrFalseForOfferable_When_ListingTypeIsBuyItNow()
    {
        var ex = CreateExporter();
        var card = SampleCard();
        card.Offerable = false;
        var row = ex.SerializeRow(card, TitleFor, DescFor, new WhatnotExportOptions());
        Assert.Equal("FALSE", row["Offerable"]);
    }

    [Fact]
    public void Should_PassThroughKnownShippingBucket_When_SerializingRow()
    {
        var ex = CreateExporter();
        var card = SampleCard();
        card.ShippingProfile = "4-7 oz";
        var row = ex.SerializeRow(card, TitleFor, DescFor, new WhatnotExportOptions());
        Assert.Equal("4-7 oz", row["Shipping Profile"]);
    }

    [Fact]
    public void Should_FormatCostBasisToTwoDecimals_When_Present()
    {
        var ex = CreateExporter();
        var card = SampleCard();
        card.CostBasis = 4.5m;
        var row = ex.SerializeRow(card, TitleFor, DescFor, new WhatnotExportOptions());
        Assert.Equal("4.50", row["Cost Per Item"]);
    }

    [Fact]
    public void Should_LeaveCostBasisBlank_When_NotSet()
    {
        var ex = CreateExporter();
        var card = SampleCard();
        card.CostBasis = null;
        var row = ex.SerializeRow(card, TitleFor, DescFor, new WhatnotExportOptions());
        Assert.Equal(string.Empty, row["Cost Per Item"]);
    }

    [Fact]
    public void Should_PopulateAllImageUrlSlots_When_SomeAreEmpty()
    {
        // The 8 Image URL columns must always be present, blank when no URL exists.
        var ex = CreateExporter();
        var card = SampleCard();
        card.ImageUrl1 = "https://i.ibb.co/xyz/front.jpg";
        card.ImageUrl2 = null;
        var row = ex.SerializeRow(card, TitleFor, DescFor, new WhatnotExportOptions());
        Assert.Equal("https://i.ibb.co/xyz/front.jpg", row["Image URL 1"]);
        Assert.Equal(string.Empty, row["Image URL 2"]);
        for (int i = 3; i <= 8; i++)
            Assert.True(row.ContainsKey($"Image URL {i}"));
    }

    [Fact]
    public void Should_DefaultHazmatToNotHazmat_When_SerializingRow()
    {
        // We never emit a Hazmat value other than "Not Hazmat" — sports cards never apply.
        var ex = CreateExporter();
        var row = ex.SerializeRow(SampleCard(), TitleFor, DescFor, new WhatnotExportOptions());
        Assert.Equal("Not Hazmat", row["Hazmat"]);
    }
}
