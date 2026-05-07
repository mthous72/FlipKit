using System.Collections.Generic;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations.SurpriseSets.Rules;
using Xunit;

namespace FlipKit.Core.Tests.Services.SurpriseSet.Rules;

public class MinCardsRuleTests
{
    private static readonly MinCardsRule Rule = new();
    private static Models.SurpriseSet Set() => new();

    [Fact]
    public void Should_ReturnError_When_CardListIsEmpty()
    {
        var issues = Rule.Check(Set(), new List<Card>());
        Assert.Contains(issues, i => i.Code == "MIN_CARDS" && i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void Should_ReturnNoIssues_When_AtLeastOneCard()
    {
        var issues = Rule.Check(Set(), new List<Card> { new() });
        Assert.Empty(issues);
    }
}
