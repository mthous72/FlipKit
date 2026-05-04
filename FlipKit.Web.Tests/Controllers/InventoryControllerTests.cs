using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Web.Controllers;
using FlipKit.Web.Models;
using FlipKit.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Web.Tests.Controllers;

public class InventoryControllerTests
{
    private static InventoryController Create(
        ICardRepository? repo = null,
        IImageUploadService? upload = null)
    {
        var env = Substitute.For<IWebHostEnvironment>();
        env.WebRootPath.Returns(Path.Combine(Path.GetTempPath(), "flipkit-test-www"));
        var controller = new InventoryController(
            repo ?? Substitute.For<ICardRepository>(),
            env,
            upload ?? Substitute.For<IImageUploadService>(),
            NullLogger<InventoryController>.Instance);
        TempDataHelper.Attach(controller);
        return controller;
    }

    private static List<Card> Sample() => new()
    {
        new Card { Id = 1, PlayerName = "Mike Trout", Sport = Sport.Baseball, Status = CardStatus.Ready, UpdatedAt = DateTime.UtcNow.AddDays(-1) },
        new Card { Id = 2, PlayerName = "Aaron Judge", Sport = Sport.Baseball, Status = CardStatus.Listed, Brand = "Topps", UpdatedAt = DateTime.UtcNow.AddDays(-2) },
        new Card { Id = 3, PlayerName = "Patrick Mahomes", Sport = Sport.Football, Status = CardStatus.Draft, UpdatedAt = DateTime.UtcNow.AddDays(-3) },
    };

    // === Index ===

    [Fact]
    public async Task Should_ReturnAllCards_When_IndexCalledWithNoFilters()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var controller = Create(repo: repo);

        var result = await controller.Index(null, "All", "All");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<InventoryListViewModel>(view.Model);
        Assert.Equal(3, model.Cards.Count);
    }

    [Fact]
    public async Task Should_FilterByPlayerSearch_When_SearchTermProvided()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var controller = Create(repo: repo);

        var result = await controller.Index(search: "trout");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<InventoryListViewModel>(view.Model);
        Assert.Single(model.Cards);
        Assert.Equal("Mike Trout", model.Cards[0].PlayerName);
    }

    [Fact]
    public async Task Should_FilterBySportAndStatus_When_FiltersProvided()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var controller = Create(repo: repo);

        var result = await controller.Index(null, sport: "Baseball", status: "Ready");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<InventoryListViewModel>(view.Model);
        Assert.Single(model.Cards);
        Assert.Equal("Mike Trout", model.Cards[0].PlayerName);
    }

    [Fact]
    public async Task Should_PaginateResults_When_PageAndPageSizeSet()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(Sample());
        var controller = Create(repo: repo);

        var result = await controller.Index(null, "All", "All", page: 2, pageSize: 2);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<InventoryListViewModel>(view.Model);
        Assert.Single(model.Cards); // 3 cards, page 2 of size 2 = 1 card
        Assert.Equal(2, model.CurrentPage);
    }

    [Fact]
    public async Task Should_HandleErrorAndReturnEmptyView_When_RepoThrowsInIndex()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns<List<Card>>(_ => throw new Exception("db down"));
        var controller = Create(repo: repo);

        var result = await controller.Index(null);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<InventoryListViewModel>(view.Model);
        Assert.Empty(model.Cards);
        Assert.True(controller.TempData.ContainsKey("ErrorMessage"));
    }

    // === Details ===

    [Fact]
    public async Task Should_ReturnDetailsView_When_DetailsCalledForExistingCard()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardAsync(1).Returns(new Card { Id = 1, PlayerName = "Mike" });
        var controller = Create(repo: repo);

        var result = await controller.Details(1);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CardDetailsViewModel>(view.Model);
        Assert.Equal("Mike", model.PlayerName);
    }

    [Fact]
    public async Task Should_RedirectToIndex_When_DetailsCalledForMissingCard()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardAsync(99).Returns((Card?)null);
        var controller = Create(repo: repo);

        var result = await controller.Details(99);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
    }

    // === Edit GET ===

    [Fact]
    public async Task Should_ReturnEditView_When_EditGetCalledForExistingCard()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardAsync(1).Returns(new Card { Id = 1, PlayerName = "Mike" });
        var controller = Create(repo: repo);

        var result = await controller.Edit(1);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<CardDetailsViewModel>(view.Model);
    }

    // === Edit POST ===

    [Fact]
    public async Task Should_RedirectWithError_When_EditPostHasIdMismatch()
    {
        var controller = Create();

        var result = await controller.Edit(1, new CardDetailsViewModel { Id = 99 });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Contains("Invalid", controller.TempData["ErrorMessage"]!.ToString());
    }

    [Fact]
    public async Task Should_PersistChangesAndRedirectToDetails_When_EditPostSucceeds()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardAsync(7).Returns(new Card { Id = 7, PlayerName = "Old Name" });
        var controller = Create(repo: repo);

        var result = await controller.Edit(7, new CardDetailsViewModel { Id = 7, PlayerName = "New Name" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        await repo.Received(1).UpdateCardAsync(Arg.Is<Card>(c => c.PlayerName == "New Name"));
    }

    // === Delete ===

    [Fact]
    public async Task Should_DeleteCardAndRedirect_When_DeleteCalled()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardAsync(5).Returns(new Card { Id = 5, PlayerName = "Mike" });
        var controller = Create(repo: repo);

        var result = await controller.Delete(5);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        await repo.Received(1).DeleteCardAsync(5);
        Assert.Contains("deleted successfully", controller.TempData["SuccessMessage"]!.ToString());
    }

    [Fact]
    public async Task Should_RedirectWithError_When_DeleteCalledForMissingCard()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardAsync(99).Returns((Card?)null);
        var controller = Create(repo: repo);

        var result = await controller.Delete(99);

        Assert.IsType<RedirectToActionResult>(result);
        await repo.DidNotReceive().DeleteCardAsync(Arg.Any<int>());
        Assert.Contains("not found", controller.TempData["ErrorMessage"]!.ToString());
    }
}
