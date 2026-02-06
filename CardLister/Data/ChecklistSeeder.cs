using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using CardLister.Models;
using CardLister.Services;

namespace CardLister.Data
{
    public static class ChecklistSeeder
    {
        public static async Task SeedIfEmptyAsync(CardListerDbContext db)
        {
            if (db.SetChecklists.Any())
                return;

            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames()
                .Where(n => n.Contains("SeedData") && n.EndsWith(".json"))
                .ToList();

            var now = DateTime.UtcNow;

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
                }
                catch
                {
                    // Skip malformed seed files
                }
            }

            await db.SaveChangesAsync();
        }
    }
}
