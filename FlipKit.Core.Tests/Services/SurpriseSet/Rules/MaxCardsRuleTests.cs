using System.Collections.Generic;
using System.Linq;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations.SurpriseSets.Rules;
using Xunit;

namespace FlipKit.Core.Tests.Services.SurpriseSet.Rules;

public class MaxCardsRuleTests
{
    private static readonly MaxCardsRule Rule = new();
    private static Models.SurpriseSet Set() => new();

    [Fact]
    public void Should_ReturnError_When_CardCountExceeds500()
    {
        var cards = Enumerable.Range(0, 501).Select(_ => new Card()).ToList();
        var issues = Rule.Check(Set(), cards);
        Assert.Contains(issues, i => i.Code == "MAX_CARDS" && i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void Should_ReturnNoIssues_When_CardCountIs500()
    {
        var cards = Enumerable.Range(0, 500).Select(_ => new Card()).ToList();
        var issues = Rule.Check(Set(), cards);
        Assert.Empty(issues);
    }

    [Fact]
    public void Should_ReturnNoIssues_When_CardCountIsBelowLimit()
    {
        var cards = Enumerable.Range(0, 10).Select(_ => new Card()).ToList();
        var issues = Rule.Check(Set(), cards);
        Assert.Empty(issues);
    }
}
