using System.Linq;
using FlipKit.Core.Data;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlipKit.Core.Tests.Services;

public class ChecklistImportServiceTests
{
    private const string MosaicFilename = "2025-Panini-Mosaic-Football-Checklist-Downloads-Excel-spreadsheet.xlsx";

    private static ChecklistImportService NewService(TestDbContext db)
    {
        var importer = new ExcelChecklistImporter(new ChecklistFileMetadataExtractor());
        return new ChecklistImportService(importer, db.ServiceProvider, NullLogger<ChecklistImportService>.Instance);
    }

    private static FlipKitDbContext NewScopedContext(TestDbContext db)
        => db.ServiceProvider.CreateScope().ServiceProvider.GetRequiredService<FlipKitDbContext>();

    private static ChecklistImportPreview ParseSyntheticMosaic()
    {
        var importer = new ExcelChecklistImporter(new ChecklistFileMetadataExtractor());
        using var stream = SyntheticChecklistXlsxBuilder.MosaicLikeFixture();
        return importer.Parse(stream, MosaicFilename);
    }

    [Fact]
    public async Task Parse_DelegatesToImporter_AndReturnsPreviewForUI()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);

        using var stream = SyntheticChecklistXlsxBuilder.MosaicLikeFixture();
        var preview = svc.Parse(stream, MosaicFilename);

        Assert.True(preview.IsValid);
        Assert.True(preview.Cards.Count > 0);
        // No DB write yet — Parse must be side-effect-free.
        Assert.Empty(NewScopedContext(db).SetChecklists);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CommitAsync_WritesNewSetChecklist_WhenNoneExists()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);
        var preview = ParseSyntheticMosaic();

        var result = await svc.CommitAsync(preview);

        Assert.True(result.Success);
        Assert.False(result.ReplacedExisting);
        Assert.Equal(preview.Cards.Count, result.CardsImported);

        var checklist = Assert.Single(NewScopedContext(db).SetChecklists);
        Assert.Equal("checklist-insider", checklist.DataSource);
        Assert.NotNull(checklist.ImportedAt);
        Assert.Equal(2025, checklist.Year);
        Assert.Equal("Mosaic", checklist.Brand);
        Assert.Equal("Football", checklist.Sport);
        Assert.True(checklist.Cards.Count > 0);
    }

    [Fact]
    public async Task CommitAsync_ReplacesExistingChecklist_WhenReplaceFlagIsTrue()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);

        // Pre-seed a stale row for the same set.
        using (var seedScope = db.ServiceProvider.CreateScope())
        {
            var ctx = seedScope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
            ctx.SetChecklists.Add(new SetChecklist
            {
                Manufacturer = "Panini",
                Brand = "Mosaic",
                Year = 2025,
                Sport = "Football",
                DataSource = "seed",
                Cards = new() { new ChecklistCard { CardNumber = "1", PlayerName = "Stale" } },
            });
            await ctx.SaveChangesAsync();
        }

        var preview = ParseSyntheticMosaic();
        var result = await svc.CommitAsync(preview, replaceExisting: true);

        Assert.True(result.Success);
        Assert.True(result.ReplacedExisting);

        var saved = Assert.Single(NewScopedContext(db).SetChecklists);
        Assert.Equal("checklist-insider", saved.DataSource);
        Assert.DoesNotContain(saved.Cards, c => c.PlayerName == "Stale");
    }

    [Fact]
    public async Task CommitAsync_RefusesReplacement_WhenReplaceFlagIsFalse()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);

        using (var seedScope = db.ServiceProvider.CreateScope())
        {
            var ctx = seedScope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
            ctx.SetChecklists.Add(new SetChecklist
            {
                Manufacturer = "Panini",
                Brand = "Mosaic",
                Year = 2025,
                Sport = "Football",
                DataSource = "seed",
            });
            await ctx.SaveChangesAsync();
        }

        var preview = ParseSyntheticMosaic();
        var result = await svc.CommitAsync(preview, replaceExisting: false);

        Assert.False(result.Success);
        Assert.NotNull(result.ChecklistId);
    }

    [Fact]
    public async Task CommitAsync_RemovesMissingChecklistEntry_OnceImported()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);

        using (var seedScope = db.ServiceProvider.CreateScope())
        {
            var ctx = seedScope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
            ctx.MissingChecklists.Add(new MissingChecklist
            {
                Manufacturer = "Panini",
                Brand = "Mosaic",
                Year = 2025,
                Sport = "Football",
                HitCount = 7,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        var preview = ParseSyntheticMosaic();
        await svc.CommitAsync(preview);

        Assert.Empty(NewScopedContext(db).MissingChecklists);
    }

    [Fact]
    public async Task CommitAsync_FailsCleanly_WhenPreviewIsIncomplete()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);

        var bad = new ChecklistImportPreview(); // no metadata, no cards

        var result = await svc.CommitAsync(bad);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Empty(NewScopedContext(db).SetChecklists);
    }
}
