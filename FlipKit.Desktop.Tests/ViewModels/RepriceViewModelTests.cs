using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Desktop.ViewModels;
using NSubstitute;

namespace FlipKit.Desktop.Tests.ViewModels;

public class RepriceViewModelTests
{
    private static RepriceViewModel Create(
        ICardRepository? repo = null,
        IPricerService? pricer = null,
        IBrowserService? browser = null,
        ISettingsService? settings = null)
    {
        settings ??= Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { WhatnotFeePercent = 11m, PriceStalenessThresholdDays = 30 });
        return new RepriceViewModel(
            repo ?? Substitute.For<ICardRepository>(),
            pricer ?? Substitute.For<IPricerService>(),
            browser ?? Substitute.For<IBrowserService>(),
            settings);
    }

    private static List<Card> SampleStale() => new()
    {
        new Card { Id = 1, PlayerName = "Stale1", ListingPrice = 10m, PriceDate = DateTime.UtcNow.AddDays(-45) },
        new Card { Id = 2, PlayerName = "Stale2", ListingPrice = 20m, PriceDate = DateTime.UtcNow.AddDays(-60) },
    };

    private static async Task<RepriceViewModel> CreateAndWaitForLoad(ICardRepository repo, IPricerService? pricer = null)
    {
        var vm = Create(repo: repo, pricer: pricer);
        for (int i = 0; i < 20 && !vm.HasCards && string.IsNullOrEmpty(vm.StatusMessage); i++)
            await Task.Delay(10);
        return vm;
    }

    // === Initial load ===

    [Fact]
    public async Task Should_PopulateFirstCard_When_LoadFindsStaleCards()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetStaleCardsAsync(30).Returns(SampleStale());
        var vm = await CreateAndWaitForLoad(repo);

        Assert.True(vm.HasCards);
        Assert.Equal("Stale1", vm.CurrentCard!.PlayerName);
        Assert.Equal(10m, vm.CurrentPrice);
        Assert.Equal(2, vm.TotalCount);
    }

    [Fact]
    public async Task Should_ShowFreshMessage_When_NoStaleCards()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetStaleCardsAsync(30).Returns(new List<Card>());
        var vm = await CreateAndWaitForLoad(repo);

        Assert.False(vm.HasCards);
        Assert.Contains("All prices are fresh", vm.StatusMessage);
    }

    // === Price calculations ===

    [Fact]
    public async Task Should_SetSuggestedAndNewListing_When_NewMarketValueChanges()
    {
        var pricer = Substitute.For<IPricerService>();
        pricer.SuggestPrice(50m, Arg.Any<Card>()).Returns(45m);
        var repo = Substitute.For<ICardRepository>();
        repo.GetStaleCardsAsync(30).Returns(SampleStale());
        var vm = await CreateAndWaitForLoad(repo, pricer);

        vm.NewMarketValue = 50m;

        Assert.Equal(45m, vm.SuggestedPrice);
        Assert.Equal(45m, vm.NewListingPrice);
    }

    [Fact]
    public async Task Should_ComputeNetAfterFees_When_NewListingPriceChanges()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetStaleCardsAsync(30).Returns(SampleStale());
        var vm = await CreateAndWaitForLoad(repo);

        vm.NewListingPrice = 100m;

        Assert.Equal(88.70m, vm.NetAfterFees);
    }

    // === Browser deeplinks ===

    [Fact]
    public async Task Should_OpenTerapeakUrl_When_CommandFires()
    {
        var pricer = Substitute.For<IPricerService>();
        pricer.BuildTerapeakUrl(Arg.Any<Card>()).Returns("https://terapeak/x");
        var browser = Substitute.For<IBrowserService>();
        var repo = Substitute.For<ICardRepository>();
        repo.GetStaleCardsAsync(30).Returns(SampleStale());
        var vm = Create(repo: repo, pricer: pricer, browser: browser);
        for (int i = 0; i < 20 && vm.CurrentCard == null; i++) await Task.Delay(10);

        vm.OpenTerapeakCommand.Execute(null);

        browser.Received(1).OpenUrl("https://terapeak/x");
    }

    // === Keep current price ===

    [Fact]
    public async Task Should_BumpPriceCheckAndUpdate_When_KeepCurrentPriceFires()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetStaleCardsAsync(30).Returns(SampleStale());
        var vm = await CreateAndWaitForLoad(repo);
        var initialCount = vm.CurrentCard!.PriceCheckCount;

        await vm.KeepCurrentPriceCommand.ExecuteAsync(null);

        await repo.Received(1).UpdateCardAsync(Arg.Is<Card>(c => c.PriceCheckCount == initialCount + 1));
        await repo.Received(1).AddPriceHistoryAsync(Arg.Any<PriceHistory>());
    }

    // === Save new price ===

    [Fact]
    public async Task Should_PersistNewPriceAndAddHistory_When_SaveNewPriceFires()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetStaleCardsAsync(30).Returns(SampleStale());
        var vm = await CreateAndWaitForLoad(repo);
        vm.NewMarketValue = 100m;
        vm.NewListingPrice = 90m;

        await vm.SaveNewPriceCommand.ExecuteAsync(null);

        await repo.Received(1).UpdateCardAsync(Arg.Is<Card>(c =>
            c.ListingPrice == 90m && c.EstimatedValue == 100m));
        await repo.Received(1).AddPriceHistoryAsync(Arg.Is<PriceHistory>(h => h.PriceSource == "Terapeak"));
    }

    [Fact]
    public async Task Should_DoNothing_When_SaveNewPriceWithoutNewListingPrice()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetStaleCardsAsync(30).Returns(SampleStale());
        var vm = await CreateAndWaitForLoad(repo);
        vm.NewListingPrice = null;

        await vm.SaveNewPriceCommand.ExecuteAsync(null);

        await repo.DidNotReceive().UpdateCardAsync(Arg.Any<Card>());
    }

    [Fact]
    public async Task Should_RemoveCardFromQueue_When_KeptOrSaved()
    {
        // Both KeepCurrent and SaveNew call AdvanceToNext which removes the current card.
        var repo = Substitute.For<ICardRepository>();
        repo.GetStaleCardsAsync(30).Returns(SampleStale());
        var vm = await CreateAndWaitForLoad(repo);

        await vm.KeepCurrentPriceCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.TotalCount);
        Assert.Equal("Stale2", vm.CurrentCard!.PlayerName);
    }

    [Fact]
    public async Task Should_ShowAllRepricedMessage_When_AdvancingPastLast()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetStaleCardsAsync(30).Returns(new List<Card>
        {
            new() { Id = 1, PlayerName = "Solo", ListingPrice = 5m, PriceDate = DateTime.UtcNow.AddDays(-90) },
        });
        var vm = await CreateAndWaitForLoad(repo);

        await vm.KeepCurrentPriceCommand.ExecuteAsync(null);

        Assert.False(vm.HasCards);
        Assert.Null(vm.CurrentCard);
        Assert.Contains("All stale cards repriced", vm.StatusMessage);
    }

    // === Skip wraps around ===

    [Fact]
    public async Task Should_AdvanceWithoutRemoving_When_SkipFires()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetStaleCardsAsync(30).Returns(SampleStale());
        var vm = await CreateAndWaitForLoad(repo);

        vm.SkipCommand.Execute(null);

        Assert.Equal("Stale2", vm.CurrentCard!.PlayerName);
        Assert.Equal(2, vm.TotalCount); // not removed
    }
}
