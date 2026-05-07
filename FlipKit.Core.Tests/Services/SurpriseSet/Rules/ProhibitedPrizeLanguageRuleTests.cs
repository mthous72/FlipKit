using System.Collections.Generic;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations.SurpriseSets.Rules;
using Xunit;

namespace FlipKit.Core.Tests.Services.SurpriseSet.Rules;

public class ProhibitedPrizeLanguageRuleTests
{
    private static readonly ProhibitedPrizeLanguageRule Rule = new();

    [Theory]
    [InlineData("guaranteed hit")]
    [InlineData("big hit")]
    [InlineData("chase card")]
    [InlineData("chase")]
    [InlineData("holy grail")]
    [InlineData("grail card")]
    [InlineData("whale hit")]
    [InlineData("prize card")]
    public void Should_ReturnError_When_TitleContainsProhibitedPrizeLanguage(string keyword)
    {
        var set = new Models.SurpriseSet { Title = $"Mystery set with {keyword} inside" };
        var issues = Rule.Check(set, new List<Card>());
        Assert.Contains(issues, i =>
            i.Code == "PROHIBITED_PRIZE_LANG" && i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void Should_ReturnError_When_ProhibitedLanguageIsInNotes()
    {
        var set = new Models.SurpriseSet { Title = "Mystery Baseball", Notes = "Includes a guaranteed hit card!" };
        var issues = Rule.Check(set, new List<Card>());
        Assert.Contains(issues, i => i.Code == "PROHIBITED_PRIZE_LANG");
    }

    [Fact]
    public void Should_ReturnNoIssues_When_TitleAndNotesAreClean()
    {
        var set = new Models.SurpriseSet { Title = "Baseball Mystery Set", Notes = "Great cards inside." };
        var issues = Rule.Check(set, new List<Card>());
        Assert.Empty(issues);
    }

    [Fact]
    public void Should_BeCaseInsensitive()
    {
        var set = new Models.SurpriseSet { Title = "GUARANTEED HIT SET" };
        var issues = Rule.Check(set, new List<Card>());
        Assert.Contains(issues, i => i.Code == "PROHIBITED_PRIZE_LANG");
    }
}
