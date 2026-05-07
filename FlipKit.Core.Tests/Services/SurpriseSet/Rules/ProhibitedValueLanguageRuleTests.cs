using System.Collections.Generic;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations.SurpriseSets.Rules;
using Xunit;

namespace FlipKit.Core.Tests.Services.SurpriseSet.Rules;

public class ProhibitedValueLanguageRuleTests
{
    private static readonly ProhibitedValueLanguageRule Rule = new();

    [Theory]
    [InlineData("floor", "Minimum floor value sets")]
    [InlineData("ceiling", "Sets with ceiling pricing")]
    [InlineData("average value", "Average value per card")]
    [InlineData("book value", "Cards at book value")]
    [InlineData("estimated value", "estimated value guaranteed")]
    [InlineData("worth at least", "Cards worth at least $10")]
    [InlineData("valued at", "Cards valued at $20 each")]
    [InlineData("guaranteed value", "guaranteed value sets")]
    [InlineData("guaranteed minimum", "guaranteed minimum $5")]
    [InlineData("min value", "min value assured")]
    public void Should_ReturnError_When_TitleOrNotesContainProhibitedValueLanguage(string keyword, string text)
    {
        var set = new Models.SurpriseSet { Title = text };
        var issues = Rule.Check(set, new List<Card>());
        Assert.Contains(issues, i =>
            i.Code == "PROHIBITED_VALUE_LANG" && i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void Should_ReturnError_When_ProhibitedLanguageIsInNotes()
    {
        var set = new Models.SurpriseSet { Title = "Mystery Set", Notes = "guaranteed minimum 5 dollars" };
        var issues = Rule.Check(set, new List<Card>());
        Assert.Contains(issues, i => i.Code == "PROHIBITED_VALUE_LANG");
    }

    [Fact]
    public void Should_ReturnNoIssues_When_TitleAndNotesAreClean()
    {
        var set = new Models.SurpriseSet { Title = "Baseball Mystery Set", Notes = "Fun set!" };
        var issues = Rule.Check(set, new List<Card>());
        Assert.Empty(issues);
    }

    [Fact]
    public void Should_BeCaseInsensitive()
    {
        var set = new Models.SurpriseSet { Title = "FLOOR VALUE ASSURED" };
        var issues = Rule.Check(set, new List<Card>());
        Assert.Contains(issues, i => i.Code == "PROHIBITED_VALUE_LANG");
    }
}
