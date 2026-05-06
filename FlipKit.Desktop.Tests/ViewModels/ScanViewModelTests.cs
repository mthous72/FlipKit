using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Scanning;
using FlipKit.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Desktop.Tests.ViewModels;

public class ScanViewModelTests
{
    private static ScanViewModel Create(
        IScannerService? scanner = null,
        ICardRepository? repo = null,
        IFileDialogService? dialog = null,
        ISettingsService? settings = null,
        IVariationVerifier? verifier = null,
        IChecklistLearningService? learning = null,
        IOpenRouterModelCatalog? catalog = null,
        IPaidModelConsentService? consent = null,
        IAiScanConsentService? aiConsent = null,
        IImageUploadService? upload = null)
    {
        settings ??= Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings
        {
            EnableVariationVerification = false, // off by default to keep tests focused
            AiScanConsentGiven = true,           // bypass consent gate in non-consent tests
            CustomGradingCompanies = new List<string>(),
        });

        catalog ??= Substitute.For<IOpenRouterModelCatalog>();
        catalog.GetAsync(default).ReturnsForAnyArgs(Task.FromResult(
            new ModelCatalog(Array.Empty<OpenRouterModel>(), Array.Empty<OpenRouterModel>(), DateTime.UtcNow)));

        return new ScanViewModel(
            scanner ?? Substitute.For<IScannerService>(),
            repo ?? Substitute.For<ICardRepository>(),
            dialog ?? Substitute.For<IFileDialogService>(),
            settings,
            verifier ?? Substitute.For<IVariationVerifier>(),
            learning ?? Substitute.For<IChecklistLearningService>(),
            Substitute.For<IChecklistVerificationMatcher>(),
            catalog,
            consent ?? Substitute.For<IPaidModelConsentService>(),
            aiConsent ?? Substitute.For<IAiScanConsentService>(),
            upload ?? Substitute.For<IImageUploadService>(),
            Substitute.For<IBrowserService>(),
            Substitute.For<IWebcamCaptureDialogService>(),
            NullLogger<ScanViewModel>.Instance);
    }

    private static ScanResult ScanResultFor(string playerName) => new()
    {
        Card = new Card { PlayerName = playerName, Year = 2026, Brand = "Bowman" },
        VisualCues = null,
        AllVisibleText = new(),
        Confidences = new(),
    };

    // === Image picker commands ===

    [Fact]
    public async Task Should_SetImagePathFromDialog_When_BrowseImageFires()
    {
        var dialog = Substitute.For<IFileDialogService>();
        dialog.OpenImageFileAsync().Returns("/tmp/front.jpg");
        var vm = Create(dialog: dialog);

        await vm.BrowseImageCommand.ExecuteAsync(null);

        Assert.Equal("/tmp/front.jpg", vm.ImagePath);
    }

    [Fact]
    public async Task Should_SetBackImagePath_When_BrowseBackImageFires()
    {
        var dialog = Substitute.For<IFileDialogService>();
        dialog.OpenImageFileAsync().Returns("/tmp/back.jpg");
        var vm = Create(dialog: dialog);

        await vm.BrowseBackImageCommand.ExecuteAsync(null);

        Assert.Equal("/tmp/back.jpg", vm.ImagePathBack);
    }

    [Fact]
    public void Should_ClearBackImagePath_When_RemoveBackImageFires()
    {
        var vm = Create();
        vm.ImagePathBack = "/tmp/back.jpg";

        vm.RemoveBackImageCommand.Execute(null);

        Assert.Null(vm.ImagePathBack);
    }

    [Fact]
    public async Task Should_AddPhoto_When_AddAdditionalPhotoFires()
    {
        var dialog = Substitute.For<IFileDialogService>();
        dialog.OpenImageFileAsync().Returns("/tmp/extra.jpg");
        var vm = Create(dialog: dialog);

        await vm.AddAdditionalPhotoCommand.ExecuteAsync(null);

        Assert.Single(vm.AdditionalPhotos);
    }

    [Fact]
    public async Task Should_NotExceedMaxAdditionalPhotos_When_AddCommandFiresRepeatedly()
    {
        var dialog = Substitute.For<IFileDialogService>();
        dialog.OpenImageFileAsync().Returns("/tmp/x.jpg");
        var vm = Create(dialog: dialog);
        for (int i = 0; i < ScanViewModel.MaxAdditionalPhotos; i++)
            vm.AdditionalPhotos.Add(new PhotoSlot($"/tmp/{i}.jpg"));

        await vm.AddAdditionalPhotoCommand.ExecuteAsync(null);

        Assert.Equal(ScanViewModel.MaxAdditionalPhotos, vm.AdditionalPhotos.Count);
    }

    // === ScanCardAsync ===

    [Fact]
    public async Task Should_DoNothing_When_ScanFiresWithoutImage()
    {
        var scanner = Substitute.For<IScannerService>();
        var vm = Create(scanner: scanner);

        await vm.ScanCardCommand.ExecuteAsync(null);

        await scanner.DidNotReceiveWithAnyArgs().ScanCardAsync(default!);
    }

    [Fact]
    public async Task Should_PopulateScannedCard_When_ScanSucceeds()
    {
        var scanner = Substitute.For<IScannerService>();
        scanner.ScanCardAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<XimilarScanMode>())
               .Returns(ScanResultFor("Mike Trout"));
        var vm = Create(scanner: scanner);
        vm.ImagePath = "/tmp/front.jpg";
        // Use auto-rotation path = SelectedModel == null. Need a paid+free model in catalog
        // for auto to attempt anything. With the empty catalog default, auto path returns null.
        // Force the explicit-model path by setting SelectedModel.
        var modelOption = ModelOption.FromCatalog(new OpenRouterModel(
            "test/model", "Test", IsFree: true, 0m, 0m, null, ""));
        vm.SelectedModel = modelOption;

        await vm.ScanCardCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ScannedCard);
        Assert.Equal("Mike Trout", vm.ScannedCard!.PlayerName);
        Assert.False(vm.IsScanning);
    }

    [Fact]
    public async Task Should_SurfaceErrorMessage_When_ScanThrows()
    {
        var scanner = Substitute.For<IScannerService>();
        scanner.ScanCardAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<XimilarScanMode>())
               .Returns<ScanResult>(_ => throw new Exception("model down"));
        var vm = Create(scanner: scanner);
        vm.ImagePath = "/tmp/front.jpg";
        vm.SelectedModel = ModelOption.FromCatalog(new OpenRouterModel(
            "test/model", "Test", IsFree: true, 0m, 0m, null, ""));

        await vm.ScanCardCommand.ExecuteAsync(null);

        Assert.Contains("Scan failed", vm.ErrorMessage);
    }

    // === Manual entry ===

    [Fact]
    public void Should_ProvideEmptyScannedCard_When_EnterManuallyFires()
    {
        var vm = Create();

        vm.EnterManuallyCommand.Execute(null);

        Assert.NotNull(vm.ScannedCard);
        Assert.Equal(string.Empty, vm.ScannedCard!.PlayerName);
    }

    // === Save flow ===

    [Fact]
    public async Task Should_DoNothing_When_SaveFiresWithoutScannedCard()
    {
        var repo = Substitute.For<ICardRepository>();
        var vm = Create(repo: repo);

        await vm.SaveCardCommand.ExecuteAsync(null);

        await repo.DidNotReceive().InsertCardAsync(Arg.Any<Card>());
    }

    [Fact]
    public async Task Should_InsertCardAndClear_When_SaveSucceeds()
    {
        var repo = Substitute.For<ICardRepository>();
        var vm = Create(repo: repo);
        vm.ImagePath = "/tmp/front.jpg";
        vm.ScannedCard = CardDetailViewModel.FromCard(new Card { PlayerName = "Test", Year = 2026, Brand = "X" });

        await vm.SaveCardCommand.ExecuteAsync(null);

        await repo.Received(1).InsertCardAsync(Arg.Is<Card>(c => c.PlayerName == "Test"));
        Assert.Contains("Saved Test", vm.SuccessMessage);
        Assert.Null(vm.ScannedCard); // cleared
    }

    [Fact]
    public async Task Should_PersistCustomGradingCompany_When_SavingNewCustomGrader()
    {
        var settings = Substitute.For<ISettingsService>();
        var settingsState = new AppSettings { CustomGradingCompanies = new List<string>() };
        settings.Load().Returns(settingsState);
        var repo = Substitute.For<ICardRepository>();
        var vm = Create(repo: repo, settings: settings);
        var card = new Card { PlayerName = "Graded", IsGraded = true, GradeCompany = "MyCustomGrader" };
        vm.ScannedCard = CardDetailViewModel.FromCard(card);

        await vm.SaveCardCommand.ExecuteAsync(null);

        // The new custom grader should be saved back to settings.
        settings.Received().Save(Arg.Is<AppSettings>(s =>
            s.CustomGradingCompanies.Contains("MyCustomGrader")));
    }

    // === Suggestion handlers ===

    [Fact]
    public void Should_ApplyPlayerNameSuggestion_When_AcceptSuggestionMentionsPlayerName()
    {
        var vm = Create();
        vm.ScannedCard = new CardDetailViewModel { PlayerName = "Wrong" };
        vm.VerificationResult = new VerificationResult
        {
            SuggestedPlayerName = "Correct",
            Suggestions = { "Player name mismatch — accept correction?" },
        };

        vm.AcceptSuggestionCommand.Execute("Player name mismatch — accept correction?");

        Assert.Equal("Correct", vm.ScannedCard.PlayerName);
        Assert.Empty(vm.VerificationResult.Suggestions);
    }

    [Fact]
    public void Should_RemoveSuggestionWithoutChange_When_IgnoreSuggestionFires()
    {
        var vm = Create();
        vm.ScannedCard = new CardDetailViewModel { PlayerName = "X" };
        vm.VerificationResult = new VerificationResult
        {
            SuggestedPlayerName = "Should not apply",
            Suggestions = { "Player name suggestion" },
        };

        vm.IgnoreSuggestionCommand.Execute("Player name suggestion");

        Assert.Equal("X", vm.ScannedCard.PlayerName); // unchanged
        Assert.Empty(vm.VerificationResult.Suggestions);
    }

    // === Clear ===

    [Fact]
    public void Should_ResetEverything_When_ClearFires()
    {
        var vm = Create();
        vm.ImagePath = "/tmp/x.jpg";
        vm.ImagePathBack = "/tmp/y.jpg";
        vm.AdditionalPhotos.Add(new PhotoSlot("/tmp/z.jpg"));
        vm.ScannedCard = new CardDetailViewModel();
        vm.ErrorMessage = "err";
        vm.VerificationResult = new VerificationResult();
        vm.VerificationStatus = "status";

        vm.ClearCommand.Execute(null);

        Assert.Null(vm.ImagePath);
        Assert.Null(vm.ImagePathBack);
        Assert.Empty(vm.AdditionalPhotos);
        Assert.Null(vm.ScannedCard);
        Assert.Null(vm.ErrorMessage);
        Assert.Null(vm.VerificationResult);
        Assert.Equal(string.Empty, vm.VerificationStatus);
    }
}
