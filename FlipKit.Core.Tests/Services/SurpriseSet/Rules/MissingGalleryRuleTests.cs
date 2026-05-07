using System.Collections.Generic;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations.SurpriseSets.Rules;
using Xunit;

namespace FlipKit.Core.Tests.Services.SurpriseSet.Rules;

public class MissingGalleryRuleTests
{
    private static readonly MissingGalleryRule Rule = new();

    [Fact]
    public void Should_ReturnError_When_SharedImageUrl1IsNull()
    {
        var set = new Models.SurpriseSet { SharedImageUrl1 = null };
        var issues = Rule.Check(set, new List<Card>());
        Assert.Contains(issues, i => i.Code == "MISSING_GALLERY" && i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void Should_ReturnError_When_SharedImageUrl1IsWhitespace()
    {
        var set = new Models.SurpriseSet { SharedImageUrl1 = "   " };
        var issues = Rule.Check(set, new List<Card>());
        Assert.Contains(issues, i => i.Code == "MISSING_GALLERY");
    }

    [Fact]
    public void Should_ReturnNoIssues_When_SharedImageUrl1IsProvided()
    {
        var set = new Models.SurpriseSet { SharedImageUrl1 = "https://example.com/gallery.jpg" };
        var issues = Rule.Check(set, new List<Card>());
        Assert.Empty(issues);
    }
}
