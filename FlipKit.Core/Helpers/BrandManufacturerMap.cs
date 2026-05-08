using System;
using System.Collections.Generic;
using System.Linq;

namespace FlipKit.Core.Helpers
{
    /// <summary>
    /// Resolves a card "Brand" string (Prizm, Mosaic, Bowman, Optic, etc.) to its
    /// parent "Manufacturer" (Panini, Topps, Upper Deck). The same lookup is used
    /// by the checklist-file metadata extractor and the parallel-candidate provider
    /// — single source of truth so both stay aligned when we add new brands.
    /// </summary>
    public static class BrandManufacturerMap
    {
        // Brands that don't carry the manufacturer in their name. Keep in sync with
        // ChecklistFileMetadataExtractor's import-time mapping.
        private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Mosaic", "Panini" },
            { "Prizm", "Panini" },
            { "Select", "Panini" },
            { "Optic", "Panini" },
            { "Donruss", "Panini" },
            { "Score", "Panini" },
            { "Contenders", "Panini" },
            { "Absolute", "Panini" },
            { "Origins", "Panini" },
            { "Obsidian", "Panini" },
            { "Immaculate", "Panini" },
            { "National Treasures", "Panini" },
            { "Bowman", "Topps" },
            { "Chrome", "Topps" },
            { "Heritage", "Topps" },
            { "Stadium Club", "Topps" },
            { "Allen and Ginter", "Topps" },
            { "Gypsy Queen", "Topps" },
            { "Fire", "Topps" },
            { "Series 1", "Topps" },
            { "Series 2", "Topps" },
        };

        /// <summary>
        /// Returns the manufacturer for <paramref name="brand"/>, walking left-to-right
        /// through the brand tokens so multi-word brands (e.g. "Donruss Elite") still
        /// resolve via their root ("Donruss" → Panini). Returns null when the brand
        /// is unknown.
        /// </summary>
        public static string? Resolve(string? brand)
        {
            if (string.IsNullOrWhiteSpace(brand)) return null;
            if (Map.TryGetValue(brand, out var direct)) return direct;

            var tokens = brand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var take = tokens.Length; take >= 1; take--)
            {
                var prefix = string.Join(' ', tokens.Take(take));
                if (Map.TryGetValue(prefix, out var prefixMfr))
                    return prefixMfr;
            }
            return null;
        }
    }
}
