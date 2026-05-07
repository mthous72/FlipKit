using System;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Desktop.Models;
using FlipKit.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Desktop.Tests.ViewModels;

public class InventoryViewModelTests
{
    private static InventoryViewModel Create(
        ICardRepository? repo = null,
        ISettingsService? settings = null,
        IExportService? export = null,
        IFileDialogService? dialog = null,
        IImageUploadService? upload = null,
        IBrowserService? browser = null,
        INavigationService? nav = null,
        IServiceProvider? services = null)
    {
        settings ??= Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings
        {
            WhatnotFeePercent = 11m,
            PriceStalenessThresholdDays = 30,
            CustomGradingCompanies = new List<string>(),
        });
        return new InventoryViewModel(
            repo ?? Substitute.For<ICardRepository>(),
            settings,
            export ?? Substitute.For<IExportService>(),
            dialog ?? Substitute.For<IFileDialogService>(),
            upload ?? Substitute.For<IImageUploadService>(),
            browser ?? Substitute.For<IBrowserService>(),
            nav ?? Substitute.For<INavigationService>(),
            services ?? Substitute.For<IServiceProvider>(),
            Substitute.For<IScannerService>(),
            NullLogger<InventoryViewModel>.Instance);
    }

    private static List<Card> Sample() => new()
    {
        new Card { Id = 1, PlayerName = "Mike Trout", Sport = Sport.Baseball, Status = CardStatus.Ready, ListingPrice = 25m, CostBasis = 5m },
        new Card { Id = 2, PlayerName = "Aaron Judge", Sport = Sport.Baseball, Status = CardStatus.Listed, ListingPrice = 30m },
        new Card { Id = 3, PlayerName = "Patrick Mahomes", Sport = Sport.Football, Status = CardStatus.Draft },
    };

    private static async Task<InventoryViewModel> CreateAndWait(ICardRepository repo, params object[] otherDeps)
    {
        var vm = Create(repo: repo);
        for (int i = 0; i < 20 && vm.FilteredCards.Count == 0; i++) await Task.Delay(10);
        return vm;
    }

    // === Initial load ===

    [Fact]
    public async Task Should_LoadAllCards_When_Constructed()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);

        Assert.Equal(3, vm.FilteredCards.Count);
        Assert.Equal(3, vm.TotalCount);
    }

    [Fact]
    public async Task Should_ComputeSummary_When_LoadCompletes()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);

        Assert.Equal(2, vm.PricedCount); // ListingPrice > 0 on Ready + Listed
        Assert.Equal(1, vm.NeedsPricingCount); // Draft only
        Assert.Equal(55m, vm.TotalValue); // 25 + 30
    }

    // === Filters ===

    [Fact]
    public async Task Should_FilterByPlayerName_When_SearchTextSet()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);

        vm.SearchText = "trout";

        Assert.Single(vm.FilteredCards);
        Assert.Equal("Mike Trout", vm.FilteredCards[0].Card.PlayerName);
    }

    [Fact]
    public async Task Should_FilterBySport_When_SportSelected()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);

        vm.SelectedSport = "Football";

        Assert.Single(vm.FilteredCards);
        Assert.Equal("Patrick Mahomes", vm.FilteredCards[0].Card.PlayerName);
    }

    [Fact]
    public async Task Should_FilterByStatus_When_StatusSelected()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);

        vm.SelectedStatus = "Ready";

        Assert.Single(vm.FilteredCards);
    }

    // === Edit panel ===

    [Fact]
    public async Task Should_OpenEditPanelWithCardData_When_EditSelectedFires()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);
        vm.SelectedItem = vm.FilteredCards[0];

        vm.EditSelectedCommand.Execute(null);

        Assert.True(vm.IsEditPanelOpen);
        Assert.NotNull(vm.EditingCard);
    }

    [Fact]
    public void Should_CloseEditPanel_When_CloseEditPanelFires()
    {
        var vm = Create();
        vm.IsEditPanelOpen = true;
        vm.EditingCard = new CardDetailViewModel();

        vm.CloseEditPanelCommand.Execute(null);

        Assert.False(vm.IsEditPanelOpen);
        Assert.Null(vm.EditingCard);
    }

    [Fact]
    public async Task Should_NavigateToFullEdit_When_OpenFullEditFires()
    {
        var nav = Substitute.For<INavigationService>();
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = Create(repo: repo, nav: nav);
        for (int i = 0; i < 20 && vm.FilteredCards.Count == 0; i++) await Task.Delay(10);
        vm.SelectedItem = vm.FilteredCards[0];

        await vm.OpenFullEditCommand.ExecuteAsync(null);

        await nav.Received(1).NavigateToEditCardAsync(1);
    }

    // === Delete ===

    [Fact]
    public async Task Should_OpenConfirmDialog_When_RequestDeleteFires()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);
        vm.SelectedItem = vm.FilteredCards[0];

        vm.RequestDeleteSelectedCommand.Execute(null);

        Assert.True(vm.ShowDeleteConfirmDialog);
        Assert.Equal(1, vm.DeleteCount);
    }

    [Fact]
    public void Should_CloseConfirmDialog_When_CancelDeleteFires()
    {
        var vm = Create();
        vm.ShowDeleteConfirmDialog = true;

        vm.CancelDeleteCommand.Execute(null);

        Assert.False(vm.ShowDeleteConfirmDialog);
    }

    [Fact]
    public async Task Should_DeleteCardAndRemoveFromList_When_ConfirmDeleteFires()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);
        vm.SelectedItem = vm.FilteredCards[0]; // Mike Trout

        await vm.ConfirmDeleteCommand.ExecuteAsync(null);

        await repo.Received(1).DeleteCardAsync(1);
        Assert.Equal(2, vm.FilteredCards.Count); // one removed
        Assert.False(vm.ShowDeleteConfirmDialog);
    }

    // === Sold dialog ===

    [Fact]
    public async Task Should_PopulateDialogFromCard_When_OpenSoldDialogFires()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);
        vm.SelectedItem = vm.FilteredCards[0]; // ListingPrice = 25

        vm.OpenSoldDialogCommand.Execute(null);

        Assert.True(vm.ShowSoldDialog);
        Assert.Equal(25m, vm.SoldSalePrice);
        Assert.Equal("Whatnot", vm.SoldPlatform);
    }

    [Fact]
    public async Task Should_RecomputeFeesAndProfit_When_SoldSalePriceChanges()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);
        vm.SelectedItem = vm.FilteredCards[0]; // CostBasis = 5

        vm.SoldShippingCost = 1m;
        vm.SoldSalePrice = 100m; // triggers OnSoldSalePriceChanged

        Assert.Equal(11.30m, vm.SoldFees); // 100 * 0.11 + 0.30
        Assert.Equal(100m - 5m - 11.30m - 1m, vm.SoldNetProfit);
    }

    [Fact]
    public async Task Should_PersistSoldStateAndUpdateCard_When_ConfirmSoldFires()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);
        vm.SelectedItem = vm.FilteredCards[0];
        vm.SoldSalePrice = 50m;

        await vm.ConfirmSoldCommand.ExecuteAsync(null);

        await repo.Received(1).UpdateCardAsync(Arg.Is<Card>(c =>
            c.SalePrice == 50m && c.Status == CardStatus.Sold));
        Assert.False(vm.ShowSoldDialog);
    }

    // === Reprice ===

    [Fact]
    public async Task Should_ResetCardToDraftAndClearPricing_When_RepriceSelectedFires()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);
        vm.SelectedItem = vm.FilteredCards[0]; // Ready, ListingPrice 25

        await vm.RepriceSelectedCommand.ExecuteAsync(null);

        await repo.Received(1).UpdateCardAsync(Arg.Is<Card>(c =>
            c.Status == CardStatus.Draft && c.ListingPrice == null && c.PriceSource == null));
    }

    // === Selection ===

    [Fact]
    public async Task Should_SelectAll_When_SelectAllFires()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);

        vm.SelectAllCommand.Execute(null);

        Assert.Equal(3, vm.SelectedCount);
        Assert.All(vm.FilteredCards, c => Assert.True(c.IsSelected));
    }

    [Fact]
    public async Task Should_DeselectAll_When_DeselectAllFires()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);
        foreach (var c in vm.FilteredCards) c.IsSelected = true;

        vm.DeselectAllCommand.Execute(null);

        Assert.Equal(0, vm.SelectedCount);
        Assert.All(vm.FilteredCards, c => Assert.False(c.IsSelected));
    }

    // === Export ===

    [Fact]
    public async Task Should_ErrorWhenNoSelection_When_ExportFires()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);

        await vm.ExportSelectedCsvCommand.ExecuteAsync(null);

        Assert.Contains("No cards selected", vm.ExportError);
    }
}
