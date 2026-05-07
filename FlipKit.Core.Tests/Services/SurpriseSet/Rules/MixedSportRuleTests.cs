using System.Collections.Generic;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations.SurpriseSets.Rules;
using Xunit;

namespace FlipKit.Core.Tests.Services.SurpriseSet.Rules;

public class MixedSportRuleTests
{
    private static readonly MixedSportRule Rule = new();
    private static Models.SurpriseSet Set() => new();

    [Fact]
    public void Should_ReturnWarning_When_MultipleDistinctSports()
    {
        var cards = new List<Card>
        {
            new() { Sport = Sport.Baseball },
            new() { Sport = Sport.Basketball },
        };
        var issues = Rule.Check(Set(), cards);
        Assert.Contains(issues, i => i.Code == "MIXED_SPORT" && i.Severity == IssueSeverity.Warning);
    }

    [Fact]
    public void Should_ReturnNoIssues_When_AllCardsSameSport()
    {
        var cards = new List<Card>
        {
            new() { Sport = Sport.Baseball },
            new() { Sport = Sport.Baseball },
        };
        var issues = Rule.Check(Set(), cards);
        Assert.Empty(issues);
    }

    [Fact]
    public void Should_ReturnNoIssues_When_NullSportCardsOnly()
    {
        var cards = new List<Card> { new() { Sport = null }, new() { Sport = null } };
        var issues = Rule.Check(Set(), cards);
        Assert.Empty(issues);
    }

    [Fact]
    public void Should_IgnoreNullSport_When_ComputingDistinctSports()
    {
        // One null-sport card + one Baseball card = only 1 distinct non-null sport → no warning
        var cards = new List<Card>
        {
            new() { Sport = null },
            new() { Sport = Sport.Baseball },
        };
        var issues = Rule.Check(Set(), cards);
        Assert.Empty(issues);
    }
}
