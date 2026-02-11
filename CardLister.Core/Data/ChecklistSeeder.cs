using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using static FlipKit.Core.Services.ChecklistLearningService;

namespace FlipKit.Core.Data
{
    public static class ChecklistSeeder
    {
        public static async Task SeedIfEmptyAsync(FlipKitDbContext db)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames()
                .Where(n => n.Contains("SeedData") && n.EndsWith(".json"))
                .ToList();

            if (resourceNames.Count == 0) return;

            var now = DateTime.UtcNow;
            int added = 0;

            foreach (var resourceName in resourceNames)
            {
                try
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream == null) continue;

                    using var reader = new StreamReader(stream);
                    var json = await reader.ReadToEndAsync();
                    var seedData = JsonSerializer.Deserialize<SeedChecklistData>(json);
                    if (seedData == null) continue;

                    // Skip if this exact set already exists in the database
                    var exists = await db.SetChecklists.AnyAsync(s =>
                        s.Manufacturer == seedData.Manufacturer &&
                        s.Brand == seedData.Brand &&
                        s.Year == seedData.Year &&
                        s.Sport == seedData.Sport);

                    if (exists) continue;

                    var checklist = new SetChecklist
                    {
                        Manufacturer = seedData.Manufacturer,
                        Brand = seedData.Brand,
                        Year = seedData.Year,
                        Sport = seedData.Sport,
                        TotalBaseCards = seedData.TotalBaseCards,
                        DataSource = "seed",
                        CachedAt = now,
                        LastEnrichedAt = DateTime.MinValue,
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

                    db.SetChecklists.Add(checklist);
                    added++;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to load seed file: {ResourceName}", resourceName);
                }
            }

            if (added > 0)
            {
                await db.SaveChangesAsync();
                Log.Information("Seeded {Count} new checklists from embedded resources", added);
            }
        }
    }
}
