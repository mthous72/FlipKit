using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Scanning;
using FlipKit.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Desktop.Tests.ViewModels;

public class BulkScanViewModelTests
{
    private static BulkScanViewModel Create(
        IScannerService? scanner = null,
        ICardRepository? repo = null,
        IFileDialogService? dialog = null,
        ISettingsService? settings = null,
        IVariationVerifier? verifier = null,
        IBulkScanErrorLogger? errorLogger = null,
        IOpenRouterModelCatalog? catalog = null,
        IPaidModelConsentService? consent = null,
        IImageUploadService? upload = null)
    {
        settings ??= Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings
        {
            EnableVariationVerification = false,
            MaxConcurrentScans = 1,
            CustomGradingCompanies = new List<string>(),
        });

        catalog ??= Substitute.For<IOpenRouterModelCatalog>();
        catalog.GetAsync(default).ReturnsForAnyArgs(Task.FromResult(
            new ModelCatalog(Array.Empty<OpenRouterModel>(), Array.Empty<OpenRouterModel>(), DateTime.UtcNow)));

        return new BulkScanViewModel(
            scanner ?? Substitute.For<IScannerService>(),
            repo ?? Substitute.For<ICardRepository>(),
            dialog ?? Substitute.For<IFileDialogService>(),
            settings,
            verifier ?? Substitute.For<IVariationVerifier>(),
            errorLogger ?? Substitute.For<IBulkScanErrorLogger>(),
            catalog,
            consent ?? Substitute.For<IPaidModelConsentService>(),
            upload ?? Substitute.For<IImageUploadService>(),
            NullLogger<BulkScanViewModel>.Instance);
    }

    // === Image picker (paired vs unpaired) ===

    [Fact]
    public async Task Should_PairConsecutiveImages_When_ImagesArePairsTrue()
    {
        // SelectImagesAsync sorts paths alphabetically before pairing — use names that
        // already sort into the desired pair order.
        var dialog = Substitute.For<IFileDialogService>();
        dialog.OpenImageFilesAsync().Returns(new List<string>
        {
            "/tmp/01.jpg", "/tmp/02.jpg",
            "/tmp/03.jpg", "/tmp/04.jpg",
        });
        using var vm = Create(dialog: dialog);
        vm.ImagesArePairs = true;

        await vm.SelectImagesCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Items.Count);
        Assert.Equal("/tmp/01.jpg", vm.Items[0].FrontImagePath);
        Assert.Equal("/tmp/02.jpg", vm.Items[0].BackImagePath);
        Assert.Equal("/tmp/03.jpg", vm.Items[1].FrontImagePath);
        Assert.Equal("/tmp/04.jpg", vm.Items[1].BackImagePath);
    }

    [Fact]
    public async Task Should_TreatEachAsSeparate_When_ImagesArePairsFalse()
    {
        var dialog = Substitute.For<IFileDialogService>();
        dialog.OpenImageFilesAsync().Returns(new List<string>
        {
            "/tmp/a.jpg", "/tmp/b.jpg", "/tmp/c.jpg",
        });
        using var vm = Create(dialog: dialog);
        vm.ImagesArePairs = false;

        await vm.SelectImagesCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Items.Count);
        Assert.All(vm.Items, item => Assert.Null(item.BackImagePath));
    }

    // === IsSelectedModelFree ===

    [Fact]
    public void Should_TreatAutoAsFree_When_NoModelSelected()
    {
        using var vm = Create();
        vm.SelectedModel = null;

        Assert.True(vm.IsSelectedModelFree);
    }

    [Fact]
    public void Should_TreatExplicitFreeModelAsFree_When_FreeSelected()
    {
        using var vm = Create();
        vm.SelectedModel = ModelOption.FromCatalog(
            new OpenRouterModel("google/gemma:free", "Gemma Free", IsFree: true, 0m, 0m, null, ""));

        Assert.True(vm.IsSelectedModelFree);
    }

    [Fact]
    public void Should_TreatPaidModelAsNotFree_When_PaidSelected()
    {
        using var vm = Create();
        vm.SelectedModel = ModelOption.FromCatalog(
            new OpenRouterModel("anthropic/claude", "Claude", IsFree: false, 3m, 15m, null, ""));

        Assert.False(vm.IsSelectedModelFree);
    }

    [Fact]
    public void Should_ForceConcurrencyToOne_When_SelectingFreeModel()
    {
        using var vm = Create();
        vm.MaxConcurrentScans = 4;

        vm.SelectedModel = ModelOption.FromCatalog(
            new OpenRouterModel("google/gemma:free", "Gemma Free", IsFree: true, 0m, 0m, null, ""));

        Assert.Equal(1, vm.MaxConcurrentScans);
    }

    // === Item management ===

    [Fact]
    public void Should_RemoveAndReindex_When_RemoveSelectedFires()
    {
        using var vm = Create();
        var item1 = new BulkScanItem { Index = 1, FrontImagePath = "/tmp/a.jpg" };
        var item2 = new BulkScanItem { Index = 2, FrontImagePath = "/tmp/b.jpg" };
        var item3 = new BulkScanItem { Index = 3, FrontImagePath = "/tmp/c.jpg" };
        vm.Items.Add(item1);
        vm.Items.Add(item2);
        vm.Items.Add(item3);
        vm.SelectedItem = item2;

        vm.RemoveSelectedCommand.Execute(null);

        Assert.Equal(2, vm.Items.Count);
        // Items 1 and 3 remain, but reindexed to 1 and 2.
        Assert.Equal(1, vm.Items[0].Index);
        Assert.Equal(2, vm.Items[1].Index);
    }

    [Fact]
    public void Should_ClearAllItems_When_ClearAllFires()
    {
        using var vm = Create();
        vm.Items.Add(new BulkScanItem { FrontImagePath = "/tmp/a.jpg" });
        vm.Items.Add(new BulkScanItem { FrontImagePath = "/tmp/b.jpg" });
        vm.ScanProgress = 5;
        vm.ScanTotal = 10;

        vm.ClearAllCommand.Execute(null);

        Assert.Empty(vm.Items);
        Assert.Null(vm.SelectedItem);
        Assert.Equal(0, vm.ScanProgress);
        Assert.Equal(0, vm.ScanTotal);
    }

    // === SelectedCard reflects SelectedItem ===

    [Fact]
    public void Should_ExposeSelectedCard_When_SelectedItemSet()
    {
        using var vm = Create();
        var item = new BulkScanItem
        {
            FrontImagePath = "/tmp/a.jpg",
            CardDetail = new CardDetailViewModel { PlayerName = "Test" },
        };
        vm.Items.Add(item);

        vm.SelectedItem = item;

        Assert.NotNull(vm.SelectedCard);
        Assert.Equal("Test", vm.SelectedCard!.PlayerName);
    }

    // === SaveAll ===

    [Fact]
    public async Task Should_SkipUnscannedItems_When_SaveAllFires()
    {
        var repo = Substitute.For<ICardRepository>();
        using var vm = Create(repo: repo);
        // Only one item is in Scanned state — others should be ignored.
        vm.Items.Add(new BulkScanItem
        {
            FrontImagePath = "/tmp/a.jpg",
            Status = BulkScanStatus.Scanned,
            CardDetail = CardDetailViewModel.FromCard(new Card { PlayerName = "Saved One" }),
        });
        vm.Items.Add(new BulkScanItem
        {
            FrontImagePath = "/tmp/b.jpg",
            Status = BulkScanStatus.Pending, // not scanned yet
        });

        await vm.SaveAllCommand.ExecuteAsync(null);

        await repo.Received(1).InsertCardAsync(Arg.Any<Card>());
        Assert.Equal(BulkScanStatus.Saved, vm.Items[0].Status);
        Assert.Equal(BulkScanStatus.Pending, vm.Items[1].Status); // unchanged
    }

    [Fact]
    public async Task Should_DoNothing_When_SaveAllWithNothingScanned()
    {
        var repo = Substitute.For<ICardRepository>();
        using var vm = Create(repo: repo);

        await vm.SaveAllCommand.ExecuteAsync(null);

        await repo.DidNotReceive().InsertCardAsync(Arg.Any<Card>());
    }

    // === Cancel ===

    [Fact]
    public void Should_TolerateCancelWithoutRunningScan_When_CommandFires()
    {
        // No-op when nothing is in flight; should not throw.
        using var vm = Create();
        vm.CancelScanCommand.Execute(null);
    }

    // === Dispose ===

    [Fact]
    public void Should_NotThrow_When_Disposed()
    {
        var vm = Create();
        vm.Dispose();
        vm.Dispose(); // double-dispose should also not throw
    }
}
