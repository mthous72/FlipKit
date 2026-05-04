using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Best-effort extractor that pulls (Year, Sport, Manufacturer, Brand, SetName) out of a
    /// Checklist Insider filename. Whatever we can't determine, we leave null and let the
    /// preview UI prompt the user to correct it before commit. Filenames seen in the wild:
    ///
    ///   2025-Panini-Mosaic-Football-Checklist-Downloads-Excel-spreadsheet.xlsx
    ///   2026-Bowman-Baseball-Checklist-Downloads-Excel-spreadsheet.xlsx
    ///   2025-Topps-Chrome-Update-Series-Baseball-Checklist-...
    ///
    /// Never throws — a totally unparseable name returns an all-null result so the UI can
    /// still let the user fill in the metadata manually.
    /// </summary>
    public class ChecklistFileMetadataExtractor : IChecklistFileMetadataExtractor
    {
        private static readonly string[] Sports = new[]
        {
            "Baseball", "Basketball", "Football", "Hockey", "Soccer",
            "Golf", "Racing", "WWE", "Wrestling", "MMA", "UFC",
            "MLB", "NBA", "NFL", "NHL", "MLS",
        };

        // Only true manufacturers — brands like Bowman, Donruss, Score are owned by a
        // manufacturer (Topps, Panini, Panini respectively) and resolved through
        // BrandToManufacturer so we don't accidentally label them as the manufacturer.
        private static readonly string[] Manufacturers = new[]
        {
            "Topps", "Panini", "UpperDeck", "Upper Deck", "Fanatics", "Leaf", "Pinnacle",
        };

        // Brand → manufacturer mapping for brands that don't carry the manufacturer in the
        // filename. Covers the common modern releases that ship with Checklist Insider xlsx
        // files. Unknown brands fall through and the user picks the manufacturer in the UI.
        private static readonly Dictionary<string, string> BrandToManufacturer = new(StringComparer.OrdinalIgnoreCase)
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

        public ChecklistImportMetadata Extract(string fileName)
        {
            var result = new ChecklistImportMetadata { SourceFileName = fileName };
            if (string.IsNullOrWhiteSpace(fileName)) return result;

            var stem = Path.GetFileNameWithoutExtension(fileName);
            // Normalize separators so dashes, underscores, and spaces all behave the same way.
            var normalized = Regex.Replace(stem, "[_\\s]+", "-");

            // Year — first 4-digit run between 1990 and 2099.
            var yearMatch = Regex.Match(normalized, "(?<![0-9])(19[9]\\d|20\\d{2})(?![0-9])");
            if (yearMatch.Success && int.TryParse(yearMatch.Value, out var year))
                result.Year = year;

            // Sport — match against known sports list (case-insensitive, dash-bounded).
            foreach (var sport in Sports)
            {
                if (Regex.IsMatch(normalized, "(?<![A-Za-z])" + Regex.Escape(sport) + "(?![A-Za-z])", RegexOptions.IgnoreCase))
                {
                    result.Sport = NormalizeSportName(sport);
                    break;
                }
            }

            // Manufacturer — explicit name in filename wins over inferred-from-brand.
            foreach (var mfr in Manufacturers)
            {
                if (Regex.IsMatch(normalized, "(?<![A-Za-z])" + Regex.Escape(mfr) + "(?![A-Za-z])", RegexOptions.IgnoreCase))
                {
                    result.Manufacturer = mfr.Equals("UpperDeck", StringComparison.OrdinalIgnoreCase) ? "Upper Deck" : mfr;
                    break;
                }
            }

            // Brand + SetName — strip year/sport/manufacturer/boilerplate, what remains is the
            // set name. The brand is the lead noun (e.g. "Mosaic" out of "Mosaic" or "Mosaic
            // Choice"); we treat the whole remainder as both Brand and SetName by default and
            // let the UI split if needed.
            var trimmed = normalized;
            trimmed = StripYear(trimmed, yearMatch);
            trimmed = StripSport(trimmed, result.Sport);
            trimmed = StripManufacturer(trimmed, result.Manufacturer);
            trimmed = Regex.Replace(trimmed, "(?i)-?Checklist(-Downloads)?(-Excel(-spreadsheet)?)?", "");
            trimmed = Regex.Replace(trimmed, "(?i)-?SUBJECT-TO-CHANGE", "");
            trimmed = trimmed.Trim('-', ' ').Replace("-", " ");
            trimmed = Regex.Replace(trimmed, "\\s+", " ").Trim();

            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                result.SetName = trimmed;
                // Brand = full trimmed remainder ("Donruss", "Donruss Elite", "Donruss Optic",
                // "Bowman Chrome", "Mosaic", etc.). Reducing to the first word collapses
                // distinct releases onto the same SetChecklist unique key
                // (Manufacturer, Brand, Year, Sport) — e.g. Donruss / Donruss Elite / Donruss
                // Optic would all overwrite each other.
                result.Brand = trimmed;
            }

            // If we still don't have a manufacturer, infer from the brand. Try the full brand
            // first so multi-word brands resolve directly; then fall back to a longest-prefix
            // search over the catalog so "Donruss Elite" still maps to Panini via "Donruss".
            if (string.IsNullOrWhiteSpace(result.Manufacturer) && !string.IsNullOrWhiteSpace(result.Brand))
            {
                if (BrandToManufacturer.TryGetValue(result.Brand, out var directMfr))
                {
                    result.Manufacturer = directMfr;
                }
                else
                {
                    // Walk the brand tokens left-to-right looking for a known root brand.
                    var tokens = result.Brand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    for (var take = tokens.Length; take >= 1; take--)
                    {
                        var prefix = string.Join(' ', tokens.Take(take));
                        if (BrandToManufacturer.TryGetValue(prefix, out var prefixMfr))
                        {
                            result.Manufacturer = prefixMfr;
                            break;
                        }
                    }
                }
            }

            return result;
        }

        private static string NormalizeSportName(string raw) => raw.ToUpperInvariant() switch
        {
            "MLB" => "Baseball",
            "NBA" => "Basketball",
            "NFL" => "Football",
            "NHL" => "Hockey",
            "MLS" => "Soccer",
            "WRESTLING" => "WWE",
            _ => char.ToUpperInvariant(raw[0]) + raw.Substring(1).ToLowerInvariant(),
        };

        private static string StripYear(string s, Match yearMatch)
            => yearMatch.Success ? s.Replace(yearMatch.Value, "", StringComparison.Ordinal) : s;

        private static string StripSport(string s, string? sport)
            => string.IsNullOrEmpty(sport)
                ? s
                : Regex.Replace(s, "(?i)(?<![A-Za-z])" + Regex.Escape(sport) + "(?![A-Za-z])", "");

        private static string StripManufacturer(string s, string? manufacturer)
            => string.IsNullOrEmpty(manufacturer)
                ? s
                : Regex.Replace(s, "(?i)(?<![A-Za-z])" + Regex.Escape(manufacturer).Replace("\\ ", "[\\- ]?") + "(?![A-Za-z])", "");
    }
}
