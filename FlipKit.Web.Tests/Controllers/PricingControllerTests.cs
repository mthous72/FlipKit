using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Web.Controllers;
using FlipKit.Web.Models;
using FlipKit.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Web.Tests.Controllers;

public class PricingControllerTests
{
    private static PricingController Create(
        ICardRepository? repo = null,
        IPricerService? pricer = null,
        IBrowserService? browser = null,
        bool attachTempData = true)
    {
        var controller = new PricingController(
            repo ?? Substitute.For<ICardRepository>(),
            pricer ?? Substitute.For<IPricerService>(),
            browser ?? Substitute.For<IBrowserService>(),
            NullLogger<PricingController>.Instance);

        if (attachTempData) TempDataHelper.Attach(controller);
        return controller;
    }

    private static List<Card> SampleCards() => new()
    {
        new Card { Id = 1, PlayerName = "Mike Trout", Status = CardStatus.Draft },
        new Card { Id = 2, PlayerName = "Aaron Judge", Status = CardStatus.Ready, ListingPrice = 25m },
        new Card { Id = 3, PlayerName = "No Price", Status = CardStatus.Ready, ListingPrice = null },
    };

    // === Index ===

    [Fact]
    public async Task Should_OnlyShowCardsThatNeedPricing_When_IndexCalled()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(SampleCards());
        var controller = Create(repo: repo);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PricingListViewModel>(view.Model);
        // Draft (Mike) + null-price Ready (No Price). Aaron Judge has a price → excluded.
        Assert.Equal(2, model.Cards.Count);
    }

    [Fact]
    public async Task Should_ReturnEmptyViewModel_When_IndexThrows()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns<List<Card>>(_ => throw new Exception("db down"));
        var controller = Create(repo: repo);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PricingListViewModel>(view.Model);
        Assert.Empty(model.Cards);
    }

    // === Research ===

    [Fact]
    public async Task Should_PopulateResearchView_When_CardExists()
    {
        var repo = Substitute.For<ICardRepository>();
        var card = new Card { Id = 5, PlayerName = "X", EstimatedValue = 50m };
        repo.GetCardAsync(5).Returns(card);
        var pricer = Substitute.For<IPricerService>();
        pricer.BuildTerapeakUrl(card).Returns("https://terapeak/x");
        pricer.BuildEbaySoldUrl(card).Returns("https://ebay/x");
        pricer.SuggestPrice(50m, card).Returns(45m);
        var controller = Create(repo: repo, pricer: pricer);

        var result = await controller.Research(5);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PricingResearchViewModel>(view.Model);
        Assert.Equal("https://terapeak/x", model.TerapeakUrl);
        Assert.Equal("https://ebay/x", model.EbaySoldUrl);
        Assert.Equal(45m, model.SuggestedPrice);
    }

    [Fact]
    public async Task Should_Return404_When_ResearchCalledForMissingCard()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardAsync(99).Returns((Card?)null);
        var controller = Create(repo: repo);

        var result = await controller.Research(99);

        Assert.IsType<NotFoundResult>(result);
    }

    // === Save ===

    [Fact]
    public async Task Should_PersistPricingAndPromoteToPriced_When_SaveCalledWithListing()
    {
        var repo = Substitute.For<ICardRepository>();
        var card = new Card { Id = 7, PlayerName = "Mike", Status = CardStatus.Draft };
        repo.GetCardAsync(7).Returns(card);
        var controller = Create(repo: repo);

        var result = await controller.Save(7, estimatedValue: 30m, listingPrice: 25m);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        await repo.Received(1).UpdateCardAsync(Arg.Is<Card>(c =>
            c.ListingPrice == 25m && c.EstimatedValue == 30m && c.Status == CardStatus.Priced));
    }

    [Fact]
    public async Task Should_RedirectWithErrorMessage_When_SaveCalledForMissingCard()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardAsync(99).Returns((Card?)null);
        var controller = Create(repo: repo);

        var result = await controller.Save(99, null, null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Contains("not found", controller.TempData["ErrorMessage"]!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // === Browser-opening AJAX endpoints ===

    [Fact]
    public async Task Should_OpenTerapeakAndReturnJson_When_OpenTerapeakCalled()
    {
        var repo = Substitute.For<ICardRepository>();
        var card = new Card { Id = 1 };
        repo.GetCardAsync(1).Returns(card);
        var pricer = Substitute.For<IPricerService>();
        pricer.BuildTerapeakUrl(card).Returns("https://t/x");
        var browser = Substitute.For<IBrowserService>();
        var controller = Create(repo: repo, pricer: pricer, browser: browser);

        var result = await controller.OpenTerapeak(1);

        var json = Assert.IsType<JsonResult>(result);
        browser.Received(1).OpenUrl("https://t/x");
        Assert.NotNull(json.Value);
    }

    [Fact]
    public async Task Should_Return404Json_When_OpenTerapeakForMissingCard()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardAsync(99).Returns((Card?)null);
        var controller = Create(repo: repo);

        var result = await controller.OpenTerapeak(99);

        Assert.IsType<NotFoundResult>(result);
    }

    // === CalculateSuggested ===

    [Fact]
    public async Task Should_ReturnSuggestedPriceJson_When_CalculateSuggestedCalled()
    {
        var repo = Substitute.For<ICardRepository>();
        var card = new Card { Id = 1 };
        repo.GetCardAsync(1).Returns(card);
        var pricer = Substitute.For<IPricerService>();
        pricer.SuggestPrice(50m, card).Returns(45m);
        pricer.CalculateNet(45m).Returns(39.75m);
        var controller = Create(repo: repo, pricer: pricer);

        var result = await controller.CalculateSuggested(1, 50m);

        var json = Assert.IsType<JsonResult>(result);
        Assert.NotNull(json.Value);
    }

    [Fact]
    public async Task Should_ReturnFailureJson_When_CalculateSuggestedForMissingCard()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardAsync(99).Returns((Card?)null);
        var controller = Create(repo: repo);

        var result = await controller.CalculateSuggested(99, 10m);

        var json = Assert.IsType<JsonResult>(result);
        Assert.NotNull(json.Value);
        // The anonymous response is { success = false, error = "Card not found" } —
        // verify via reflection because anonymous types are private.
        var props = json.Value!.GetType().GetProperty("success")!.GetValue(json.Value);
        Assert.Equal(false, props);
    }
}
