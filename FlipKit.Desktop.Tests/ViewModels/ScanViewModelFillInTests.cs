using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Scanning;
using FlipKit.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Desktop.Tests.ViewModels;

/// <summary>
/// Phase 4e gap-fill — covers ScanWithAutoRotationAsync, the auto-mode model
/// rotation path. Original Phase 4c tests covered the explicit-model path;
/// this finishes the auto path including paid-consent fallback.
/// </summary>
public class ScanViewModelFillInTests
{
    private static ScanViewModel Create(
        IScannerService scanner,
        IOpenRouterModelCatalog catalog,
        IPaidModelConsentService consent,
        ISettingsService? settings = null)
    {
        settings ??= Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings
        {
            EnableVariationVerification = false,
            CustomGradingCompanies = new List<string>(),
        });
        return new ScanViewModel(
            scanner,
            Substitute.For<ICardRepository>(),
            Substitute.For<IFileDialogService>(),
            settings,
            Substitute.For<IVariationVerifier>(),
            Substitute.For<IChecklistLearningService>(),
            Substitute.For<IChecklistVerificationMatcher>(),
            catalog,
            consent,
            Substitute.For<IImageUploadService>(),
            Substitute.For<IBrowserService>(),
            NullLogger<ScanViewModel>.Instance);
    }

    private static OpenRouterModel FreeModel(string id) => new(id, id, IsFree: true, 0m, 0m, null, "");
    private static OpenRouterModel PaidModel(string id) => new(id, id, IsFree: false, 1m, 5m, null, "");

    private static ScanResult ScanResultFor(string player) => new()
    {
        Card = new Card { PlayerName = player, Year = 2026 },
        VisualCues = null,
        AllVisibleText = new(),
        Confidences = new(),
    };

    [Fact]
    public async Task Should_TryEachFreeModelInOrder_When_AutoRotationFirstFails()
    {
        var scanner = Substitute.For<IScannerService>();
        scanner.ScanCardAsync("/tmp/x.jpg", null, "free-1", Arg.Any<XimilarScanMode>())
               .Returns<ScanResult>(_ => throw new Exception("model 1 down"));
        scanner.ScanCardAsync("/tmp/x.jpg", null, "free-2", Arg.Any<XimilarScanMode>())
               .Returns(ScanResultFor("Mike Trout"));

        var catalog = Substitute.For<IOpenRouterModelCatalog>();
        catalog.GetAsync(default).ReturnsForAnyArgs(Task.FromResult(new ModelCatalog(
            new[] { FreeModel("free-1"), FreeModel("free-2") },
            Array.Empty<OpenRouterModel>(), DateTime.UtcNow)));

        var vm = Create(scanner, catalog, Substitute.For<IPaidModelConsentService>());
        for (int i = 0; i < 20 && vm.IsLoadingModels; i++) await Task.Delay(10);
        vm.ImagePath = "/tmp/x.jpg";
        vm.SelectedModel = ModelOption.Auto(); // explicitly Auto

        await vm.ScanCardCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ScannedCard);
        Assert.Equal("Mike Trout", vm.ScannedCard!.PlayerName);
        // Both free models attempted (first failed, second succeeded).
        await scanner.Received(1).ScanCardAsync("/tmp/x.jpg", null, "free-1", Arg.Any<XimilarScanMode>());
        await scanner.Received(1).ScanCardAsync("/tmp/x.jpg", null, "free-2", Arg.Any<XimilarScanMode>());
    }

    [Fact]
    public async Task Should_AskForPaidConsentAndUsePaidModel_When_AllFreeModelsFail()
    {
        var scanner = Substitute.For<IScannerService>();
        scanner.ScanCardAsync(Arg.Any<string>(), Arg.Any<string?>(), "free-1", Arg.Any<XimilarScanMode>())
               .Returns<ScanResult>(_ => throw new Exception("free down"));
        scanner.ScanCardAsync(Arg.Any<string>(), Arg.Any<string?>(), "paid-1", Arg.Any<XimilarScanMode>())
               .Returns(ScanResultFor("Paid Result"));

        var catalog = Substitute.For<IOpenRouterModelCatalog>();
        catalog.GetAsync(default).ReturnsForAnyArgs(Task.FromResult(new ModelCatalog(
            new[] { FreeModel("free-1") },
            new[] { PaidModel("paid-1") }, DateTime.UtcNow)));

        var consent = Substitute.For<IPaidModelConsentService>();
        consent.AskAsync(Arg.Any<OpenRouterModel>(), Arg.Any<string>()).Returns(true);

        var vm = Create(scanner, catalog, consent);
        for (int i = 0; i < 20 && vm.IsLoadingModels; i++) await Task.Delay(10);
        vm.ImagePath = "/tmp/x.jpg";
        vm.SelectedModel = ModelOption.Auto();

        await vm.ScanCardCommand.ExecuteAsync(null);

        Assert.Equal("Paid Result", vm.ScannedCard!.PlayerName);
        await consent.Received(1).AskAsync(Arg.Any<OpenRouterModel>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Should_SetCanceledMessage_When_UserDeclinesPaidConsent()
    {
        var scanner = Substitute.For<IScannerService>();
        scanner.ScanCardAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<XimilarScanMode>())
               .Returns<ScanResult>(_ => throw new Exception("free down"));

        var catalog = Substitute.For<IOpenRouterModelCatalog>();
        catalog.GetAsync(default).ReturnsForAnyArgs(Task.FromResult(new ModelCatalog(
            new[] { FreeModel("free-1") },
            new[] { PaidModel("paid-1") }, DateTime.UtcNow)));

        var consent = Substitute.For<IPaidModelConsentService>();
        consent.AskAsync(Arg.Any<OpenRouterModel>(), Arg.Any<string>()).Returns(false); // user says no

        var vm = Create(scanner, catalog, consent);
        for (int i = 0; i < 20 && vm.IsLoadingModels; i++) await Task.Delay(10);
        vm.ImagePath = "/tmp/x.jpg";
        vm.SelectedModel = ModelOption.Auto();

        await vm.ScanCardCommand.ExecuteAsync(null);

        Assert.Null(vm.ScannedCard); // no scan performed
        Assert.Contains("canceled", vm.SuccessMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_SetErrorMessage_When_AutoRotationCatalogIsEmpty()
    {
        var scanner = Substitute.For<IScannerService>();

        var catalog = Substitute.For<IOpenRouterModelCatalog>();
        catalog.GetAsync(default).ReturnsForAnyArgs(Task.FromResult(new ModelCatalog(
            Array.Empty<OpenRouterModel>(), Array.Empty<OpenRouterModel>(), DateTime.UtcNow)));

        var vm = Create(scanner, catalog, Substitute.For<IPaidModelConsentService>());
        for (int i = 0; i < 20 && vm.IsLoadingModels; i++) await Task.Delay(10);
        vm.ImagePath = "/tmp/x.jpg";
        vm.SelectedModel = ModelOption.Auto();

        await vm.ScanCardCommand.ExecuteAsync(null);

        Assert.Null(vm.ScannedCard);
        Assert.Contains("No OpenRouter models", vm.ErrorMessage);
    }

    [Fact]
    public async Task Should_SetErrorWithLastException_When_AllFreeFailedAndNoPaidAvailable()
    {
        var scanner = Substitute.For<IScannerService>();
        scanner.ScanCardAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<XimilarScanMode>())
               .Returns<ScanResult>(_ => throw new Exception("upstream down"));

        var catalog = Substitute.For<IOpenRouterModelCatalog>();
        catalog.GetAsync(default).ReturnsForAnyArgs(Task.FromResult(new ModelCatalog(
            new[] { FreeModel("free-1") },
            Array.Empty<OpenRouterModel>(), // no paid models
            DateTime.UtcNow)));

        var vm = Create(scanner, catalog, Substitute.For<IPaidModelConsentService>());
        for (int i = 0; i < 20 && vm.IsLoadingModels; i++) await Task.Delay(10);
        vm.ImagePath = "/tmp/x.jpg";
        vm.SelectedModel = ModelOption.Auto();

        await vm.ScanCardCommand.ExecuteAsync(null);

        Assert.Contains("All free models failed", vm.ErrorMessage);
        Assert.Contains("upstream down", vm.ErrorMessage);
    }
}
