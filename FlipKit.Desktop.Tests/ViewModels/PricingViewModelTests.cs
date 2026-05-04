using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Desktop.ViewModels;
using NSubstitute;

namespace FlipKit.Desktop.Tests.ViewModels;

public class PricingViewModelTests
{
    private static PricingViewModel Create(
        ICardRepository? repo = null,
        IPricerService? pricer = null,
        IBrowserService? browser = null,
        ISettingsService? settings = null)
    {
        settings ??= Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { WhatnotFeePercent = 11m });
        return new PricingViewModel(
            repo ?? Substitute.For<ICardRepository>(),
            pricer ?? Substitute.For<IPricerService>(),
            browser ?? Substitute.For<IBrowserService>(),
            settings);
    }

    private static List<Card> Two() => new()
    {
        new Card { Id = 1, PlayerName = "A", Status = CardStatus.Draft, EstimatedValue = 10m },
        new Card { Id = 2, PlayerName = "B", Status = CardStatus.Draft, EstimatedValue = 20m },
    };

    private static async Task<PricingViewModel> CreateAndWaitForLoad(ICardRepository repo, IPricerService? pricer = null)
    {
        var vm = Create(repo: repo, pricer: pricer);
        // Constructor fires LoadUnpricedAsync; spin briefly to let it settle.
        for (int i = 0; i < 20 && !vm.HasCards && string.IsNullOrEmpty(vm.StatusMessage); i++)
            await Task.Delay(10);
        return vm;
    }

    // === Price calculation reactivity ===

    [Fact]
    public void Should_SetSuggestedAndListing_When_MarketValueChanges()
    {
        var pricer = Substitute.For<IPricerService>();
        pricer.SuggestPrice(50m, Arg.Any<Card>()).Returns(45m);
        var vm = Create(pricer: pricer);
        // Need a CurrentCard for the partial method to compute
        typeof(PricingViewModel).GetProperty("CurrentCard")!
            .SetValue(vm, new Card { PlayerName = "X" });

        vm.MarketValue = 50m;

        Assert.Equal(45m, vm.SuggestedPrice);
        Assert.Equal(45m, vm.ListingPrice);
    }

    [Fact]
    public void Should_ClearSuggestedAndListing_When_MarketValueClearedToNull()
    {
        var vm = Create();
        // Have to actually transition non-null → null for the partial to fire.
        vm.MarketValue = 50m;
        vm.SuggestedPrice = 10m;
        vm.ListingPrice = 12m;

        vm.MarketValue = null;

        Assert.Null(vm.SuggestedPrice);
        Assert.Null(vm.ListingPrice);
    }

    [Fact]
    public void Should_ComputeNetAfterFees_When_ListingPriceChanges()
    {
        var vm = Create();
        vm.ListingPrice = 100m;
        // Default Whatnot fee 11%: 100 * 0.89 - 0.30 = 88.70
        Assert.Equal(88.70m, vm.NetAfterFees);
    }

    [Fact]
    public void Should_ClearNetAfterFees_When_ListingPriceClearedToNull()
    {
        var vm = Create();
        vm.ListingPrice = 100m;
        vm.ListingPrice = null;

        Assert.Null(vm.NetAfterFees);
    }

    // === Browser opens ===

    [Fact]
    public async Task Should_OpenTerapeakWithCardUrl_When_CommandFires()
    {
        var pricer = Substitute.For<IPricerService>();
        pricer.BuildTerapeakUrl(Arg.Any<Card>()).Returns("https://terapeak.example/q");
        var browser = Substitute.For<IBrowserService>();
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Draft).Returns(Two());
        var vm = Create(repo: repo, pricer: pricer, browser: browser);
        for (int i = 0; i < 20 && vm.CurrentCard == null; i++) await Task.Delay(10);

        vm.OpenTerapeakCommand.Execute(null);

        browser.Received(1).OpenUrl("https://terapeak.example/q");
    }

    [Fact]
    public async Task Should_OpenEbaySoldWithCardUrl_When_CommandFires()
    {
        var pricer = Substitute.For<IPricerService>();
        pricer.BuildEbaySoldUrl(Arg.Any<Card>()).Returns("https://ebay.example/sold");
        var browser = Substitute.For<IBrowserService>();
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Draft).Returns(Two());
        var vm = Create(repo: repo, pricer: pricer, browser: browser);
        for (int i = 0; i < 20 && vm.CurrentCard == null; i++) await Task.Delay(10);

        vm.OpenEbaySoldCommand.Execute(null);

        browser.Received(1).OpenUrl("https://ebay.example/sold");
    }

    // === SaveAndNext: persists card, advances, finishes when empty ===

    [Fact]
    public async Task Should_PersistCardAndAdvance_When_SaveAndNextFires()
    {
        var pricer = Substitute.For<IPricerService>();
        pricer.SuggestPrice(30m, Arg.Any<Card>()).Returns(25m); // make the partial method's override match
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Draft).Returns(Two());
        var vm = await CreateAndWaitForLoad(repo, pricer);
        vm.MarketValue = 30m; // partial fires, sets ListingPrice to SuggestPrice's 25m

        await vm.SaveAndNextCommand.ExecuteAsync(null);

        await repo.Received(1).UpdateCardAsync(Arg.Is<Card>(c =>
            c.ListingPrice == 25m && c.EstimatedValue == 30m && c.Status == CardStatus.Priced));
        await repo.Received(1).AddPriceHistoryAsync(Arg.Any<PriceHistory>());
        Assert.Equal(1, vm.TotalCount); // one consumed, one remaining
    }

    [Fact]
    public async Task Should_FlipHasCardsFalse_When_SaveAndNextEmptiesQueue()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Draft).Returns(new List<Card>
        {
            new() { Id = 1, PlayerName = "Solo", Status = CardStatus.Draft },
        });
        var vm = await CreateAndWaitForLoad(repo);
        vm.ListingPrice = 5m;

        await vm.SaveAndNextCommand.ExecuteAsync(null);

        Assert.False(vm.HasCards);
        Assert.Null(vm.CurrentCard);
        Assert.Contains("All cards priced", vm.StatusMessage);
    }

    [Fact]
    public async Task Should_DoNothing_When_SaveAndNextWithoutListingPrice()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Draft).Returns(Two());
        var vm = await CreateAndWaitForLoad(repo);
        vm.ListingPrice = null;

        await vm.SaveAndNextCommand.ExecuteAsync(null);

        await repo.DidNotReceive().UpdateCardAsync(Arg.Any<Card>());
    }

    // === Skip / Previous wrap correctly ===

    [Fact]
    public async Task Should_AdvanceWithoutSaving_When_SkipFires()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Draft).Returns(Two());
        var vm = await CreateAndWaitForLoad(repo);

        vm.SkipCommand.Execute(null);

        Assert.Equal("B", vm.CurrentCard!.PlayerName);
        await repo.DidNotReceive().UpdateCardAsync(Arg.Any<Card>());
    }

    [Fact]
    public async Task Should_WrapAround_When_SkipPastLastCard()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Draft).Returns(Two());
        var vm = await CreateAndWaitForLoad(repo);

        vm.SkipCommand.Execute(null); // → B
        vm.SkipCommand.Execute(null); // → wrap to A

        Assert.Equal("A", vm.CurrentCard!.PlayerName);
    }

    [Fact]
    public async Task Should_WrapBackwards_When_PreviousFromFirstCard()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Draft).Returns(Two());
        var vm = await CreateAndWaitForLoad(repo);

        vm.PreviousCommand.Execute(null); // from index 0 → wrap to last (B)

        Assert.Equal("B", vm.CurrentCard!.PlayerName);
    }
}
