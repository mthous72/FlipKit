using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Desktop.ViewModels;
using NSubstitute;

namespace FlipKit.Desktop.Tests.ViewModels;

public class ReportsViewModelTests
{
    private static ReportsViewModel Create(
        ICardRepository? repo = null,
        IExportService? export = null,
        IFileDialogService? dialog = null) =>
        new(repo ?? Substitute.For<ICardRepository>(),
            export ?? Substitute.For<IExportService>(),
            dialog ?? Substitute.For<IFileDialogService>());

    private static List<Card> SampleSold(int year = 2026)
    {
        return new()
        {
            new Card
            {
                PlayerName = "Mike Trout", Year = year, Brand = "Bowman", Status = CardStatus.Sold,
                SaleDate = new DateTime(year, 3, 15), SalePrice = 100m, CostBasis = 20m,
                FeesPaid = 11m, ShippingCost = 4m, NetProfit = 65m,
            },
            new Card
            {
                PlayerName = "Aaron Judge", Year = year, Brand = "Topps", Status = CardStatus.Sold,
                SaleDate = new DateTime(year, 4, 10), SalePrice = 50m, CostBasis = 10m,
                FeesPaid = 6m, ShippingCost = 2m, NetProfit = 32m,
            },
        };
    }

    [Fact]
    public async Task Should_PopulateAllSummaryFields_When_LoadReportRuns()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Sold).Returns(SampleSold());
        var vm = Create(repo: repo);

        await vm.LoadReportCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.CardsSold);
        Assert.Equal(150m, vm.TotalRevenue);
        Assert.Equal(30m, vm.TotalCostBasis);
        Assert.Equal(17m, vm.TotalFees);
        Assert.Equal(6m, vm.TotalShipping);
        Assert.Equal(97m, vm.NetProfit);
    }

    [Fact]
    public async Task Should_FilterByDateRange_When_LoadReportRuns()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Sold).Returns(SampleSold());
        var vm = Create(repo: repo);
        vm.StartDate = new DateTimeOffset(new DateTime(2026, 4, 1));
        vm.EndDate = new DateTimeOffset(new DateTime(2026, 4, 30));

        await vm.LoadReportCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.CardsSold); // only the April sale
        Assert.Equal(50m, vm.TotalRevenue);
    }

    [Fact]
    public async Task Should_BuildMonthlyBreakdown_When_LoadReportRuns()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Sold).Returns(SampleSold());
        var vm = Create(repo: repo);

        await vm.LoadReportCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.MonthlyData.Count); // March + April
        Assert.Equal("2026-03", vm.MonthlyData[0].MonthName); // ordered
    }

    [Fact]
    public async Task Should_BuildTopSellersByProfit_When_LoadReportRuns()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Sold).Returns(SampleSold());
        var vm = Create(repo: repo);

        await vm.LoadReportCommand.ExecuteAsync(null);

        Assert.Equal("2026 Bowman Mike Trout", vm.TopSellers[0].Description); // highest profit first
    }

    [Fact]
    public async Task Should_SetStatusMessage_When_LoadReportThrows()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Sold).Returns<List<Card>>(_ => throw new Exception("db down"));
        var vm = Create(repo: repo);

        await vm.LoadReportCommand.ExecuteAsync(null);

        Assert.Contains("Failed", vm.StatusMessage);
    }

    [Fact]
    public async Task Should_DoNothing_When_ExportTaxCsvFiresWithNoSoldCards()
    {
        var dialog = Substitute.For<IFileDialogService>();
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Sold).Returns(new List<Card>());
        var vm = Create(repo: repo, dialog: dialog);
        await vm.LoadReportCommand.ExecuteAsync(null);

        await vm.ExportTaxCsvCommand.ExecuteAsync(null);

        await dialog.DidNotReceiveWithAnyArgs().SaveCsvFileAsync(default!);
        Assert.Contains("No sold cards", vm.StatusMessage);
    }

    [Fact]
    public async Task Should_AbortExport_When_UserCancelsSaveDialog()
    {
        var dialog = Substitute.For<IFileDialogService>();
        dialog.SaveCsvFileAsync(Arg.Any<string>()).Returns((string?)null);
        var export = Substitute.For<IExportService>();
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Sold).Returns(SampleSold());
        var vm = Create(repo: repo, export: export, dialog: dialog);
        await vm.LoadReportCommand.ExecuteAsync(null);

        await vm.ExportTaxCsvCommand.ExecuteAsync(null);

        await export.DidNotReceiveWithAnyArgs().ExportTaxCsvAsync(default!, default!);
    }

    [Fact]
    public async Task Should_DelegateToExportService_When_ExportTaxCsvCompletes()
    {
        var dialog = Substitute.For<IFileDialogService>();
        dialog.SaveCsvFileAsync(Arg.Any<string>()).Returns("/tmp/tax.csv");
        var export = Substitute.For<IExportService>();
        var repo = Substitute.For<ICardRepository>();
        repo.GetAllCardsAsync(CardStatus.Sold).Returns(SampleSold());
        var vm = Create(repo: repo, export: export, dialog: dialog);
        await vm.LoadReportCommand.ExecuteAsync(null);

        await vm.ExportTaxCsvCommand.ExecuteAsync(null);

        await export.Received(1).ExportTaxCsvAsync(Arg.Any<List<Card>>(), "/tmp/tax.csv");
        Assert.Contains("Exported 2 records", vm.StatusMessage);
    }
}
