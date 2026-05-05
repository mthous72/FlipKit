using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Export;
using FlipKit.Web.Controllers;
using FlipKit.Web.Models;
using FlipKit.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Web.Tests.Controllers;

public class ExportControllerTests
{
    private static ExportController Create(ICardRepository? repo = null, IExportService? export = null)
    {
        var controller = new ExportController(
            repo ?? Substitute.For<ICardRepository>(),
            export ?? Substitute.For<IExportService>(),
            NullLogger<ExportController>.Instance);
        TempDataHelper.Attach(controller);
        return controller;
    }

    private static List<Card> ReadyCards() => new()
    {
        new Card { Id = 1, PlayerName = "Mike Trout", Sport = Sport.Baseball, Status = CardStatus.Ready, ListingPrice = 25.00m, UpdatedAt = DateTime.UtcNow },
        new Card { Id = 2, PlayerName = "Aaron Judge", Sport = Sport.Baseball, Status = CardStatus.Ready, ListingPrice = 40.00m, UpdatedAt = DateTime.UtcNow },
    };

    [Fact]
    public async Task Index_ReturnsView_WithFilteredCards()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Ready, null).Returns(ReadyCards());
        var controller = Create(repo: repo);

        var result = await controller.Index(null, "All", "Ready", "Whatnot");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ExportListViewModel>(view.Model);
        Assert.Equal(2, model.Cards.Count);
        Assert.Equal("Ready", model.SelectedStatus);
        Assert.Equal("Whatnot", model.SelectedPlatform);
    }

    [Fact]
    public async Task ExportCsv_RedirectsWithError_WhenNoIdsSelected()
    {
        var controller = Create();

        var result = await controller.ExportCsv(null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.True(controller.TempData.ContainsKey("ErrorMessage"));
    }

    [Fact]
    public async Task ExportCsv_RedirectsWithError_WhenEmptyListSelected()
    {
        var controller = Create();

        var result = await controller.ExportCsv(new List<int>());

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.True(controller.TempData.ContainsKey("ErrorMessage"));
    }

    [Fact]
    public async Task ExportCsv_BlocksExport_WhenValidationErrors()
    {
        var cards = ReadyCards();
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Ready, null).Returns(cards);

        var export = Substitute.For<IExportService>();
        export.ValidateBatch(Arg.Any<IList<Card>>(), Arg.Any<ExportPlatform>())
              .Returns(new List<ExportRowError>
              {
                  new ExportRowError(1, "Mike Trout", "ImageUrl", "No image URL", ExportErrorSeverity.Error)
              });

        var controller = Create(repo: repo, export: export);

        var result = await controller.ExportCsv(new List<int> { 1 }, "Whatnot", "All", "Ready");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ExportListViewModel>(view.Model);
        Assert.Single(model.ValidationErrors);
        Assert.True(controller.TempData.ContainsKey("ErrorMessage"));
    }
}

public class ReportsControllerTests
{
    private static ReportsController Create(ICardRepository? repo = null, IExportService? export = null)
    {
        var controller = new ReportsController(
            repo ?? Substitute.For<ICardRepository>(),
            export ?? Substitute.For<IExportService>(),
            NullLogger<ReportsController>.Instance);
        TempDataHelper.Attach(controller);
        return controller;
    }

    private static List<Card> AllCards() => new()
    {
        new Card { Id = 1, PlayerName = "Mike Trout", Status = CardStatus.Ready, ListingPrice = 25m, UpdatedAt = DateTime.UtcNow },
        new Card { Id = 2, PlayerName = "Aaron Judge", Status = CardStatus.Sold, SalePrice = 40m, SaleDate = DateTime.Today, UpdatedAt = DateTime.UtcNow },
        new Card { Id = 3, PlayerName = "Patrick Mahomes", Status = CardStatus.Draft, UpdatedAt = DateTime.UtcNow },
    };

    private static List<Card> SoldCards() => AllCards().Where(c => c.Status == CardStatus.Sold).ToList();

    [Fact]
    public async Task Index_ReturnsView_WithInventorySnapshot()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(AllCards());
        repo.GetAllCardsAsync(CardStatus.Sold, null).Returns(SoldCards());
        var controller = Create(repo: repo);

        var result = await controller.Index(null, null);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReportsViewModel>(view.Model);
        Assert.Equal(3, model.TotalCards);
        Assert.Equal(1, model.SoldCards);
    }

    [Fact]
    public async Task Index_FiltersSalesByDateRange()
    {
        var sold = new List<Card>
        {
            new Card { Id = 10, PlayerName = "Old Sale", Status = CardStatus.Sold, SalePrice = 10m, SaleDate = DateTime.Today.AddDays(-100), UpdatedAt = DateTime.Today.AddDays(-100) },
            new Card { Id = 11, PlayerName = "Recent Sale", Status = CardStatus.Sold, SalePrice = 20m, SaleDate = DateTime.Today.AddDays(-10), UpdatedAt = DateTime.Today.AddDays(-10) },
        };
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync().Returns(new List<Card>());
        repo.GetAllCardsAsync(CardStatus.Sold, null).Returns(sold);
        var controller = Create(repo: repo);

        var start = DateTime.Today.AddDays(-30);
        var end = DateTime.Today;
        var result = await controller.Index(start, end);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReportsViewModel>(view.Model);
        Assert.Single(model.SoldInRange);
        Assert.Equal("Recent Sale", model.SoldInRange[0].PlayerName);
    }

    [Fact]
    public async Task ExportTaxCsv_RedirectsWithError_WhenNoSalesInRange()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Sold, null).Returns(new List<Card>());
        var controller = Create(repo: repo);

        var result = await controller.ExportTaxCsv(DateTime.Today.AddDays(-30), DateTime.Today);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.True(controller.TempData.ContainsKey("ErrorMessage"));
    }
}
