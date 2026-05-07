using System.Collections.Generic;
using System.Linq;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations.SurpriseSets.Rules;
using Xunit;

namespace FlipKit.Core.Tests.Services.SurpriseSet.Rules;

public class InconsistentConditionRuleTests
{
    private static readonly InconsistentConditionRule Rule = new();

    private static Models.SurpriseSet Set(string condition = "Near Mint") =>
        new() { SharedCondition = condition };

    [Fact]
    public void Should_ReturnError_When_CardConditionDiffersFromSetCondition()
    {
        var set = Set("Near Mint");
        var cards = new List<Card> { new() { PlayerName = "Trout", Condition = "Good" } };

        var issues = Rule.Check(set, cards);

        Assert.Contains(issues, i =>
            i.Code == "INCONSISTENT_CONDITION" &&
            i.Severity == IssueSeverity.Error &&
            i.Field == nameof(Card.Condition));
    }

    [Fact]
    public void Should_ReturnOneErrorPerMismatchedCard()
    {
        var set = Set("Near Mint");
        var cards = new List<Card>
        {
            new() { PlayerName = "Card1", Condition = "Good" },
            new() { PlayerName = "Card2", Condition = "Good" },
            new() { PlayerName = "Card3", Condition = "Near Mint" },
        };

        var issues = Rule.Check(set, cards);
        Assert.Equal(2, issues.Count());
    }

    [Fact]
    public void Should_ReturnNoIssues_When_AllCardsMatchSetCondition()
    {
        var set = Set("Near Mint");
        var cards = new List<Card>
        {
            new() { Condition = "Near Mint" },
            new() { Condition = "Near Mint" },
        };

        var issues = Rule.Check(set, cards);
        Assert.Empty(issues);
    }

    [Fact]
    public void Should_SkipCards_WithEmptyCondition()
    {
        var set = Set("Near Mint");
        var cards = new List<Card> { new() { Condition = "" } };

        var issues = Rule.Check(set, cards);
        Assert.Empty(issues);
    }
}
