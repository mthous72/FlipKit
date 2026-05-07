using System.Collections.Generic;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services.Implementations.SurpriseSets;
using Xunit;

namespace FlipKit.Core.Tests.Services.SurpriseSet;

public class RevenueAllocationServiceTests
{
    private static readonly RevenueAllocationService _svc = new();

    private static Card Card(int id, int slot, decimal? cost = null, decimal? salePrice = null) => new()
    {
        Id = id,
        SurpriseSetSlot = slot,
        CostBasis = cost,
        SalePrice = salePrice,
    };

    // ── EqualSplit ───────────────────────────────────────────────────────────

    [Fact]
    public void EqualSplit_AllSold_NetDividedEvenly()
    {
        var cards = new List<Card> { Card(1, 1), Card(2, 2), Card(3, 3) };
        var result = _svc.Allocate(RevenueAllocationMethod.EqualSplit, cards, 3, 30m, 3m, 0m);

        Assert.Equal(3, result.Count);
        Assert.All(result, a => Assert.True(a.IsSold));
        // net = 27; per card = 9.00
        Assert.All(result, a => Assert.Equal(9.00m, a.AllocatedRevenue));
    }

    [Fact]
    public void EqualSplit_RoundingRemainderGoesToLastCard()
    {
        // net = 10; 3 cards → 3.33, 3.33, 3.34
        var cards = new List<Card> { Card(1, 1), Card(2, 2), Card(3, 3) };
        var result = _svc.Allocate(RevenueAllocationMethod.EqualSplit, cards, 3, 10m, 0m, 0m);

        Assert.Equal(3.33m, result[0].AllocatedRevenue);
        Assert.Equal(3.33m, result[1].AllocatedRevenue);
        Assert.Equal(3.34m, result[2].AllocatedRevenue);
        Assert.Equal(10.00m, result[0].AllocatedRevenue!.Value + result[1].AllocatedRevenue!.Value + result[2].AllocatedRevenue!.Value);
    }

    [Fact]
    public void EqualSplit_PartialSellThrough_UnsoldCardsHaveNullRevenue()
    {
        var cards = new List<Card> { Card(1, 1), Card(2, 2), Card(3, 3) };
        var result = _svc.Allocate(RevenueAllocationMethod.EqualSplit, cards, 2, 20m, 2m, 0m);

        Assert.True(result[0].IsSold);
        Assert.True(result[1].IsSold);
        Assert.False(result[2].IsSold);
        Assert.Null(result[2].AllocatedRevenue);
        // net = 18; per card = 9
        Assert.Equal(9m, result[0].AllocatedRevenue);
        Assert.Equal(9m, result[1].AllocatedRevenue);
    }

    [Fact]
    public void EqualSplit_ZeroSold_AllUnsold()
    {
        var cards = new List<Card> { Card(1, 1), Card(2, 2) };
        var result = _svc.Allocate(RevenueAllocationMethod.EqualSplit, cards, 0, 0m, 0m, 0m);

        Assert.Equal(2, result.Count);
        Assert.All(result, a => Assert.False(a.IsSold));
        Assert.All(result, a => Assert.Null(a.AllocatedRevenue));
    }

    [Fact]
    public void EqualSplit_NegativeNetRevenue_IsAllowed()
    {
        var cards = new List<Card> { Card(1, 1), Card(2, 2) };
        var result = _svc.Allocate(RevenueAllocationMethod.EqualSplit, cards, 2, 5m, 10m, 0m);

        // net = -5; per card = -2.50
        Assert.Equal(-2.50m, result[0].AllocatedRevenue);
        Assert.Equal(-2.50m, result[1].AllocatedRevenue);
    }

    [Fact]
    public void EqualSplit_CardsOrderedBySlot()
    {
        // Slot order must determine which cards are "sold" when spotsSold < total.
        var cards = new List<Card> { Card(10, 3), Card(20, 1), Card(30, 2) };
        var result = _svc.Allocate(RevenueAllocationMethod.EqualSplit, cards, 1, 10m, 0m, 0m);

        var sold = result.Single(a => a.IsSold);
        Assert.Equal(20, sold.CardId); // slot 1
    }

