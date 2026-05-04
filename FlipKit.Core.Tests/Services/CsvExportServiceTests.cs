using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Export;
using NSubstitute;

namespace FlipKit.Core.Tests.Services;

public class CsvExportServiceTests
{
    // CsvExportService doesn't actually depend on FlipKitDbContext (audit's claim was
    // wrong — Web's "Depends on DbContext" comment is stale documentation). All deps
    // are real instances of the stateless services tested in Phase 4a.
    private static CsvExportService CreateService(AppSettings? settingsOverride = null)
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(settingsOverride ?? new AppSettings());

        var whatnot = new WhatnotValuesProvider();
        var shipping = new ShippingProfileNormalizer(whatnot);
        var ebayTemplate = new EbayTemplateProvider();

        return new CsvExportService(
            settings,
            new WhatnotExporter(whatnot, shipping),
            new EbayExporter(ebayTemplate, shipping),
            new ExportValidator(whatnot));
    }

    private static Card MinimalValidCard() => new()
    {
        PlayerName = "Mike Trout",
        Year = 2026,
        Brand = "Bowman",
        Team = "Angels",
        Sport = Sport.Baseball,
        WhatnotCategory = "Sports Cards",
        WhatnotSubcategory = "Baseball Singles",
        ListingPrice = 10m,
        Quantity = 1,
        ImageUrl1 = "https://i.ibb.co/x/y.jpg",
        ShippingProfile = "1-3 oz",
        Condition = "Near Mint",
    };

    // GenerateTitle uses the platform-specific template from settings.

    [Fact]
    public void Should_PickWhatnotTemplate_When_ActivePlatformIsWhatnot()
    {
        var settings = new AppSettings { ActiveExportPlatform = ExportPlatform.Whatnot };
        var svc = CreateService(settings);

        var title = svc.GenerateTitle(MinimalValidCard());

        // Whatnot template is "{Year} {Brand} {Player} {Parallel} ..." — no Manufacturer.
        Assert.Contains("2026", title);
        Assert.Contains("Bowman", title);
        Assert.Contains("Mike Trout", title);
    }

    [Fact]
    public void Should_AcceptExplicitPlatformOverride_When_GeneratingTitleForCrossPlatform()
    {
        var svc = CreateService(new AppSettings { ActiveExportPlatform = ExportPlatform.Whatnot });
        var card = MinimalValidCard();
        card.Manufacturer = "Topps";

        var ebayTitle = svc.GenerateTitle(card, ExportPlatform.eBay);

        // eBay template includes {Manufacturer}; Whatnot doesn't.
        Assert.Contains("Topps", ebayTitle);
    }

    // GenerateDescription assembles a multi-line block from card fields.

    [Fact]
    public void Should_BuildDescriptionWithKeyFields_When_GeneratingDescription()
    {
        var svc = CreateService();
        var card = MinimalValidCard();
        card.IsRookie = true;
        card.IsAuto = true;

        var desc = svc.GenerateDescription(card);

        Assert.Contains("Team: Angels", desc);
        Assert.Contains("Condition: Near Mint", desc);
        Assert.Contains("Rookie Card!", desc);
        Assert.Contains("Autograph!", desc);
        Assert.Contains("Ships within 2 business days", desc);
    }

    [Fact]
    public void Should_IncludeGradedFields_When_CardIsGraded()
    {
        var svc = CreateService();
        var card = MinimalValidCard();
        card.IsGraded = true;
        card.GradeCompany = "PSA";
        card.GradeValue = "10";
        card.CertNumber = "12345678";

        var desc = svc.GenerateDescription(card);

        Assert.Contains("Grade: PSA 10", desc);
        Assert.Contains("Cert #: 12345678", desc);
    }

    // ValidateBatch routes to the platform-specific validator.

    [Fact]
    public void Should_RouteToEbayRules_When_PlatformIsEbay()
    {
        var svc = CreateService();
        var card = MinimalValidCard();
        card.Sport = null; // eBay-specific failure (Whatnot doesn't require Sport).

        var errors = svc.ValidateBatch(new[] { card }, ExportPlatform.eBay);

        Assert.Contains(errors, e => e.Field == nameof(Card.Sport));
    }

    [Fact]
    public void Should_RouteToWhatnotRules_When_PlatformIsWhatnotOrUnknown()
    {
        var svc = CreateService();
        var card = MinimalValidCard();
        card.WhatnotCategory = "Fictional"; // Whatnot-specific failure.

        var errors = svc.ValidateBatch(new[] { card }, ExportPlatform.Whatnot);

        Assert.Contains(errors, e => e.Field == nameof(Card.WhatnotCategory));
    }

    [Fact]
    public void Should_OnlyReturnBlockingErrors_When_UsingLegacyValidateCardForExport()
    {
        // ValidateCardForExport returns string messages and filters out warnings.
        var svc = CreateService(new AppSettings { ActiveExportPlatform = ExportPlatform.Whatnot });
        var card = MinimalValidCard();
        card.PlayerName = ""; // blocking

        var errors = svc.ValidateCardForExport(card);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Player name", StringComparison.OrdinalIgnoreCase));
    }

    // ExportCsvAsync writes a real file and dispatches by platform.

    [Fact]
    public async Task Should_WriteValidWhatnotCsv_When_ExportingValidBatch()
    {
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"flipkit-test-{Guid.NewGuid():N}.csv");

        try
        {
            await svc.ExportCsvAsync(new List<Card> { MinimalValidCard() }, path, ExportPlatform.Whatnot);
            var content = await File.ReadAllTextAsync(path);
            Assert.Contains("Title", content); // header present
            Assert.Contains("Mike Trout", content); // data row present (in title col)
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Should_ThrowExportValidationException_When_BlockingErrorsArePresent()
    {
        var svc = CreateService();
        var card = MinimalValidCard();
        card.PlayerName = ""; // blocking

        var path = Path.Combine(Path.GetTempPath(), $"flipkit-test-{Guid.NewGuid():N}.csv");
        try
        {
            await Assert.ThrowsAsync<ExportValidationException>(
                () => svc.ExportCsvAsync(new List<Card> { card }, path));
            Assert.False(File.Exists(path)); // didn't write anything
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Should_WriteTaxCsvWithEightColumns_When_ExportingSoldCards()
    {
        var svc = CreateService();
        var sold = MinimalValidCard();
        sold.SaleDate = new DateTime(2026, 5, 1);
        sold.SalePrice = 25m;
        sold.CostBasis = 5m;
        sold.NetProfit = 17m;
        var path = Path.Combine(Path.GetTempPath(), $"flipkit-test-tax-{Guid.NewGuid():N}.csv");

        try
        {
            await svc.ExportTaxCsvAsync(new List<Card> { sold }, path);
            var lines = await File.ReadAllLinesAsync(path);
            Assert.Contains("Sale Date", lines[0]);
            Assert.Contains("Net Profit", lines[0]);
            Assert.Contains("2026-05-01", lines[1]);
            Assert.Contains("25.00", lines[1]);
        }
        finally { File.Delete(path); }
    }
}
