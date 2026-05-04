using FlipKit.Core.Data;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Core.Tests.Services;

public class ChecklistLearningServiceTests
{
    private static ISettingsService SettingsWithLearning(bool enabled)
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { EnableChecklistLearning = enabled });
        return settings;
    }

    private static ChecklistLearningService CreateService(TestDbContext db, bool learningEnabled = true) =>
        new(db.ServiceProvider, SettingsWithLearning(learningEnabled), NullLogger<ChecklistLearningService>.Instance);

    private static FlipKitDbContext NewScopedContext(TestDbContext db) =>
        db.ServiceProvider.CreateScope().ServiceProvider.GetRequiredService<FlipKitDbContext>();

    private static Card BowmanRookie() => new()
    {
        PlayerName = "Mike Trout",
        CardNumber = "BCP-1",
        Manufacturer = "Topps",
        Brand = "Bowman",
        Year = 2026,
        Sport = Sport.Baseball,
        IsRookie = true,
        ParallelName = "Refractor",
        VariationType = "Refractor",
    };

    // === LearnFromCardAsync — opt-out ===

    [Fact]
    public async Task Should_DoNothing_When_LearningIsDisabled()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db, learningEnabled: false);

        await svc.LearnFromCardAsync(BowmanRookie());

        Assert.Empty(NewScopedContext(db).SetChecklists);
    }

    [Fact]
    public async Task Should_DoNothing_When_CardLacksManufacturerBrandOrYear()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db);
        var card = new Card { PlayerName = "Anonymous" }; // no Manufacturer/Brand/Year

        await svc.LearnFromCardAsync(card);

        Assert.Empty(NewScopedContext(db).SetChecklists);
    }

    // === LearnFromCardAsync — create new checklist ===

    [Fact]
    public async Task Should_CreateLearnedChecklist_When_NoneExistsForThatSet()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db);

        await svc.LearnFromCardAsync(BowmanRookie());

        var checklist = Assert.Single(NewScopedContext(db).SetChecklists);
        Assert.Equal("learned", checklist.DataSource);
        Assert.Equal(2026, checklist.Year);
        Assert.Equal("Bowman", checklist.Brand);
        Assert.Single(checklist.Cards);
        Assert.Contains("Refractor", checklist.KnownVariations);
    }

    [Fact]
    public async Task Should_RemoveMissingChecklistEntry_When_FirstCardForThatSetIsLearned()
    {
        using var db = TestDbContext.Create();
        // Pre-seed a "missing" entry that should be cleared.
        using (var seedScope = db.ServiceProvider.CreateScope())
        {
            var ctx = seedScope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
            ctx.MissingChecklists.Add(new MissingChecklist
            {
                Manufacturer = "Topps", Brand = "Bowman", Year = 2026, Sport = "Baseball",
                HitCount = 5, FirstSeen = DateTime.UtcNow, LastSeen = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        var svc = CreateService(db);
        await svc.LearnFromCardAsync(BowmanRookie());

        Assert.Empty(NewScopedContext(db).MissingChecklists);
    }

    // === LearnFromCardAsync — enrich existing ===

    [Fact]
    public async Task Should_AppendNewCardAndVariation_When_ChecklistAlreadyExists()
    {
        // Test the enrichment code path by learning two distinct cards from the same set.
        // First call creates the checklist; second call must append to it (same set, new
        // card number + same variation already known).
        using var db = TestDbContext.Create();
        var svc = CreateService(db);

        await svc.LearnFromCardAsync(BowmanRookie()); // creates checklist with BCP-1 / Refractor

        var second = BowmanRookie();
        second.PlayerName = "Aaron Judge";
        second.CardNumber = "BCP-2"; // distinct number — should be appended
        second.ParallelName = "Gold Refractor"; // distinct variation — should be appended
        await svc.LearnFromCardAsync(second);

        var ctx = NewScopedContext(db);
        var checklist = await ctx.SetChecklists.FirstAsync();
        Assert.Equal(2, checklist.Cards.Count);
        Assert.Contains("Refractor", checklist.KnownVariations);
        Assert.Contains("Gold Refractor", checklist.KnownVariations);
    }

    [Fact]
    public async Task Should_NotDuplicateCardOrVariation_When_AlreadyPresent()
    {
        using var db = TestDbContext.Create();
        using (var seedScope = db.ServiceProvider.CreateScope())
        {
            var ctx = seedScope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
            ctx.SetChecklists.Add(new SetChecklist
            {
                Manufacturer = "Topps", Brand = "Bowman", Year = 2026, Sport = "Baseball",
                DataSource = "imported",
                Cards = new() { new ChecklistCard { CardNumber = "BCP-1", PlayerName = "Mike Trout", Source = "imported" } },
                KnownVariations = new() { "Refractor" },
            });
            await ctx.SaveChangesAsync();
        }

        var svc = CreateService(db);
        await svc.LearnFromCardAsync(BowmanRookie()); // same card number, same variation

        var ctx2 = NewScopedContext(db);
        var checklist = await ctx2.SetChecklists.FirstAsync();
        Assert.Single(checklist.Cards); // no duplicate
        Assert.Single(checklist.KnownVariations); // no duplicate
    }

    [Fact]
    public async Task Should_NeverThrow_When_CardDataIsAnythingButNullCheck()
    {
        // Best-effort learning — internal exceptions are swallowed and logged.
        using var db = TestDbContext.Create();
        var svc = CreateService(db);
        // Intentionally weird input: empty CardNumber, weird ParallelName
        var card = new Card { Manufacturer = "Topps", Brand = "Bowman", Year = 2026, PlayerName = "Anon", CardNumber = "" };

        await svc.LearnFromCardAsync(card); // should not throw

        // The checklist is still created (just with no cards or variations).
        Assert.Single(NewScopedContext(db).SetChecklists);
    }

    // === GetAllChecklistsAsync / GetChecklistByIdAsync / DeleteChecklistAsync ===

    [Fact]
    public async Task Should_OrderByManufacturerThenBrandThenYear_When_ListingAllChecklists()
    {
        using var db = TestDbContext.Create();
        using (var seedScope = db.ServiceProvider.CreateScope())
        {
            var ctx = seedScope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
            ctx.SetChecklists.AddRange(
                new SetChecklist { Manufacturer = "Topps", Brand = "Chrome", Year = 2025 },
                new SetChecklist { Manufacturer = "Panini", Brand = "Prizm", Year = 2026 },
                new SetChecklist { Manufacturer = "Topps", Brand = "Bowman", Year = 2026 });
            await ctx.SaveChangesAsync();
        }

        var svc = CreateService(db);
        var all = await svc.GetAllChecklistsAsync();

        Assert.Equal(3, all.Count);
        Assert.Equal("Panini", all[0].Manufacturer); // alphabetical first
        Assert.Equal("Bowman", all[1].Brand);        // Topps Bowman before Topps Chrome
    }

    [Fact]
    public async Task Should_RemoveChecklist_When_DeletingById()
    {
        using var db = TestDbContext.Create();
        int id;
        using (var seedScope = db.ServiceProvider.CreateScope())
        {
            var ctx = seedScope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
            var entity = new SetChecklist { Manufacturer = "X", Brand = "Y", Year = 2026 };
            ctx.SetChecklists.Add(entity);
            await ctx.SaveChangesAsync();
            id = entity.Id;
        }

        var svc = CreateService(db);
        await svc.DeleteChecklistAsync(id);

        Assert.Empty(NewScopedContext(db).SetChecklists);
    }

    // === ImportChecklistAsync / ExportChecklistAsync ===

    [Fact]
    public async Task Should_RejectMalformedFile_When_Importing()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db);
        var path = Path.Combine(Path.GetTempPath(), $"flipkit-test-import-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{ not valid json");

        try
        {
            var result = await svc.ImportChecklistAsync(path);
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Should_RoundTripImportedChecklist_When_ExportingThenImporting()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db);

        // Seed → export → re-import into a fresh DB → verify count
        int id;
        using (var seedScope = db.ServiceProvider.CreateScope())
        {
            var ctx = seedScope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
            var entity = new SetChecklist
            {
                Manufacturer = "Topps", Brand = "Bowman", Year = 2026, Sport = "Baseball",
                Cards = new() { new ChecklistCard { CardNumber = "1", PlayerName = "Trout", Source = "seed" } },
                KnownVariations = new() { "Base", "Refractor" },
            };
            ctx.SetChecklists.Add(entity);
            await ctx.SaveChangesAsync();
            id = entity.Id;
        }

        var path = Path.Combine(Path.GetTempPath(), $"flipkit-test-roundtrip-{Guid.NewGuid():N}.json");
        try
        {
            await svc.ExportChecklistAsync(id, path);

            using var freshDb = TestDbContext.Create();
            var freshSvc = CreateService(freshDb);
            var importResult = await freshSvc.ImportChecklistAsync(path);

            Assert.True(importResult.Success);
            Assert.Equal(1, importResult.CardsAdded);
            Assert.Equal(2, importResult.VariationsAdded);
        }
        finally { File.Delete(path); }
    }
}
