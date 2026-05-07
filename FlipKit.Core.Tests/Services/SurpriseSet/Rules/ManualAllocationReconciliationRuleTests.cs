using System.Collections.Generic;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations.SurpriseSets.Rules;
using Xunit;

namespace FlipKit.Core.Tests.Services.SurpriseSet.Rules;

public class ManualAllocationReconciliationRuleTests
{
    private static readonly ManualAllocationReconciliationRule Rule = new();

    private static Models.SurpriseSet ManualSet(decimal grossRevenue, decimal fees = 0m, decimal shipping = 0m) =>
        new()
        {
            AllocationMethod = RevenueAllocationMethod.Manual,
            GrossRevenue = grossRevenue,
            TotalFees = fees,
            TotalShipping = shipping,
        };

    [Fact]
    public void Should_ReturnError_When_AllocatedTotalDoesNotMatchNetGross()
    {
        var set = ManualSet(grossRevenue: 100m, fees: 10m, shipping: 5m); // netGross = 85
        var cards = new List<Card>
        {
            new() { SalePrice = 40m },
            new() { SalePrice = 40m }, // total = 80, diff = 5 → error
        };

        var issues = Rule.Check(set, cards);

        Assert.Contains(issues, i =>
            i.Code == "MANUAL_ALLOC_MISMATCH" && i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void Should_ReturnNoIssues_When_AllocatedTotalMatchesNetGrossExactly()
    {
        var set = ManualSet(grossRevenue: 100m, fees: 10m, shipping: 5m); // netGross = 85
        var cards = new List<Card>
        {
            new() { SalePrice = 50m },
            new() { SalePrice = 35m }, // total = 85 = netGross
        };

        var issues = Rule.Check(set, cards);
        Assert.Empty(issues);
    }

    [Fact]
    public void Should_ReturnNoIssues_When_DifferenceIsWithinTolerance()
    {
        var set = ManualSet(grossRevenue: 100m); // netGross = 100
        var cards = new List<Card>
        {
            new() { SalePrice = 99.995m }, // diff = 0.005 < 0.01 tolerance
        };

        var issues = Rule.Check(set, cards);
        Assert.Empty(issues);
    }

    [Fact]
    public void Should_ReturnNoIssues_When_AllocationMethodIsNotManual()
    {
        var set = new Models.SurpriseSet
        {
            AllocationMethod = RevenueAllocationMethod.EqualSplit,
            GrossRevenue = 100m,
        };
        var cards = new List<Card> { new() { SalePrice = 50m } }; // clearly wrong total
        var issues = Rule.Check(set, cards);
        Assert.Empty(issues);
    }

    [Fact]
    public void Should_ReturnNoIssues_When_GrossRevenueIsNull()
    {
        var set = new Models.SurpriseSet
        {
            AllocationMethod = RevenueAllocationMethod.Manual,
            GrossRevenue = null,
        };
        var cards = new List<Card> { new() { SalePrice = 50m } };
        var issues = Rule.Check(set, cards);
        Assert.Empty(issues);
    }

    [Fact]
    public void Should_ReturnNoIssues_When_NoCardsHaveSalePrice()
    {
        var set = ManualSet(grossRevenue: 100m);
        var cards = new List<Card> { new() { SalePrice = null } };
        var issues = Rule.Check(set, cards);
        Assert.Empty(issues);
    }

    [Fact]
    public void Should_OnlyCountCardsWithSalePrice_WhenSomeAreNull()
    {
        var set = ManualSet(grossRevenue: 100m); // netGross = 100
        var cards = new List<Card>
        {
            new() { SalePrice = 100m },
            new() { SalePrice = null }, // excluded from sum
        };

        var issues = Rule.Check(set, cards);
        Assert.Empty(issues);
    }
}
