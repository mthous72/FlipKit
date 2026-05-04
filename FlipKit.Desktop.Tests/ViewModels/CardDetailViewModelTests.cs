using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Desktop.ViewModels;

namespace FlipKit.Desktop.Tests.ViewModels;

/// <summary>
/// CardDetailViewModel is a pure data shape — no commands, no services. Just verify
/// the FromCard / ToCard round-trip and the static option lists.
/// </summary>
public class CardDetailViewModelTests
{
    private static Card FullySpecifiedCard() => new()
    {
        PlayerName = "Mike Trout",
        CardNumber = "BCP-1",
        Year = 2026,
        Sport = Sport.Baseball,
        Manufacturer = "Topps",
        Brand = "Bowman",
        SetName = "Bowman Chrome Prospects",
        Team = "Angels",
        VariationType = "Refractor",
        ParallelName = "Silver",
        SerialNumbered = "/199",
        IsShortPrint = true,
        IsSSP = false,
        IsRookie = true,
        IsAuto = true,
        IsRelic = false,
        Condition = "Near Mint",
        IsGraded = true,
        GradeCompany = "PSA",
        GradeValue = "10",
        CertNumber = "12345",
        AutoGrade = "10",
        CostBasis = 4.50m,
        CostSource = CostSource.Break,
        CostDate = new DateTime(2026, 5, 1),
        CostNotes = "Box break",
        EstimatedValue = 50m,
        ListingPrice = 45m,
        Status = CardStatus.Ready,
        Quantity = 1,
        ListingType = "Buy It Now",
        Offerable = true,
        ShippingProfile = "1-3 oz",
        WhatnotCategory = "Sports Cards",
        WhatnotSubcategory = "Baseball Singles",
        Notes = "Test card",
    };

    [Fact]
    public void Should_RoundTripCard_When_GoingThroughFromCardThenToCard()
    {
        var original = FullySpecifiedCard();

        var vm = CardDetailViewModel.FromCard(original);
        var roundTripped = vm.ToCard();

        // Spot-check key fields across all sections of the model.
        Assert.Equal(original.PlayerName, roundTripped.PlayerName);
        Assert.Equal(original.CardNumber, roundTripped.CardNumber);
        Assert.Equal(original.Year, roundTripped.Year);
        Assert.Equal(original.Sport, roundTripped.Sport);
        Assert.Equal(original.ParallelName, roundTripped.ParallelName);
        Assert.Equal(original.IsRookie, roundTripped.IsRookie);
        Assert.Equal(original.IsGraded, roundTripped.IsGraded);
        Assert.Equal(original.GradeValue, roundTripped.GradeValue);
        Assert.Equal(original.CostBasis, roundTripped.CostBasis);
        Assert.Equal(original.ListingPrice, roundTripped.ListingPrice);
        Assert.Equal(original.WhatnotCategory, roundTripped.WhatnotCategory);
        Assert.Equal(original.Notes, roundTripped.Notes);
    }

    [Fact]
    public void Should_DefaultPropertiesToSpecValues_When_CreatedEmpty()
    {
        var vm = new CardDetailViewModel();

        Assert.Equal(string.Empty, vm.PlayerName);
        Assert.Equal("Base", vm.VariationType);
        Assert.Equal("Near Mint", vm.Condition);
        Assert.Equal(1, vm.Quantity);
        Assert.Equal("Buy It Now", vm.ListingType);
        Assert.True(vm.Offerable);
        Assert.Equal("4 oz", vm.ShippingProfile);
        Assert.Equal("Sports Cards", vm.WhatnotCategory);
        Assert.Equal(CardStatus.Draft, vm.Status);
    }

    [Fact]
    public void Should_IncludeStandardGradingCompanies_When_ListingOptions()
    {
        var vm = new CardDetailViewModel();

        Assert.Contains("PSA", vm.GradingCompanyOptions);
        Assert.Contains("BGS", vm.GradingCompanyOptions);
        Assert.Contains("CGC", vm.GradingCompanyOptions);
        Assert.Contains("SGC", vm.GradingCompanyOptions);
    }

    [Fact]
    public void Should_BuildGradeOptionsInHalfSteps_When_ListingGrades()
    {
        // Grades from 0 to 10 in 0.5 steps = 21 values, plus "" and "Authentic" = 23.
        Assert.Equal(23, CardDetailViewModel.GradeOptions.Count);
        Assert.Contains("", CardDetailViewModel.GradeOptions);
        Assert.Contains("Authentic", CardDetailViewModel.GradeOptions);
        Assert.Contains("9.5", CardDetailViewModel.GradeOptions);
        Assert.Contains("10", CardDetailViewModel.GradeOptions);
    }

    [Fact]
    public void Should_IncludeAllConditionOptions_When_Listing()
    {
        Assert.Contains("Near Mint", CardDetailViewModel.ConditionOptions);
        Assert.Contains("Acceptable", CardDetailViewModel.ConditionOptions);
    }

    [Fact]
    public void Should_IncludeNullSentinelInOptionalEnumLists_When_Listing()
    {
        // The first entry of SportOptions and CostSourceOptions is null — represents
        // "no selection" in the UI dropdowns.
        Assert.Null(CardDetailViewModel.SportOptions[0]);
        Assert.Null(CardDetailViewModel.CostSourceOptions[0]);
    }
}
