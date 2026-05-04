using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services.Export;

namespace FlipKit.Core.Tests.Services;

public class ExportValidatorTests
{
    private static ExportValidator CreateValidator() => new(new WhatnotValuesProvider());

    private static Card MinimalValidWhatnotCard() => new()
    {
        PlayerName = "Mike Trout",
        ListingPrice = 10m,
        WhatnotCategory = "Sports Cards",
        WhatnotSubcategory = "Baseball Singles",
        Quantity = 1,
        ImageUrl1 = "https://i.ibb.co/xyz/card.jpg",
        ShippingProfile = "1-3 oz",
        Sport = Sport.Baseball,
    };

    // === Whatnot validation ===

    [Fact]
    public void Should_PassValidation_When_AllRequiredWhatnotFieldsArePresent()
    {
        var v = CreateValidator();
        var errors = v.ValidateForWhatnot(new[] { MinimalValidWhatnotCard() });
        Assert.Empty(errors);
    }

    [Fact]
    public void Should_FlagMissingPlayerName_When_ValidatingForWhatnot()
    {
        var v = CreateValidator();
        var card = MinimalValidWhatnotCard();
        card.PlayerName = "";
        var errors = v.ValidateForWhatnot(new[] { card });
        Assert.Contains(errors, e => e.Field == nameof(Card.PlayerName));
    }

    [Fact]
    public void Should_FlagNonPositiveListingPrice_When_ValidatingForWhatnot()
    {
        var v = CreateValidator();
        var card = MinimalValidWhatnotCard();
        card.ListingPrice = 0m;
        var errors = v.ValidateForWhatnot(new[] { card });
        Assert.Contains(errors, e => e.Field == nameof(Card.ListingPrice));
    }

    [Fact]
    public void Should_FlagInvalidCategory_When_CategoryIsNotInWhatnotList()
    {
        var v = CreateValidator();
        var card = MinimalValidWhatnotCard();
        card.WhatnotCategory = "Fictional Category";
        var errors = v.ValidateForWhatnot(new[] { card });
        Assert.Contains(errors, e => e.Field == nameof(Card.WhatnotCategory));
    }

    [Fact]
    public void Should_FlagMissingSubcategoryWithExamples_When_CategoryRequiresOne()
    {
        var v = CreateValidator();
        var card = MinimalValidWhatnotCard();
        card.WhatnotSubcategory = null;
        var errors = v.ValidateForWhatnot(new[] { card });
        var subErr = Assert.Single(errors, e => e.Field == nameof(Card.WhatnotSubcategory));
        // The error message should include "Examples:" with concrete examples for the user.
        Assert.Contains("Examples:", subErr.Message);
    }

    [Fact]
    public void Should_FlagInvalidSubcategory_When_NotInListForCategory()
    {
        var v = CreateValidator();
        var card = MinimalValidWhatnotCard();
        card.WhatnotSubcategory = "Made Up Subcategory";
        var errors = v.ValidateForWhatnot(new[] { card });
        Assert.Contains(errors, e => e.Field == nameof(Card.WhatnotSubcategory));
    }

    [Fact]
    public void Should_FlagQuantityBelowOne_When_ValidatingForWhatnot()
    {
        var v = CreateValidator();
        var card = MinimalValidWhatnotCard();
        card.Quantity = 0;
        var errors = v.ValidateForWhatnot(new[] { card });
        Assert.Contains(errors, e => e.Field == nameof(Card.Quantity));
    }

    [Fact]
    public void Should_FlagMissingFirstImageUrl_When_NoneUploaded()
    {
        var v = CreateValidator();
        var card = MinimalValidWhatnotCard();
        card.ImageUrl1 = null;
        var errors = v.ValidateForWhatnot(new[] { card });
        Assert.Contains(errors, e => e.Field == nameof(Card.ImageUrl1));
    }

    [Fact]
    public void Should_FlagNonHttpsImageUrl_When_ProtocolIsHttp()
    {
        var v = CreateValidator();
        var card = MinimalValidWhatnotCard();
        card.ImageUrl1 = "http://insecure.example.com/card.jpg";
        var errors = v.ValidateForWhatnot(new[] { card });
        Assert.Contains(errors, e => e.Field == "ImageUrl1");
    }

    [Fact]
    public void Should_EmitWarningButNotError_When_ShippingProfileIsCustomNonWeight()
    {
        var v = CreateValidator();
        var card = MinimalValidWhatnotCard();
        card.ShippingProfile = "MyCustomProfileName";
        var errors = v.ValidateForWhatnot(new[] { card });
        // Custom profiles get a Warning so they don't block export.
        var shipErr = Assert.Single(errors, e => e.Field == nameof(Card.ShippingProfile));
        Assert.Equal(ExportErrorSeverity.Warning, shipErr.Severity);
    }

    [Fact]
    public void Should_NotWarn_When_ShippingProfileLooksLikeAWeightString()
    {
        // "8 oz" isn't a known bucket but is a weight the normalizer can handle —
        // suppress the custom-profile warning per LooksLikeWeightString check.
        var v = CreateValidator();
        var card = MinimalValidWhatnotCard();
        card.ShippingProfile = "8 oz";
        var errors = v.ValidateForWhatnot(new[] { card });
        Assert.DoesNotContain(errors, e => e.Field == nameof(Card.ShippingProfile));
    }

    // === eBay validation ===

    [Fact]
    public void Should_FlagMissingSport_When_ValidatingForEbay()
    {
        // eBay's *C:Sport item-specific is required for the Sports Trading Cards category.
        var v = CreateValidator();
        var card = MinimalValidWhatnotCard();
        card.Sport = null;
        var errors = v.ValidateForEbay(new[] { card });
        Assert.Contains(errors, e => e.Field == nameof(Card.Sport));
    }

    [Fact]
    public void Should_FlagSpaceInImageUrl_When_ValidatingForEbay()
    {
        var v = CreateValidator();
        var card = MinimalValidWhatnotCard();
        card.ImageUrl1 = "https://example.com/my card.jpg";
        var errors = v.ValidateForEbay(new[] { card });
        Assert.Contains(errors, e => e.Field == "ImageUrl1" && e.Message.Contains("space"));
    }

    [Fact]
    public void Should_RequireGraderCompanyAndGrade_When_CardIsGraded()
    {
        var v = CreateValidator();
        var card = MinimalValidWhatnotCard();
        card.IsGraded = true;
        // Both GradeCompany and GradeValue intentionally omitted.
        var errors = v.ValidateForEbay(new[] { card });
        Assert.Contains(errors, e => e.Field == nameof(Card.GradeCompany));
        Assert.Contains(errors, e => e.Field == nameof(Card.GradeValue));
    }
}
