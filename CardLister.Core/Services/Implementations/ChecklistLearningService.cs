using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using FlipKit.Core.Data;
using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services
{
    public class ChecklistLearningService : IChecklistLearningService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<ChecklistLearningService> _logger;

        public ChecklistLearningService(IServiceProvider serviceProvider, ISettingsService settingsService, ILogger<ChecklistLearningService> logger)
        {
            _serviceProvider = serviceProvider;
            _settingsService = settingsService;
            _logger = logger;
        }

        public async Task LearnFromCardAsync(Card card)
        {
            try
            {
                var settings = _settingsService.Load();
                if (!settings.EnableChecklistLearning)
                    return;

                if (string.IsNullOrWhiteSpace(card.Manufacturer) ||
                    string.IsNullOrWhiteSpace(card.Brand) ||
                    !card.Year.HasValue)
                    return;

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();

                var sport = card.Sport?.ToString();
                var checklist = await db.SetChecklists.FirstOrDefaultAsync(s =>
                    s.Manufacturer == card.Manufacturer &&
                    s.Brand == card.Brand &&
                    s.Year == card.Year.Value &&
                    s.Sport == sport);

                if (checklist == null)
                {
                    // Try to load from embedded seed data first
                    checklist = TryLoadFromSeedData(card.Manufacturer, card.Brand, card.Year.Value, sport);

                    if (checklist == null)
                    {
                        // No seed data — create minimal learned checklist
                        checklist = new SetChecklist
                        {
                            Manufacturer = card.Manufacturer,
                            Brand = card.Brand,
                            Year = card.Year.Value,
                            Sport = sport,
                            DataSource = "learned",
                            CachedAt = DateTime.UtcNow,
                            LastEnrichedAt = DateTime.UtcNow,
                            Cards = new List<ChecklistCard>(),
                            KnownVariations = new List<string>()
                        };
                    }

                    // Add the scanned card if not already in seed data
                    if (!string.IsNullOrWhiteSpace(card.CardNumber))
                    {
                        var normalizedNumber = FuzzyMatcher.NormalizeCardNumber(card.CardNumber);
                        var cardExists = checklist.Cards.Any(c =>
                            FuzzyMatcher.NormalizeCardNumber(c.CardNumber) == normalizedNumber);

                        if (!cardExists)
                        {
                            checklist.Cards.Add(new ChecklistCard
                            {
                                CardNumber = card.CardNumber,
                                PlayerName = card.PlayerName,
                                Team = card.Team,
                                IsRookie = card.IsRookie,
                                Source = "learned"
                            });
                        }
                    }

                    // Add variation if not already in seed data
                    if (!string.IsNullOrWhiteSpace(card.ParallelName))
                    {
                        var normalizedParallel = FuzzyMatcher.NormalizeParallelName(card.ParallelName);
                        if (!checklist.KnownVariations.Any(v =>
                            FuzzyMatcher.NormalizeParallelName(v) == normalizedParallel))
                        {
                            checklist.KnownVariations.Add(card.ParallelName);
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(card.VariationType) && card.VariationType != "Base")
                    {
                        if (!checklist.KnownVariations.Contains(card.VariationType, StringComparer.OrdinalIgnoreCase))
                            checklist.KnownVariations.Add(card.VariationType);
                    }

                    db.SetChecklists.Add(checklist);

                    // Remove from missing checklists if present
                    var missing = await db.MissingChecklists.FirstOrDefaultAsync(m =>
                        m.Manufacturer == card.Manufacturer &&
                        m.Brand == card.Brand &&
                        m.Year == card.Year.Value &&
                        m.Sport == sport);
                    if (missing != null)
                        db.MissingChecklists.Remove(missing);

                    await db.SaveChangesAsync();
                    _logger.LogInformation("Created checklist for {Manufacturer} {Brand} {Year} ({Source})",
                        checklist.Manufacturer, checklist.Brand, checklist.Year, checklist.DataSource);
                    return;
                }

                // Enrich existing checklist
                bool changed = false;

                // Add card number if new
                if (!string.IsNullOrWhiteSpace(card.CardNumber))
                {
                    var normalizedNumber = FuzzyMatcher.NormalizeCardNumber(card.CardNumber);
                    var exists = checklist.Cards.Any(c =>
                        FuzzyMatcher.NormalizeCardNumber(c.CardNumber) == normalizedNumber);

                    if (!exists)
                    {
                        checklist.Cards.Add(new ChecklistCard
                        {
                            CardNumber = card.CardNumber,
                            PlayerName = card.PlayerName,
                            Team = card.Team,
                            IsRookie = card.IsRookie,
                            Source = "learned"
                        });
                        changed = true;
                    }
                }

                // Add variation if new
                if (!string.IsNullOrWhiteSpace(card.ParallelName))
                {
                    var normalizedParallel = FuzzyMatcher.NormalizeParallelName(card.ParallelName);
                    var exists = checklist.KnownVariations.Any(v =>
                        FuzzyMatcher.NormalizeParallelName(v) == normalizedParallel);

                    if (!exists)
                    {
                        checklist.KnownVariations.Add(card.ParallelName);
                        changed = true;
                    }
                }

                if (changed)
                {
                    checklist.LastEnrichedAt = DateTime.UtcNow;
                    if (checklist.DataSource == "seed")
                        checklist.DataSource = "mixed";

                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                // Never crash the caller — learning is best-effort
                _logger.LogError(ex, "Checklist learning failed for {Player} ({Manufacturer} {Brand} {Year})",
                    card.PlayerName, card.Manufacturer, card.Brand, card.Year);
            }
        }

        public async Task<ChecklistImportResult> ImportChecklistAsync(string filePath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var imported = JsonSerializer.Deserialize<SeedChecklistData>(json);

                if (imported == null || string.IsNullOrWhiteSpace(imported.Manufacturer) ||
                    string.IsNullOrWhiteSpace(imported.Brand))
                {
                    return new ChecklistImportResult { Success = false, ErrorMessage = "Invalid checklist format" };
                }

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();

                var existing = await db.SetChecklists.FirstOrDefaultAsync(s =>
                    s.Manufacturer == imported.Manufacturer &&
                    s.Brand == imported.Brand &&
                    s.Year == imported.Year &&
                    s.Sport == imported.Sport);

                int cardsAdded = 0;
                int variationsAdded = 0;

                if (existing == null)
                {
                    var checklist = new SetChecklist
                    {
                        Manufacturer = imported.Manufacturer,
                        Brand = imported.Brand,
                        Year = imported.Year,
                        Sport = imported.Sport,
                        TotalBaseCards = imported.TotalBaseCards,
                        DataSource = "imported",
                        CachedAt = DateTime.UtcNow,
                        LastEnrichedAt = DateTime.UtcNow,
                        Cards = imported.Cards?.Select(c => new ChecklistCard
                        {
                            CardNumber = c.CardNumber,
                            PlayerName = c.PlayerName,
                            Team = c.Team,
                            IsRookie = c.IsRookie,
                            Source = "imported"
                        }).ToList() ?? new List<ChecklistCard>(),
                        KnownVariations = imported.KnownVariations ?? new List<string>()
                    };

                    cardsAdded = checklist.Cards.Count;
                    variationsAdded = checklist.KnownVariations.Count;
                    db.SetChecklists.Add(checklist);
                }
                else
                {
                    // Merge into existing
                    if (imported.Cards != null)
                    {
                        foreach (var importedCard in imported.Cards)
                        {
                            var normalizedNumber = FuzzyMatcher.NormalizeCardNumber(importedCard.CardNumber);
                            if (!existing.Cards.Any(c => FuzzyMatcher.NormalizeCardNumber(c.CardNumber) == normalizedNumber))
                            {
                                existing.Cards.Add(new ChecklistCard
                                {
                                    CardNumber = importedCard.CardNumber,
                                    PlayerName = importedCard.PlayerName,
                                    Team = importedCard.Team,
                                    IsRookie = importedCard.IsRookie,
                                    Source = "imported"
                                });
                                cardsAdded++;
                            }
                        }
                    }

                    if (imported.KnownVariations != null)
                    {
                        foreach (var variation in imported.KnownVariations)
                        {
                            var normalized = FuzzyMatcher.NormalizeParallelName(variation);
                            if (!existing.KnownVariations.Any(v => FuzzyMatcher.NormalizeParallelName(v) == normalized))
                            {
                                existing.KnownVariations.Add(variation);
                                variationsAdded++;
                            }
                        }
                    }

                    existing.LastEnrichedAt = DateTime.UtcNow;
                    if (existing.DataSource != "imported")
                        existing.DataSource = "mixed";
                }

                await db.SaveChangesAsync();

                return new ChecklistImportResult
                {
                    Success = true,
                    CardsAdded = cardsAdded,
                    VariationsAdded = variationsAdded
                };
            }
            catch (Exception ex)
            {
                return new ChecklistImportResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        public async Task ExportChecklistAsync(int checklistId, string outputPath)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();

            var checklist = await db.SetChecklists.FindAsync(checklistId);
            if (checklist == null)
                throw new InvalidOperationException("Checklist not found");

            var exportData = new SeedChecklistData
            {
                Manufacturer = checklist.Manufacturer,
                Brand = checklist.Brand,
                Year = checklist.Year,
                Sport = checklist.Sport,
                TotalBaseCards = checklist.TotalBaseCards,
                Cards = checklist.Cards.Select(c => new SeedCardData
                {
                    CardNumber = c.CardNumber,
                    PlayerName = c.PlayerName,
                    Team = c.Team,
                    IsRookie = c.IsRookie
                }).ToList(),
                KnownVariations = checklist.KnownVariations
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputPath, json);
        }

        private SetChecklist? TryLoadFromSeedData(string manufacturer, string brand, int year, string? sport)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceNames = assembly.GetManifestResourceNames()
                    .Where(n => n.Contains("SeedData") && n.EndsWith(".json"))
                    .ToList();

                foreach (var resourceName in resourceNames)
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream == null) continue;

                    using var reader = new StreamReader(stream);
                    var json = reader.ReadToEnd();
                    var seedData = JsonSerializer.Deserialize<SeedChecklistData>(json);
                    if (seedData == null) continue;

                    if (string.Equals(seedData.Manufacturer, manufacturer, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(seedData.Brand, brand, StringComparison.OrdinalIgnoreCase) &&
                        seedData.Year == year &&
                        string.Equals(seedData.Sport, sport, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Found seed data for {Manufacturer} {Brand} {Year}", manufacturer, brand, year);
                        return new SetChecklist
                        {
                            Manufacturer = seedData.Manufacturer,
                            Brand = seedData.Brand,
                            Year = seedData.Year,
                            Sport = seedData.Sport,
                            TotalBaseCards = seedData.TotalBaseCards,
                            DataSource = "seed",
                            CachedAt = DateTime.UtcNow,
                            LastEnrichedAt = DateTime.UtcNow,
                            Cards = seedData.Cards?.Select(c => new ChecklistCard
                            {
                                CardNumber = c.CardNumber,
                                PlayerName = c.PlayerName,
                                Team = c.Team,
                                IsRookie = c.IsRookie,
                                Source = "seed"
                            }).ToList() ?? new List<ChecklistCard>(),
                            KnownVariations = seedData.KnownVariations ?? new List<string>()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to search seed data for {Manufacturer} {Brand} {Year}", manufacturer, brand, year);
            }

            return null;
        }

        public async Task<List<SetChecklist>> GetAllChecklistsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
            return await db.SetChecklists.OrderBy(s => s.Manufacturer).ThenBy(s => s.Brand).ThenBy(s => s.Year).ToListAsync();
        }

        public async Task<SetChecklist?> GetChecklistByIdAsync(int id)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
            return await db.SetChecklists.FindAsync(id);
        }

        public async Task<List<MissingChecklist>> GetMissingChecklistsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
            return await db.MissingChecklists.OrderByDescending(m => m.HitCount).ToListAsync();
        }

        public async Task DeleteChecklistAsync(int id)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
            var checklist = await db.SetChecklists.FindAsync(id);
            if (checklist != null)
            {
                db.SetChecklists.Remove(checklist);
                await db.SaveChangesAsync();
            }
        }
    }

    // JSON models for seed data and import/export
    public class SeedChecklistData
    {
        [System.Text.Json.Serialization.JsonPropertyName("manufacturer")]
        public string Manufacturer { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("brand")]
        public string Brand { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("year")]
        public int Year { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("sport")]
        public string? Sport { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("totalBaseCards")]
        public int TotalBaseCards { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("cards")]
        public List<SeedCardData>? Cards { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("knownVariations")]
        public List<string>? KnownVariations { get; set; }
    }

    public class SeedCardData
    {
        [System.Text.Json.Serialization.JsonPropertyName("card_number")]
        public string CardNumber { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("player_name")]
        public string PlayerName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("team")]
        public string? Team { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("is_rookie")]
        public bool IsRookie { get; set; }
    }
}
