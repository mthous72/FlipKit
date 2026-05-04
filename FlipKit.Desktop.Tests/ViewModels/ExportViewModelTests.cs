using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Export;
using FlipKit.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Desktop.Tests.ViewModels;

public class ExportViewModelTests
{
    private static ExportViewModel Create(
        ICardRepository? repo = null,
        IExportService? export = null,
        IFileDialogService? dialog = null,
        IBrowserService? browser = null,
        ISettingsService? settings = null)
    {
        settings ??= Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { ActiveExportPlatform = ExportPlatform.Whatnot });
        return new ExportViewModel(
            repo ?? Substitute.For<ICardRepository>(),
            export ?? Substitute.For<IExportService>(),
            dialog ?? Substitute.For<IFileDialogService>(),
            browser ?? Substitute.For<IBrowserService>(),
            settings,
            NullLogger<ExportViewModel>.Instance);
    }

    private static List<Card> Sample() => new()
    {
        new Card { Id = 1, PlayerName = "ReadyA", Status = CardStatus.Ready, Sport = Sport.Baseball, ListingPrice = 10m, CreatedAt = DateTime.UtcNow.AddDays(-1) },
        new Card { Id = 2, PlayerName = "ListedB", Status = CardStatus.Listed, Sport = Sport.Football, ListingPrice = 25m, CreatedAt = DateTime.UtcNow.AddDays(-2) },
        new Card { Id = 3, PlayerName = "DraftC", Status = CardStatus.Draft, Sport = Sport.Baseball, ListingPrice = 5m, CreatedAt = DateTime.UtcNow.AddDays(-3) },
        new Card { Id = 4, PlayerName = "SoldD", Status = CardStatus.Sold, Sport = Sport.Basketball, SalePrice = 50m, CreatedAt = DateTime.UtcNow.AddDays(-4) },
    };

    private static async Task<ExportViewModel> CreateAndWait(ICardRepository repo, IExportService? export = null, IBrowserService? browser = null)
    {
        var vm = Create(repo: repo, export: export, browser: browser);
        for (int i = 0; i < 20 && vm.Items.Count == 0; i++) await Task.Delay(10);
        return vm;
    }

    // === Initial load + default filters ===

    [Fact]
    public async Task Should_OnlyShowReadyAndListedByDefault_When_Loaded()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);

        // Default: ShowReady=true, ShowListed=true, ShowDraft=false, ShowSold=false.
        Assert.Equal(2, vm.Items.Count);
        Assert.All(vm.Items, i => Assert.Contains(i.Card.Status, new[] { CardStatus.Ready, CardStatus.Listed }));
    }

    [Fact]
    public async Task Should_PreSelectReadyCards_When_Loaded()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);

        // Ready items are auto-selected; Listed are not.
        var readyItem = vm.Items.First(i => i.Card.PlayerName == "ReadyA");
        var listedItem = vm.Items.First(i => i.Card.PlayerName == "ListedB");
        Assert.True(readyItem.IsSelected);
        Assert.False(listedItem.IsSelected);
    }

    [Fact]
    public async Task Should_SortNewestFirst_When_Loaded()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);

        Assert.Equal("ReadyA", vm.Items[0].Card.PlayerName); // newest CreatedAt
    }

    // === Filter reactivity ===

    [Fact]
    public async Task Should_AddDraftsToVisibleList_When_ShowDraftToggled()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);

        vm.ShowDraft = true;

        Assert.Equal(3, vm.Items.Count); // Ready + Listed + Draft
    }

    [Fact]
    public async Task Should_FilterBySport_When_SportFilterChanged()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);

        vm.SportFilter = "Baseball";

        Assert.Single(vm.Items); // only ReadyA matches
    }

    [Fact]
    public async Task Should_FilterByPlayerName_When_SearchTextSet()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);

        vm.SearchText = "Listed";

        Assert.Single(vm.Items);
        Assert.Equal("ListedB", vm.Items[0].Card.PlayerName);
    }

    [Fact]
    public async Task Should_ResetAllFilters_When_ClearFiltersFires()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);
        vm.ShowDraft = true;
        vm.SportFilter = "Baseball";
        vm.SearchText = "Ready";

        vm.ClearFiltersCommand.Execute(null);

        Assert.True(vm.ShowReady);
        Assert.False(vm.ShowDraft);
        Assert.Equal(ExportViewModel.AllSportsLabel, vm.SportFilter);
        Assert.Equal(string.Empty, vm.SearchText);
    }

    // === Selection commands ===

    [Fact]
    public async Task Should_SelectAllVisible_When_CommandFires()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);
        foreach (var i in vm.Items) i.IsSelected = false;

        vm.SelectAllVisibleCommand.Execute(null);

        Assert.All(vm.Items, i => Assert.True(i.IsSelected));
    }

    [Fact]
    public async Task Should_DeselectAllVisible_When_SelectNoneFires()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);
        foreach (var i in vm.Items) i.IsSelected = true;

        vm.SelectNoneCommand.Execute(null);

        Assert.All(vm.Items, i => Assert.False(i.IsSelected));
    }

    // === Export ===

    [Fact]
    public async Task Should_ErrorWhenNothingSelected_When_ExportFires()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo);
        foreach (var i in vm.Items) i.IsSelected = false;

        await vm.ExportCsvCommand.ExecuteAsync(null);

        Assert.Contains("Pick at least one", vm.ErrorMessage);
    }

    [Fact]
    public async Task Should_BlockExport_When_ValidatorReturnsErrors()
    {
        var export = Substitute.For<IExportService>();
        export.ValidateBatch(Arg.Any<IList<Card>>(), Arg.Any<ExportPlatform>()).Returns(
            new List<ExportRowError>
            {
                new(1, "ReadyA", "PlayerName", "missing", ExportErrorSeverity.Error),
            });
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = await CreateAndWait(repo, export: export);

        await vm.ExportCsvCommand.ExecuteAsync(null);

        Assert.Contains("Export blocked by 1", vm.ErrorMessage);
        Assert.True(vm.HasRowErrors);
    }

    [Fact]
    public async Task Should_PromoteReadyToListedAfterExport_When_FileWritten()
    {
        var dialog = Substitute.For<IFileDialogService>();
        dialog.SaveCsvFileAsync(Arg.Any<string>()).Returns("/tmp/export.csv");
        var export = Substitute.For<IExportService>();
        export.ValidateBatch(Arg.Any<IList<Card>>(), Arg.Any<ExportPlatform>()).Returns(new List<ExportRowError>());
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = Create(repo: repo, export: export, dialog: dialog);
        for (int i = 0; i < 20 && vm.Items.Count == 0; i++) await Task.Delay(10);

        await vm.ExportCsvCommand.ExecuteAsync(null);

        await export.Received(1).ExportCsvAsync(Arg.Any<List<Card>>(), "/tmp/export.csv", ExportPlatform.Whatnot);
        // Ready card was promoted to Listed and persisted back.
        await repo.Received().UpdateCardAsync(Arg.Is<Card>(c => c.Status == CardStatus.Listed && c.PlayerName == "ReadyA"));
        Assert.Contains("Exported 1 cards", vm.StatusMessage);
    }

    [Fact]
    public async Task Should_AbortExport_When_UserCancelsSaveDialog()
    {
        var dialog = Substitute.For<IFileDialogService>();
        dialog.SaveCsvFileAsync(Arg.Any<string>()).Returns((string?)null);
        var export = Substitute.For<IExportService>();
        export.ValidateBatch(Arg.Any<IList<Card>>(), Arg.Any<ExportPlatform>()).Returns(new List<ExportRowError>());
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var vm = Create(repo: repo, export: export, dialog: dialog);
        for (int i = 0; i < 20 && vm.Items.Count == 0; i++) await Task.Delay(10);

        await vm.ExportCsvCommand.ExecuteAsync(null);

        await export.DidNotReceive().ExportCsvAsync(Arg.Any<List<Card>>(), Arg.Any<string>(), Arg.Any<ExportPlatform>());
    }

    [Fact]
    public async Task Should_OpenWhatnotSellerHubUrl_When_CommandFires()
    {
        var browser = Substitute.For<IBrowserService>();
        var vm = Create(browser: browser);

        vm.OpenWhatnotSellerHubCommand.Execute(null);

        browser.Received(1).OpenUrl(Arg.Is<string>(u => u.Contains("whatnot")));
    }
}
