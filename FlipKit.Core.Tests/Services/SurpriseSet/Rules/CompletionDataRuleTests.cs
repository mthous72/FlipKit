using System.Collections.Generic;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations.SurpriseSets.Rules;
using Xunit;

namespace FlipKit.Core.Tests.Services.SurpriseSet.Rules;

public class CompletionDataRuleTests
{
    private static readonly CompletionDataRule Rule = new();

    [Theory]
    [InlineData(SurpriseSetState.Exported)]
    [InlineData(SurpriseSetState.Live)]
    [InlineData(SurpriseSetState.Completed)]
    public void Should_ReturnError_When_GrossRevenueIsMissing_InAdvancedState(SurpriseSetState state)
    {
        var set = new Models.SurpriseSet { State = state, GrossRevenue = null, SpotsSold = 10 };
        var issues = Rule.Check(set, new List<Card>());
        Assert.Contains(issues, i =>
            i.Code == "COMPLETION_DATA" &&
            i.Field == nameof(Models.SurpriseSet.GrossRevenue));
    }

    [Theory]
    [InlineData(SurpriseSetState.Exported)]
    [InlineData(SurpriseSetState.Live)]
    [InlineData(SurpriseSetState.Completed)]
    public void Should_ReturnError_When_SpotsSoldIsMissing_InAdvancedState(SurpriseSetState state)
    {
        var set = new Models.SurpriseSet { State = state, GrossRevenue = 500m, SpotsSold = null };
        var issues = Rule.Check(set, new List<Card>());
        Assert.Contains(issues, i =>
            i.Code == "COMPLETION_DATA" &&
            i.Field == nameof(Models.SurpriseSet.SpotsSold));
    }

    [Fact]
    public void Should_ReturnNoIssues_When_AllCompletionDataPresent()
    {
        var set = new Models.SurpriseSet
        {
            State = SurpriseSetState.Completed,
            GrossRevenue = 500m,
            SpotsSold = 10,
        };
        var issues = Rule.Check(set, new List<Card>());
        Assert.Empty(issues);
    }

    [Theory]
    [InlineData(SurpriseSetState.Draft)]
    [InlineData(SurpriseSetState.Cancelled)]
    public void Should_ReturnNoIssues_When_StateIsDraftOrCancelled(SurpriseSetState state)
    {
        var set = new Models.SurpriseSet { State = state, GrossRevenue = null, SpotsSold = null };
        var issues = Rule.Check(set, new List<Card>());
        Assert.Empty(issues);
    }
}