    // ── CostWeighted ─────────────────────────────────────────────────────────

    [Fact]
    public void CostWeighted_ProportionalToIndividualCost()
    {
        // Card A cost 1, Card B cost 3 → A gets 25%, B gets 75% of net 20
        var cards = new List<Card> { Card(1, 1, cost: 1m), Card(2, 2, cost: 3m) };
        var result = _svc.Allocate(RevenueAllocationMethod.CostWeighted, cards, 2, 20m, 0m, 0m);

        Assert.Equal(5.00m, result[0].AllocatedRevenue);
        Assert.Equal(15.00m, result[1].AllocatedRevenue);
    }

    [Fact]
    public void CostWeighted_MissingCostBasisFallsBackToEqualSplit()
    {
        var cards = new List<Card> { Card(1, 1, cost: null), Card(2, 2, cost: 5m) };
        var result = _svc.Allocate(RevenueAllocationMethod.CostWeighted, cards, 2, 20m, 0m, 0m);

        // Both should get 10 (equal fallback)
        Assert.Equal(10.00m, result[0].AllocatedRevenue);
        Assert.Equal(10.00m, result[1].AllocatedRevenue);
    }

    [Fact]
    public void CostWeighted_ZeroCostBasisFallsBackToEqualSplit()
    {
        var cards = new List<Card> { Card(1, 1, cost: 0m), Card(2, 2, cost: 5m) };
        var result = _svc.Allocate(RevenueAllocationMethod.CostWeighted, cards, 2, 20m, 0m, 0m);

        Assert.Equal(10.00m, result[0].AllocatedRevenue);
        Assert.Equal(10.00m, result[1].AllocatedRevenue);
    }

    [Fact]
    public void CostWeighted_UnsoldCardsExcludedFromWeighting()
    {
        var cards = new List<Card>
        {
            Card(1, 1, cost: 2m),
            Card(2, 2, cost: 2m),
            Card(3, 3, cost: 100m), // unsold — cost must not distort the sold allocation
        };
        var result = _svc.Allocate(RevenueAllocationMethod.CostWeighted, cards, 2, 20m, 0m, 0m);

        Assert.True(result[0].IsSold);
        Assert.True(result[1].IsSold);
        Assert.False(result[2].IsSold);
        Assert.Equal(10.00m, result[0].AllocatedRevenue);
        Assert.Equal(10.00m, result[1].AllocatedRevenue);
    }

    // ── Manual ───────────────────────────────────────────────────────────────

    [Fact]
    public void Manual_UsesSalePriceDirectly()
    {
        var cards = new List<Card> { Card(1, 1, salePrice: 7.50m), Card(2, 2, salePrice: 12.00m) };
        var result = _svc.Allocate(RevenueAllocationMethod.Manual, cards, 2, 19.50m, 0m, 0m);

        Assert.Equal(7.50m, result[0].AllocatedRevenue);
        Assert.Equal(12.00m, result[1].AllocatedRevenue);
    }

    [Fact]
    public void Manual_NullSalePriceIsPassedThrough()
    {
        var cards = new List<Card> { Card(1, 1, salePrice: null), Card(2, 2, salePrice: 5m) };
        var result = _svc.Allocate(RevenueAllocationMethod.Manual, cards, 2, 5m, 0m, 0m);

        Assert.Null(result[0].AllocatedRevenue);
        Assert.Equal(5m, result[1].AllocatedRevenue);
    }

    // ── Guard clauses ─────────────────────────────────────────────────────────

    [Fact]
    public void SpotsSoldExceedsCardCount_Throws()
    {
        var cards = new List<Card> { Card(1, 1) };
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            _svc.Allocate(RevenueAllocationMethod.EqualSplit, cards, 5, 50m, 0m, 0m));
    }

    [Fact]
    public void NegativeSpotsSold_Throws()
    {
        var cards = new List<Card> { Card(1, 1) };
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            _svc.Allocate(RevenueAllocationMethod.EqualSplit, cards, -1, 10m, 0m, 0m));
    }
}
