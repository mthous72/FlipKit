using System.Collections.Generic;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations.SurpriseSets.Rules;
using Xunit;

namespace FlipKit.Core.Tests.Services.SurpriseSet.Rules;

public class MixedProductTypeRuleTests
{
    private static readonly MixedProductTypeRule Rule = new();
    private static Models.SurpriseSet Set() => new();

    [Fact]
    public void Should_ReturnError_When_SetHasBothGradedAndRawCards()
    {
        var cards = new List<Card>
        {
            new() { IsGraded = true },
            new() { IsGraded = false },
        };
        var issues = Rule.Check(Set(), cards);
        Assert.Contains(issues, i => i.Code == "MIXED_PRODUCT" && i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void Should_ReturnNoIssues_When_AllCardsAreGraded()
    {
        var cards = new List<Card> { new() { IsGraded = true }, new() { IsGraded = true } };
        var issues = Rule.Check(Set(), cards);
        Assert.Empty(issues);
    }

    [Fact]
    public void Should_ReturnNoIssues_When_AllCardsAreRaw()
    {
        var cards = new List<Card> { new() { IsGraded = false }, new() { IsGraded = false } };
        var issues = Rule.Check(Set(), cards);
        Assert.Empty(issues);
    }
}
