using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlipKit.Core.Helpers;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Combines two reference-data sources into a single per-card candidate list:
    ///   * <see cref="IParallelFamilyService"/> — richest signal when (year, brand)
    ///     are both known; returns the curated parallels for that exact set.
    ///   * <c>parallels.json</c> — manufacturer-tagged flat list; provides the
    ///     fallback when we don't know the year or brand precisely, and acts as
    ///     a top-up so brand-OCR errors don't strand the LLM.
    ///
    /// The output is fed into both the LLM prompt preamble (as a "pick from this
    /// list" instruction) and the response_format json_schema enum (as a hard
    /// constraint). Capped at 40 to keep prompt tokens reasonable.
    /// </summary>
    public class ParallelCandidateProvider : IParallelCandidateProvider
    {
        private const int MaxCandidates = 40;

        private readonly IParallelFamilyService _familyService;
        private readonly IReadOnlyList<ParallelEntry> _entries;

        public ParallelCandidateProvider(IParallelFamilyService familyService)
        {
            _familyService = familyService;
            _entries = LoadEntries();
        }

        public IReadOnlyList<string> GetCandidates(string? manufacturer, string? brand, int? year, string? sport)
        {
            // Resolve brand → manufacturer if the caller didn't supply one. This is
            // the path taken when the OcrHint has Brand="Prizm" but Manufacturer is
            // unset (e.g. fresh AI scan with nothing else known).
            var resolvedManufacturer = !string.IsNullOrWhiteSpace(manufacturer)
                ? manufacturer
                : BrandManufacturerMap.Resolve(brand);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ordered = new List<string>();

            void Add(string name)
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                if (ordered.Count >= MaxCandidates) return;
                if (seen.Add(name)) ordered.Add(name);
            }

            // Layer 1: per-set parallels (highest specificity). Only fires when
            // we know enough to look up — year + brand are required.
            if (year.HasValue && !string.IsNullOrWhiteSpace(brand))
            {
                foreach (var p in _familyService.GetParallels(year, brand, sport))
                    Add(p.Name);
            }

            // Layer 2: manufacturer-wide entries from parallels.json. The user
            // asked for manufacturer-wide scope on the candidate list — tolerant
            // of brand-OCR drift; the LLM can still see Wave / Sparkle / Mojo
            // when brand is mis-read.
            if (!string.IsNullOrWhiteSpace(resolvedManufacturer))
            {
                foreach (var e in _entries
                    .Where(e => e.Type.Equals("Parallel", StringComparison.OrdinalIgnoreCase))
                    .Where(e => e.Manufacturer.Equals(resolvedManufacturer, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
                {
                    Add(e.Name);
                }
            }

            // Layer 3: universal entries — colour / finish names (Silver, Gold,
            // Refractor when manufacturer-blank, etc.). Always relevant; serve as
            // top-up when the manufacturer-specific layer is small or absent.
            foreach (var e in _entries
                .Where(e => e.Type.Equals("Parallel", StringComparison.OrdinalIgnoreCase))
                .Where(e => string.IsNullOrWhiteSpace(e.Manufacturer))
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            {
                Add(e.Name);
            }

            return ordered;
        }

        private static IReadOnlyList<ParallelEntry> LoadEntries()
        {
            var assembly = typeof(ParallelCandidateProvider).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("parallels.json", StringComparison.OrdinalIgnoreCase));
            if (resourceName == null) return Array.Empty<ParallelEntry>();

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return Array.Empty<ParallelEntry>();

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            try
            {
                var entries = JsonSerializer.Deserialize<List<ParallelEntry>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return entries ?? new List<ParallelEntry>();
            }
            catch
            {
                return Array.Empty<ParallelEntry>();
            }
        }

        // Mirrors the parallels.json shape — kept private to this file to avoid
        // leaking another DTO into Core.Models. Type is "Parallel" or "Insert"
        // (we filter to Parallel for the candidate list).
        private sealed class ParallelEntry
        {
            [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;
            [JsonPropertyName("Type")] public string Type { get; set; } = string.Empty;
            [JsonPropertyName("Manufacturer")] public string Manufacturer { get; set; } = string.Empty;
            [JsonPropertyName("Sports")] public List<string>? Sports { get; set; }
        }
    }
}
