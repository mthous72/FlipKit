using System.Text.Json;
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

/// <summary>
/// Phase 4e gap-fill — covers the import-merge, export, and reads paths that the
/// initial Phase 4b test set didn't reach. Plus enables coverage on the now-fixed
/// JSON-mutation path (D3 fix in Phase 4.5).
/// </summary>
public class ChecklistLearningServiceFillInTests
{
    private static ChecklistLearningService CreateService(TestDbContext db) =>
        new(db.ServiceProvider,
            Substitute.For<ISettingsService>().Tap(s => s.Load().Returns(new AppSettings { EnableChecklistLearning = true })),
            NullLogger<ChecklistLearningService>.Instance);

    private static FlipKitDbContext NewScopedContext(TestDbContext db) =>
        db.ServiceProvider.CreateScope().ServiceProvider.GetRequiredService<FlipKitDbContext>();

    private static async Task<string> WriteJsonFixtureAsync(object data)
    {
        var path = Path.Combine(Path.GetTempPath(), $"flipkit-test-checklist-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(data));
        return path;
    }

    [Fact]
    public async Task Should_MergeIntoExistingChecklist_When_ImportingDifferentCardsForSameSet()
    {
        // Pre-seed an existing checklist via the service itself (avoids the cross-scope
        // navigation issue from the Phase 4b discovery — and post-D3 fix this works).
        using var db = TestDbContext.Create();
        var svc = CreateService(db);
        await svc.LearnFromCardAsync(new Card
        {
            PlayerName = "First", CardNumber = "1", Manufacturer = "Topps", Brand = "Bowman",
            Year = 2026, Sport = Sport.Baseball,
        });

        // Now import a JSON file that adds a new card + new variation to that set.
        var path = await WriteJsonFixtureAsync(new
        {
            manufacturer = "Topps",
            brand = "Bowman",
            year = 2026,
            sport = "Baseball",
            totalBaseCards = 100,
            cards = new[]
            {
                new { card_number = "2", player_name = "Second", team = "Angels", is_rookie = true },
                new { card_number = "1", player_name = "First", team = "Angels", is_rookie = false }, // already exists
            },
            knownVariations = new[] { "Refractor", "Gold" },
        });
        try
        {
            var result = await svc.ImportChecklistAsync(path);

            Assert.True(result.Success);
            Assert.Equal(1, result.CardsAdded); // only the new "Second", not the duplicate "First"
            Assert.Equal(2, result.VariationsAdded);

            var ctx = NewScopedContext(db);
            var checklist = await ctx.SetChecklists.FirstAsync();
            Assert.Equal(2, checklist.Cards.Count);
            Assert.Equal("mixed", checklist.DataSource); // learned + imported → mixed
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Should_ReturnFailure_When_ImportingNonexistentFile()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db);

        var result = await svc.ImportChecklistAsync("/nonexistent/path/file.json");

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Should_ReturnFailure_When_ImportedFileMissingRequiredFields()
    {
        // Manufacturer/Brand are required — empty file should be rejected.
        using var db = TestDbContext.Create();
        var svc = CreateService(db);
        var path = await WriteJsonFixtureAsync(new { });

        try
        {
            var result = await svc.ImportChecklistAsync(path);

            Assert.False(result.Success);
            Assert.Contains("Invalid", result.ErrorMessage!);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Should_ReturnNull_When_GettingMissingChecklistById()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db);

        var result = await svc.GetChecklistByIdAsync(99999);

        Assert.Null(result);
    }

    [Fact]
    public async Task Should_OrderByHitCountDescending_When_GettingMissingChecklists()
    {
        using var db = TestDbContext.Create();
        using (var scope = db.ServiceProvider.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
            ctx.MissingChecklists.AddRange(
                new MissingChecklist { Manufacturer = "A", Brand = "X", Year = 2026, HitCount = 1 },
                new MissingChecklist { Manufacturer = "B", Brand = "Y", Year = 2026, HitCount = 5 },
                new MissingChecklist { Manufacturer = "C", Brand = "Z", Year = 2026, HitCount = 3 });
            await ctx.SaveChangesAsync();
        }

        var svc = CreateService(db);
        var result = await svc.GetMissingChecklistsAsync();

        Assert.Equal(3, result.Count);
        Assert.Equal(5, result[0].HitCount); // sorted desc
        Assert.Equal(1, result[2].HitCount);
    }

    [Fact]
    public async Task Should_ThrowWhenChecklistNotFound_When_ExportingByIdThatDoesntExist()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db);
        var path = Path.Combine(Path.GetTempPath(), $"flipkit-test-export-{Guid.NewGuid():N}.json");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ExportChecklistAsync(99999, path));
    }
}
