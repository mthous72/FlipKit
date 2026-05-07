using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using FlipKit.Core.Models.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace FlipKit.Core.Data
{
    /// <summary>
    /// Bootstraps the reference-data tables (league teams, manufacturers,
    /// brands) from JSON resources embedded in this assembly. Idempotent:
    /// only inserts rows that don't already exist by their unique key.
    /// Called once at startup after the DB is initialized; safe to call
    /// repeatedly (no-op when fully seeded).
    /// </summary>
    public static class ReferenceDataSeeder
    {
        private const string TeamsResource =
            "FlipKit.Core.Resources.ReferenceData.leagues_teams.json";
        private const string ManufacturersResource =
            "FlipKit.Core.Resources.ReferenceData.manufacturers.json";
        private const string BrandsResource =
            "FlipKit.Core.Resources.ReferenceData.brands.json";
        private const string VariationsResource =
            "FlipKit.Core.Resources.ReferenceData.parallels.json";
        private const string GradingResource =
            "FlipKit.Core.Resources.ReferenceData.grading_authorities.json";
        private const string LeagueAcronymsResource =
            "FlipKit.Core.Resources.ReferenceData.league_acronyms.json";

        public static async Task SeedIfMissingAsync(FlipKitDbContext db)
        {
            await SeedTeamsAsync(db);
            await SeedManufacturersAsync(db);
            await SeedBrandsAsync(db);
            await SeedVariationsAsync(db);
            await SeedGradingAuthoritiesAsync(db);
            await SeedLeagueAcronymsAsync(db);
        }

        private static async Task SeedTeamsAsync(FlipKitDbContext db)
        {
            var seed = LoadJsonResource<List<LeagueTeam>>(TeamsResource);
            if (seed == null || seed.Count == 0) return;

            // Existing keys (Sport, TeamName) — skip rows already in the DB
            // so seed updates that ADD entries don't duplicate existing ones.
            var existing = (await db.LeagueTeams
                .AsNoTracking()
                .Select(t => new { t.Sport, t.TeamName })
                .ToListAsync())
                .Select(t => t.Sport + "|" + t.TeamName)
                .ToHashSet();

            var fresh = seed
                .Where(t => !existing.Contains(t.Sport + "|" + t.TeamName))
                .ToList();
            if (fresh.Count == 0) return;

            await db.LeagueTeams.AddRangeAsync(fresh);
            await db.SaveChangesAsync();
        }

        private static async Task SeedManufacturersAsync(FlipKitDbContext db)
        {
            var seed = LoadJsonResource<List<KnownManufacturer>>(ManufacturersResource);
            if (seed == null || seed.Count == 0) return;

            var existing = (await db.KnownManufacturers.AsNoTracking().Select(m => m.Name).ToListAsync())
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

            var fresh = seed.Where(m => !existing.Contains(m.Name)).ToList();
            if (fresh.Count == 0) return;

            await db.KnownManufacturers.AddRangeAsync(fresh);
            await db.SaveChangesAsync();
        }

        private static async Task SeedBrandsAsync(FlipKitDbContext db)
        {
            var seed = LoadJsonResource<List<KnownBrand>>(BrandsResource);
            if (seed == null || seed.Count == 0) return;

            var existing = (await db.KnownBrands
                .AsNoTracking()
                .Select(b => new { b.Manufacturer, b.Name })
                .ToListAsync())
                .Select(b => b.Manufacturer + "|" + b.Name)
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

            var fresh = seed
                .Where(b => !existing.Contains(b.Manufacturer + "|" + b.Name))
                .ToList();
            if (fresh.Count == 0) return;

            await db.KnownBrands.AddRangeAsync(fresh);
            await db.SaveChangesAsync();
        }

        private static async Task SeedVariationsAsync(FlipKitDbContext db)
        {
            var seed = LoadJsonResource<List<KnownVariation>>(VariationsResource);
            if (seed == null || seed.Count == 0) return;

            var existing = (await db.KnownVariations
                .AsNoTracking()
                .Select(v => new { v.Manufacturer, v.Type, v.Name })
                .ToListAsync())
                .Select(v => v.Manufacturer + "|" + v.Type + "|" + v.Name)
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

            var fresh = seed
                .Where(v => !existing.Contains(v.Manufacturer + "|" + v.Type + "|" + v.Name))
                .ToList();
            if (fresh.Count == 0) return;

            await db.KnownVariations.AddRangeAsync(fresh);
            await db.SaveChangesAsync();
        }

        private static async Task SeedGradingAuthoritiesAsync(FlipKitDbContext db)
        {
            var seed = LoadJsonResource<List<GradingAuthority>>(GradingResource);
            if (seed == null || seed.Count == 0) return;

            var existing = (await db.GradingAuthorities.AsNoTracking().Select(g => g.Code).ToListAsync())
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

            var fresh = seed.Where(g => !existing.Contains(g.Code)).ToList();
            if (fresh.Count == 0) return;

            await db.GradingAuthorities.AddRangeAsync(fresh);
            await db.SaveChangesAsync();
        }

        private static async Task SeedLeagueAcronymsAsync(FlipKitDbContext db)
        {
            var seed = LoadJsonResource<List<LeagueAcronym>>(LeagueAcronymsResource);
            if (seed == null || seed.Count == 0) return;

            var existing = (await db.LeagueAcronyms.AsNoTracking().Select(l => l.Acronym).ToListAsync())
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

            var fresh = seed.Where(l => !existing.Contains(l.Acronym)).ToList();
            if (fresh.Count == 0) return;

            await db.LeagueAcronyms.AddRangeAsync(fresh);
            await db.SaveChangesAsync();
        }

        private static T? LoadJsonResource<T>(string resourceName) where T : class
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null) return null;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
    }
}
