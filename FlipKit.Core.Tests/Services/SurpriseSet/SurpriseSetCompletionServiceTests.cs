using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations.SurpriseSets;
using NSubstitute;
using Xunit;

namespace FlipKit.Core.Tests.Services.SurpriseSet;

public class SurpriseSetCompletionServiceTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static Models.SurpriseSet MakeSet(
        SurpriseSetState state = SurpriseSetState.Exported,
        RevenueAllocationMethod method = RevenueAllocationMethod.EqualSplit,
        int cardCount = 3) => new()
    {
        Id = 1,
        Name = "Test",
        State = state,
        AllocationMethod = method,
        Cards = Enumerable.Range(1, cardCount)
            .Select(i => new Card { Id = i, SurpriseSetSlot = i })
            .ToList<Card>() as ICollection<Card> ?? new List<Card>(),
    };

    private static SurpriseSetCompletionService CreateService(
        ISurpriseSetRepository? repo = null,
        IRevenueAllocationService? allocator = null)
    {
        repo ??= Substitute.For<ISurpriseSetRepository>();
        allocator ??= new RevenueAllocationService();
        return new SurpriseSetCompletionService(repo, allocator);
    }

    private static CompleteSetRequest FullSell(int spotsSold = 3, decimal gross = 30m) => new()
    {
        SpotsSold = spotsSold,
        GrossRevenue = gross,
        TotalFees = 3m,
        TotalShipping = 0m,
    };

    // ── success cases ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CompleteAsync_FullSell_ReturnsSuccessWithAllocations()
    {
        var set = MakeSet(cardCount: 3);
        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(1).Returns(set);

        var svc = CreateService(repo);
        var result = await svc.CompleteAsync(1, FullSell(3, 33m));

        Assert.True(result.Success);
        Assert.Equal(3, result.Allocations.Count);
        Assert.All(result.Allocations, a => Assert.True(a.IsSold));
    }

    [Fact]
    public async Task CompleteAsync_PartialSell_UnsoldAllocationsAreFalse()
    {
        var set = MakeSet(cardCount: 3);
        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(1).Returns(set);

        var svc = CreateService(repo);
        var result = await svc.CompleteAsync(1, FullSell(2, 20m));

        Assert.True(result.Success);
        Assert.Equal(2, result.Allocations.Count(a => a.IsSold));
        Assert.Equal(1, result.Allocations.Count(a => !a.IsSold));
    }

    [Theory]
    [InlineData(SurpriseSetState.Draft)]
    [InlineData(SurpriseSetState.Exported)]
    [InlineData(SurpriseSetState.Live)]
    public async Task CompleteAsync_ValidStartingStates_Succeed(SurpriseSetState state)
    {
        var set = MakeSet(state: state);
        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(1).Returns(set);

        var svc = CreateService(repo);
        var result = await svc.CompleteAsync(1, FullSell());

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CompleteAsync_CallsCompleteSetAsyncOnRepository()
    {
        var set = MakeSet();
        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(1).Returns(set);

        var svc = CreateService(repo);
        await svc.CompleteAsync(1, FullSell());

        await repo.Received(1).CompleteSetAsync(
            Arg.Is<Models.SurpriseSet>(s => s.State == SurpriseSetState.Completed),
            Arg.Any<IList<CardAllocation>>(),
            Arg.Any<System.DateTime>());
    }

    [Fact]
    public async Task CompleteAsync_ZeroSold_Succeeds()
    {
        var set = MakeSet(cardCount: 3);
        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(1).Returns(set);

        var svc = CreateService(repo);
        var result = await svc.CompleteAsync(1, new CompleteSetRequest
        {
            SpotsSold = 0, GrossRevenue = 0m, TotalFees = 0m, TotalShipping = 0m,
        });

        Assert.True(result.Success);
        Assert.All(result.Allocations, a => Assert.False(a.IsSold));
    }

    // ── error cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CompleteAsync_SetNotFound_ReturnsFail()
    {
        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(99).Returns((Models.SurpriseSet?)null);

        var svc = CreateService(repo);
        var result = await svc.CompleteAsync(99, FullSell());

        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Theory]
    [InlineData(SurpriseSetState.Completed)]
    [InlineData(SurpriseSetState.Cancelled)]
    public async Task CompleteAsync_TerminalStates_ReturnsFail(SurpriseSetState state)
    {
        var set = MakeSet(state: state);
        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(1).Returns(set);

        var svc = CreateService(repo);
        var result = await svc.CompleteAsync(1, FullSell());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CompleteAsync_SpotsSoldExceedsCardCount_ReturnsFail()
    {
        var set = MakeSet(cardCount: 2);
        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(1).Returns(set);

        var svc = CreateService(repo);
        var result = await svc.CompleteAsync(1, FullSell(spotsSold: 5));

        Assert.False(result.Success);
        Assert.Contains("SpotsSold", result.ErrorMessage);
    }

    [Fact]
    public async Task CompleteAsync_NegativeGrossRevenue_ReturnsFail()
    {
        var set = MakeSet();
        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(1).Returns(set);

        var svc = CreateService(repo);
        var result = await svc.CompleteAsync(1, new CompleteSetRequest
        {
            SpotsSold = 3, GrossRevenue = -1m, TotalFees = 0m, TotalShipping = 0m,
        });

        Assert.False(result.Success);
    }
}
