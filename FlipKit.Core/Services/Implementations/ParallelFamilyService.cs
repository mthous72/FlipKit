using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Reads the bundled <c>ParallelFamilyCatalog.json</c> embedded resource once at
    /// construction and serves <see cref="ParallelOption"/> lists keyed by
    /// (Year, Brand, Sport). Catalog matches are case-insensitive on Brand and Sport.
    /// Returns an empty list when no entry exists; the UI falls through to free-text
    /// Parallel in that case.
    /// </summary>
    public class ParallelFamilyService : IParallelFamilyService
    {
        private readonly List<CatalogFamily> _families;

        public ParallelFamilyService()
        {
            _families = LoadCatalog();
        }

        public IReadOnlyList<ParallelOption> GetParallels(int? year, string? brand, string? sport)
        {
            if (!year.HasValue || string.IsNullOrWhiteSpace(brand)) return Array.Empty<ParallelOption>();

            var match = _families.FirstOrDefault(f =>
                f.Year == year.Value
                && string.Equals(f.Brand, brand, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(sport)
                    || string.IsNullOrWhiteSpace(f.Sport)
                    || string.Equals(f.Sport, sport, StringComparison.OrdinalIgnoreCase)));

            if (match == null) return Array.Empty<ParallelOption>();

            return match.Parallels
                .Select(p => new ParallelOption { Name = p.Name, Numbered = p.Numbered, PrintRun = p.PrintRun })
                .ToList();
        }

        private static List<CatalogFamily> LoadCatalog()
        {
            var assembly = typeof(ParallelFamilyService).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("ParallelFamilyCatalog.json", StringComparison.OrdinalIgnoreCase));
            if (resourceName == null) return new List<CatalogFamily>();

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return new List<CatalogFamily>();

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            try
            {
                var doc = JsonSerializer.Deserialize<CatalogRoot>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
                return doc?.Families ?? new List<CatalogFamily>();
            }
            catch
            {
                return new List<CatalogFamily>();
            }
        }

        private class CatalogRoot
        {
            [JsonPropertyName("families")]
            public List<CatalogFamily> Families { get; set; } = new();
        }

        private class CatalogFamily
        {
            [JsonPropertyName("year")] public int Year { get; set; }
            [JsonPropertyName("brand")] public string Brand { get; set; } = string.Empty;
            [JsonPropertyName("sport")] public string? Sport { get; set; }
            [JsonPropertyName("parallels")] public List<CatalogParallel> Parallels { get; set; } = new();
        }

        private class CatalogParallel
        {
            [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
            [JsonPropertyName("numbered")] public bool Numbered { get; set; }
            [JsonPropertyName("printRun")] public int? PrintRun { get; set; }
        }
    }
}
