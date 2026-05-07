using System.Collections.Generic;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations.SurpriseSets;
using Xunit;

namespace FlipKit.Core.Tests.Services.SurpriseSet;

public class SurpriseSetValidatorTests
{
    private static readonly SurpriseSetValidator Validator = new();

    private static Models.SurpriseSet ValidSet() => new()
    {
        Title = "Baseball Mystery Set",
        SharedCondition = "Near Mint",
        SharedImageUrl1 = "https://example.com/gallery.jpg",
        AllocationMethod = RevenueAllocationMethod.EqualSplit,
        State = SurpriseSetState.Draft,
    };

    [Fact]
    public void Should_ReturnNoIssues_When_SetIsMinimallyValid()
    {
        var set = ValidSet();
        var cards = new List<Card> { new() { Condition = "Near Mint" } };

        var issues = Validator.Validate(set, cards);

        Assert.Empty(issues);
    }

    [Fact]
    public void Should_ReturnMultipleIssues_When_MultipleRulesFire()
    {
        var set = ValidSet();
        set.SharedImageUrl1 = null;          // MISSING_GALLERY
        set.Title = "Chase guaranteed hit";  // PROHIBITED_PRIZE_LANG
        var cards = new List<Card>();        // MIN_CARDS

        var issues = Validator.Validate(set, cards);

        Assert.Contains(issues, i => i.Code == "MIN_CARDS");
        Assert.Contains(issues, i => i.Code == "MISSING_GALLERY");
        Assert.Contains(issues, i => i.Code == "PROHIBITED_PRIZE_LANG");
    }

    [Fact]
    public void Should_ReturnEmptyList_WhenNoRulesFire()
    {
        var set = ValidSet();
        var cards = new List<Card> { new() { Condition = "Near Mint" } };

        var result = Validator.Validate(set, cards);

        Assert.IsAssignableFrom<IList<SurpriseSetIssue>>(result);
        Assert.Empty(result);
    }
}
