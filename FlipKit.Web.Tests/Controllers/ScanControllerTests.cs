using System.Text.Json;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Scanning;
using FlipKit.Web.Controllers;
using FlipKit.Web.Models;
using FlipKit.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Web.Tests.Controllers;

/// <summary>
/// ScanController is one of the largest in the project (~500 lines) and depends on
/// HttpContext.Session for several actions. We test the deterministic non-session paths:
/// Upload validation, Save / Discard / Results from TempData, ResearchComps. Session-
/// based Index is exercised indirectly via the integration smoke test (later in 4d).
/// </summary>
public class ScanControllerTests
{
    private static ScanController Create(
        IScannerService? scanner = null,
        ICardRepository? repo = null,
        IVariationVerifier? verifier = null,
        ISettingsService? settings = null,
        IOpenRouterModelCatalog? catalog = null,
        IImageUploadService? upload = null,
        IPricerService? pricer = null)
    {
        var env = Substitute.For<IWebHostEnvironment>();
        env.WebRootPath.Returns(Path.Combine(Path.GetTempPath(), "flipkit-test-scan"));
        var controller = new ScanController(
            scanner ?? Substitute.For<IScannerService>(),
            repo ?? Substitute.For<ICardRepository>(),
            verifier ?? Substitute.For<IVariationVerifier>(),
            settings ?? Substitute.For<ISettingsService>(),
            catalog ?? Substitute.For<IOpenRouterModelCatalog>(),
            upload ?? Substitute.For<IImageUploadService>(),
            NullLogger<ScanController>.Instance,
            env,
            pricer ?? Substitute.For<IPricerService>());
        TempDataHelper.Attach(controller);
        return controller;
    }

    // === Upload validation ===

    [Fact]
    public async Task Should_RedirectWithError_When_UploadCalledWithoutFrontImage()
    {
        var controller = Create();

        var result = await controller.Upload(frontImage: null, backImage: null, selectedModel: null, frontImagePath: null, backImagePath: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Contains("front image", controller.TempData["ErrorMessage"]!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // === Results action ===

    [Fact]
    public void Should_RedirectToIndex_When_ResultsCalledWithoutScanResultInTempData()
    {
        var controller = Create();

        var result = controller.Results();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
    }

    [Fact]
    public void Should_ReturnResultsView_When_ScanResultIsInTempData()
    {
        var controller = Create();
        var scanVm = new ScanResultViewModel
        {
            ScannedCard = new Card { PlayerName = "Mike Trout" },
            FrontImagePath = "/tmp/x.jpg",
        };
        controller.TempData["ScanResult"] = JsonSerializer.Serialize(scanVm);

        var result = controller.Results();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ScanResultViewModel>(view.Model);
        Assert.Equal("Mike Trout", model.ScannedCard!.PlayerName);
    }

    // === Save action ===

    [Fact]
    public async Task Should_RedirectWithError_When_SaveCalledWithoutScanResultInTempData()
    {
        var controller = Create();

        var result = await controller.Save(null, null, null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Contains("expired", controller.TempData["ErrorMessage"]!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_InsertCardAndRedirectToInventory_When_SaveSucceeds()
    {
        var repo = Substitute.For<ICardRepository>();
        var controller = Create(repo: repo);
        var scanVm = new ScanResultViewModel
        {
            ScannedCard = new Card { PlayerName = "Mike", VariationType = "Base", Condition = "Near Mint" },
            FrontImagePath = "/tmp/front.jpg",
        };
        controller.TempData["ScanResult"] = JsonSerializer.Serialize(scanVm);

        var result = await controller.Save(estimatedValue: 50m, listingPrice: 45m, costBasis: 5m);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Inventory", redirect.ControllerName);
        await repo.Received(1).InsertCardAsync(Arg.Is<Card>(c =>
            c.EstimatedValue == 50m && c.ListingPrice == 45m && c.CostBasis == 5m));
    }

    [Fact]
    public async Task Should_FillDefaultsForNullFields_When_SavingScannedCard()
    {
        var repo = Substitute.For<ICardRepository>();
        var controller = Create(repo: repo);
        var scanVm = new ScanResultViewModel
        {
            ScannedCard = new Card { PlayerName = "", VariationType = "", Condition = "" },
            FrontImagePath = "/tmp/front.jpg",
        };
        controller.TempData["ScanResult"] = JsonSerializer.Serialize(scanVm);

        await controller.Save(null, null, null);

        await repo.Received(1).InsertCardAsync(Arg.Is<Card>(c =>
            c.PlayerName == "Unknown" && c.VariationType == "Base" && c.Condition == "Near Mint"));
    }

    // === Discard ===

    [Fact]
    public void Should_SetSuccessAndRedirect_When_DiscardCalled()
    {
        var controller = Create();

        var result = controller.Discard();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Contains("discarded", controller.TempData["SuccessMessage"]!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Should_NotThrow_When_DiscardCalledWithMalformedScanResult()
    {
        var controller = Create();
        controller.TempData["ScanResult"] = "{ not valid json"; // bad data — discard should swallow

        var result = controller.Discard();

        Assert.IsType<RedirectToActionResult>(result);
    }

    // === ResearchComps ===

    [Fact]
    public void Should_RedirectWithError_When_ResearchCompsCalledWithoutCard()
    {
        var controller = Create();

        var result = controller.ResearchComps(null!, null, null, null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
    }

    [Fact]
    public void Should_BuildResearchView_When_ResearchCompsCalledWithCard()
    {
        var pricer = Substitute.For<IPricerService>();
        pricer.BuildTerapeakUrl(Arg.Any<Card>()).Returns("https://t/x");
        pricer.BuildEbaySoldUrl(Arg.Any<Card>()).Returns("https://e/x");
        var controller = Create(pricer: pricer);

        var card = new Card { PlayerName = "Mike", Year = 2026 };
        var result = controller.ResearchComps(card, "/tmp/front.jpg", null, "buying");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ScanResearchViewModel>(view.Model);
        Assert.Equal("https://t/x", model.TerapeakUrl);
        Assert.Equal("https://e/x", model.EbaySoldUrl);
        Assert.Equal("buying", model.ScanMode);
    }

    // === SaveAndResearch ===

    [Fact]
    public async Task Should_RedirectWithError_When_SaveAndResearchHasNoTempData()
    {
        var controller = Create();

        var result = await controller.SaveAndResearch();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
    }

    [Fact]
    public async Task Should_InsertCardAndRedirectToPricingResearch_When_SaveAndResearchSucceeds()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.InsertCardAsync(Arg.Any<Card>()).Returns(Task.FromResult(42));
        var controller = Create(repo: repo);
        var scanVm = new ScanResultViewModel
        {
            ScannedCard = new Card { PlayerName = "Mike", VariationType = "Base", Condition = "Near Mint" },
            FrontImagePath = "/tmp/front.jpg",
        };
        controller.TempData["ScanResult"] = JsonSerializer.Serialize(scanVm);

        var result = await controller.SaveAndResearch();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Research", redirect.ActionName);
        Assert.Equal("Pricing", redirect.ControllerName);
        await repo.Received(1).InsertCardAsync(Arg.Any<Card>());
    }
}
