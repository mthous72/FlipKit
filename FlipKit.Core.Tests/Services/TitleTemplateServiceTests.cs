using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;

namespace FlipKit.Core.Tests.Services;

public class TitleTemplateServiceTests
{
    private static Card SampleCard() => new()
    {
        Year = 2026,
        Manufacturer = "Topps",
        Brand = "Bowman",
        PlayerName = "Mike Trout",
        Team = "Angels",
        ParallelName = "Refractor",
        SerialNumbered = "/199",
        CardNumber = "BCP-1",
        IsRookie = true,
        IsAuto = true,
    };

    // GenerateTitle: substitutes placeholders, collapses whitespace, falls back when empty.

    [Fact]
    public void Should_SubstituteAllKnownPlaceholders_When_GeneratingTitle()
    {
        var svc = new TitleTemplateService();
        var title = svc.GenerateTitle(SampleCard(), "{Year} {Brand} {Player} {Parallel}");
        Assert.Equal("2026 Bowman Mike Trout Refractor", title);
    }

    [Fact]
    public void Should_BeCaseInsensitive_When_MatchingPlaceholders()
    {
        // Per regex flag: `{year}` and `{Year}` should both substitute.
        var svc = new TitleTemplateService();
        var title = svc.GenerateTitle(SampleCard(), "{year} {brand}");
        Assert.Equal("2026 Bowman", title);
    }

    [Fact]
    public void Should_CollapseAndTrimWhitespace_When_PlaceholdersResolveToEmpty()
    {
        // {Team} for a card with no Team should leave a gap that gets collapsed.
        var svc = new TitleTemplateService();
        var card = SampleCard();
        card.Team = null;
        var title = svc.GenerateTitle(card, "{Year}  {Team}  {Brand}");
        Assert.Equal("2026 Bowman", title);
    }

    [Fact]
    public void Should_BuildAttributesFromBoolFlags_When_UsingAttributesPlaceholder()
    {
        var svc = new TitleTemplateService();
        var title = svc.GenerateTitle(SampleCard(), "{Player} {Attributes}");
        // IsRookie + IsAuto both set; IsRelic not.
        Assert.Equal("Mike Trout RC Auto", title);
    }

    [Fact]
    public void Should_BuildGradeWhenGraded_When_UsingGradePlaceholder()
    {
        var svc = new TitleTemplateService();
        var card = SampleCard();
        card.IsGraded = true;
        card.GradeCompany = "PSA";
        card.GradeValue = "10";
        var title = svc.GenerateTitle(card, "{Player} {Grade}");
        Assert.Equal("Mike Trout PSA 10", title);
    }

    [Fact]
    public void Should_OmitGrade_When_NotGraded()
    {
        var svc = new TitleTemplateService();
        var card = SampleCard();
        card.IsGraded = false;
        var title = svc.GenerateTitle(card, "{Player} {Grade}");
        Assert.Equal("Mike Trout", title);
    }

    [Fact]
    public void Should_PrefixCardNumberWithHash_When_UsingCardNumberPlaceholder()
    {
        var svc = new TitleTemplateService();
        var title = svc.GenerateTitle(SampleCard(), "{Player} {CardNumber}");
        Assert.Equal("Mike Trout #BCP-1", title);
    }

    [Fact]
    public void Should_LeaveUnknownPlaceholdersLiteral_When_NotInReplacementMap()
    {
        // Unknown placeholders aren't matched by GetReplacements — they pass through verbatim.
        var svc = new TitleTemplateService();
        var title = svc.GenerateTitle(SampleCard(), "{Player} {NotARealField}");
        Assert.Equal("Mike Trout {NotARealField}", title);
    }

    [Fact]
    public void Should_UseFallbackTitle_When_TemplateIsNullOrEmpty()
    {
        var svc = new TitleTemplateService();
        var title = svc.GenerateTitle(SampleCard(), "");
        // Fallback joins all populated parts with spaces.
        Assert.Contains("2026", title);
        Assert.Contains("Mike Trout", title);
        Assert.Contains("Refractor", title);
    }

    [Fact]
    public void Should_UseFallbackTitle_When_TemplateProducesEmptyResult()
    {
        // Template containing only unknown placeholders + whitespace → empty after collapse → fallback.
        // (Actually — unknown placeholders pass through verbatim per behavior tested above. So
        // this fallback path triggers when the template is just whitespace/empty post-substitution.)
        var svc = new TitleTemplateService();
        var card = new Card { PlayerName = "Anonymous", Year = 2026 };
        var title = svc.GenerateTitle(card, "{Team} {Parallel}"); // both empty for this card
        // Both placeholders resolve to empty, leaving only whitespace → triggers fallback.
        Assert.Contains("Anonymous", title);
    }

    // GetDefaultTemplate: per-platform SEO templates.

    [Fact]
    public void Should_ReturnDifferentTemplatesPerPlatform_When_GettingDefaults()
    {
        var whatnot = TitleTemplateService.GetDefaultTemplate(ExportPlatform.Whatnot);
        var ebay = TitleTemplateService.GetDefaultTemplate(ExportPlatform.eBay);
        var comc = TitleTemplateService.GetDefaultTemplate(ExportPlatform.COMC);
        var generic = TitleTemplateService.GetDefaultTemplate(ExportPlatform.Generic);

        // eBay includes Manufacturer; the others typically don't.
        Assert.Contains("{Manufacturer}", ebay);
        Assert.DoesNotContain("{Manufacturer}", whatnot);
        Assert.DoesNotContain("{Manufacturer}", comc);
        Assert.DoesNotContain("{Manufacturer}", generic);
    }

    // ValidateTemplate: catches empty + unknown placeholders.

    [Fact]
    public void Should_RejectEmptyTemplate_When_Validating()
    {
        var (ok, err) = TitleTemplateService.ValidateTemplate("");
        Assert.False(ok);
        Assert.NotNull(err);
    }

    [Fact]
    public void Should_RejectUnknownPlaceholder_When_Validating()
    {
        var (ok, err) = TitleTemplateService.ValidateTemplate("{Player} {NotAField}");
        Assert.False(ok);
        Assert.Contains("NotAField", err);
    }

    [Fact]
    public void Should_AcceptKnownPlaceholders_When_Validating()
    {
        var (ok, err) = TitleTemplateService.ValidateTemplate("{Year} {Brand} {Player}");
        Assert.True(ok);
        Assert.Null(err);
    }
}
