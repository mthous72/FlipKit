using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Parses a Checklist Insider .xlsx into a <see cref="ChecklistImportPreview"/>. Two
    /// layouts are seen in the wild:
    ///   1. Inline-header (Bowman 2026): no top header row, subsets announced by all-caps
    ///      single-cell rows in column A, data rows have A=Card #, B=Player, C=Team, D=flag.
    ///   2. Column-A-subset (Mosaic 2025): top header row "CARD SET / CARD # / ATHLETE /
    ///      TEAM / SEQ", data rows carry the subset in column A on every line.
    /// Format is auto-detected from the first non-empty row. Unrecognized rows are skipped
    /// and surfaced as warnings rather than failing the whole parse.
    /// </summary>
    public class ExcelChecklistImporter : IExcelChecklistImporter
    {
        private readonly IChecklistFileMetadataExtractor _metadataExtractor;

        // Header tokens that indicate the column-A-subset layout.
        private static readonly string[] CardSetHeaderTokens = { "CARD SET", "SET", "CARDSET", "SUBSET" };
        private static readonly string[] CardNumberHeaderTokens = { "CARD #", "CARD#", "CARD NO", "CARD NUMBER", "#" };
        private static readonly string[] PlayerHeaderTokens = { "ATHLETE", "PLAYER", "PLAYER NAME", "NAME" };
        private static readonly string[] TeamHeaderTokens = { "TEAM" };

        // Inline-header detection: a single-cell row in column A with all-caps text and no
        // accompanying B/C/D content. Allow letters, digits, spaces, slashes, dashes.
        private static readonly Regex InlineSubsetHeaderPattern = new("^[A-Z][A-Z0-9 /\\-]+$", RegexOptions.Compiled);

        // Skip-able disclaimer lines often appended at the bottom of files.
        private static readonly string[] DisclaimerSnippets =
        {
            "subject to change",
            "checklists provided by",
            "all rights reserved",
            "© ",
        };

        public ExcelChecklistImporter(IChecklistFileMetadataExtractor metadataExtractor)
        {
            _metadataExtractor = metadataExtractor;
        }

        public ChecklistImportPreview Parse(Stream xlsxStream, string fileName)
        {
            if (xlsxStream == null) throw new ArgumentNullException(nameof(xlsxStream));

            var preview = new ChecklistImportPreview
            {
                Metadata = _metadataExtractor.Extract(fileName ?? string.Empty),
            };

            using var workbook = new XLWorkbook(xlsxStream);
            var sheet = workbook.Worksheets.FirstOrDefault(s => s.RowsUsed().Any());
            if (sheet == null)
            {
                preview.Warnings.Add("The workbook has no populated sheets.");
                return preview;
            }

            var rows = sheet.RowsUsed().ToList();
            preview.TotalRowsRead = rows.Count;

            preview.DetectedFormat = DetectFormat(rows);

            switch (preview.DetectedFormat)
            {
                case ChecklistFileFormat.ColumnASubset:
                    ParseColumnASubset(rows, preview);
                    break;
                case ChecklistFileFormat.InlineHeader:
                    ParseInlineHeader(rows, preview);
                    break;
                default:
                    preview.Warnings.Add("Could not detect a known checklist layout. No cards were parsed.");
                    break;
            }

            preview.SubsetNames = preview.Cards
                .Select(c => c.Subset ?? "Base")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            preview.Metadata.Year ??= TryInferYearFromCells(rows);

            return preview;
        }

        private static ChecklistFileFormat DetectFormat(List<IXLRow> rows)
        {
            var firstRow = rows.FirstOrDefault();
            if (firstRow == null) return ChecklistFileFormat.Unknown;

            var a = TextOf(firstRow, 1);
            var b = TextOf(firstRow, 2);
            var c = TextOf(firstRow, 3);
            var d = TextOf(firstRow, 4);

            // Column-A-subset: first row is a header line with both a "set" column and a
            // "card #" column called out by name.
            var hasSetHeader = MatchesAny(a, CardSetHeaderTokens) || MatchesAny(b, CardSetHeaderTokens);
            var hasCardNumberHeader = MatchesAny(a, CardNumberHeaderTokens)
                                      || MatchesAny(b, CardNumberHeaderTokens)
                                      || MatchesAny(c, CardNumberHeaderTokens);
            var hasPlayerHeader = MatchesAny(b, PlayerHeaderTokens)
                                  || MatchesAny(c, PlayerHeaderTokens)
                                  || MatchesAny(d, PlayerHeaderTokens);
            if (hasSetHeader && hasCardNumberHeader && hasPlayerHeader)
                return ChecklistFileFormat.ColumnASubset;

            // Inline-header: the first row is itself a subset announcement (single-cell, all-caps).
            if (!string.IsNullOrWhiteSpace(a)
                && string.IsNullOrWhiteSpace(b)
                && string.IsNullOrWhiteSpace(c)
                && string.IsNullOrWhiteSpace(d)
                && InlineSubsetHeaderPattern.IsMatch(a.Trim()))
                return ChecklistFileFormat.InlineHeader;

            // Fallback: peek at the first two data-shaped rows. If column A consistently looks
            // like a card number (short, contains digits) we're in inline-header mode by
            // default; if column A consistently looks like text (the subset name), assume
            // column-A-subset.
            var sampleA = rows.Take(5)
                .Select(r => TextOf(r, 1))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Take(3)
                .ToList();
            if (sampleA.Count > 0 && sampleA.All(LooksLikeCardNumber))
                return ChecklistFileFormat.InlineHeader;
            if (sampleA.Count > 0 && sampleA.All(LooksLikeSubsetName))
                return ChecklistFileFormat.ColumnASubset;

            return ChecklistFileFormat.Unknown;
        }

        private void ParseColumnASubset(List<IXLRow> rows, ChecklistImportPreview preview)
        {
            // Skip the header row.
            foreach (var row in rows.Skip(1))
            {
                var subset = TextOf(row, 1);
                var cardNumber = TextOf(row, 2);
                var player = TextOf(row, 3);
                var team = TextOf(row, 4);

                if (string.IsNullOrWhiteSpace(subset) && string.IsNullOrWhiteSpace(cardNumber) && string.IsNullOrWhiteSpace(player))
                {
                    preview.RowsSkipped++;
                    continue;
                }

                if (IsDisclaimer(subset) || IsDisclaimer(player))
                {
                    preview.RowsSkipped++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(player) || string.IsNullOrWhiteSpace(cardNumber))
                {
                    preview.Warnings.Add($"Row {row.RowNumber()}: missing card number or player name; skipped.");
                    preview.RowsSkipped++;
                    continue;
                }

                preview.Cards.Add(new ChecklistCard
                {
                    CardNumber = cardNumber.Trim(),
                    PlayerName = player.Trim(),
                    Team = string.IsNullOrWhiteSpace(team) ? null : team.Trim(),
                    Subset = string.IsNullOrWhiteSpace(subset) ? "Base" : subset.Trim(),
                    IsRookie = false,
                    IsAutograph = ContainsAny(subset, "Autograph", "Auto", "Signature", "Scripts"),
                    IsParallel = LooksLikeParallel(subset),
                    IsInsert = LooksLikeInsert(subset),
                    Source = "checklist-insider",
                });
            }
        }

        private void ParseInlineHeader(List<IXLRow> rows, ChecklistImportPreview preview)
        {
            var currentSubset = "Base";
            foreach (var row in rows)
            {
                var a = TextOf(row, 1);
                var b = TextOf(row, 2);
                var c = TextOf(row, 3);
                var d = TextOf(row, 4);

                if (string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b))
                {
                    preview.RowsSkipped++;
                    continue;
                }

                if (IsDisclaimer(a))
                {
                    preview.RowsSkipped++;
                    continue;
                }

                // Subset header row: only column A populated and value is all-caps.
                if (!string.IsNullOrWhiteSpace(a)
                    && string.IsNullOrWhiteSpace(b)
                    && string.IsNullOrWhiteSpace(c)
                    && string.IsNullOrWhiteSpace(d)
                    && InlineSubsetHeaderPattern.IsMatch(a.Trim()))
                {
                    currentSubset = ToTitleCase(a.Trim());
                    continue;
                }

                if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                {
                    preview.Warnings.Add($"Row {row.RowNumber()}: missing card number or player name; skipped.");
                    preview.RowsSkipped++;
                    continue;
                }

                preview.Cards.Add(new ChecklistCard
                {
                    CardNumber = a.Trim(),
                    PlayerName = b.Trim(),
                    Team = string.IsNullOrWhiteSpace(c) ? null : c.Trim(),
                    Subset = currentSubset,
                    IsRookie = !string.IsNullOrWhiteSpace(d) && (d.Contains("Rookie", StringComparison.OrdinalIgnoreCase) || d.Contains("RC", StringComparison.OrdinalIgnoreCase)),
                    IsAutograph = ContainsAny(currentSubset, "Autograph", "Auto", "Signature", "Scripts"),
                    IsParallel = LooksLikeParallel(currentSubset),
                    IsInsert = LooksLikeInsert(currentSubset),
                    Source = "checklist-insider",
                });
            }
        }

        private static int? TryInferYearFromCells(List<IXLRow> rows)
        {
            // Last-ditch: scan first ~20 cells for a year-shaped number. The xlsx body never
            // carries year directly, but some files include a copyright row at the top.
            foreach (var row in rows.Take(20))
            {
                foreach (var cell in row.Cells())
                {
                    var text = cell.GetString();
                    var m = Regex.Match(text ?? "", "(19[9]\\d|20\\d{2})");
                    if (m.Success && int.TryParse(m.Value, out var year))
                        return year;
                }
            }
            return null;
        }

        private static string TextOf(IXLRow row, int column)
        {
            try
            {
                var cell = row.Cell(column);
                if (cell == null || cell.IsEmpty()) return string.Empty;
                return cell.GetString()?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool MatchesAny(string text, string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var normalized = Regex.Replace(text.ToUpperInvariant(), "\\s+", " ").Trim();
            return tokens.Any(t => string.Equals(normalized, t, StringComparison.Ordinal));
        }

        private static bool ContainsAny(string? haystack, params string[] needles)
        {
            if (string.IsNullOrWhiteSpace(haystack)) return false;
            return needles.Any(n => haystack.Contains(n, StringComparison.OrdinalIgnoreCase));
        }

        private static bool LooksLikeParallel(string? subset)
        {
            if (string.IsNullOrWhiteSpace(subset)) return false;
            if (ContainsAny(subset, "Variation", "Parallel")) return true;

            // Anything that has a recognizable parallel suffix (color, finish) attached to a
            // base/insert/auto root is a parallel of that root. Heuristic only — preview UI
            // surfaces it for review.
            string[] parallelMarkers =
            {
                " Black", " Blue", " Red", " Gold", " Silver", " Purple", " Green", " Orange",
                " Pink", " White", " Bronze", " Refractor", " Prizm", " Wave", " Sparkle",
                " Holo", " Glitter", " Scope", " Tessellation", " Honeycomb", " Reactive",
                " Fluorescent", " Spectris", " Kaleidoscopic", " FOTL",
            };
            return parallelMarkers.Any(m => subset.Contains(m, StringComparison.OrdinalIgnoreCase));
        }

        private static bool LooksLikeInsert(string? subset)
        {
            if (string.IsNullOrWhiteSpace(subset)) return false;
            if (subset.Equals("Base", StringComparison.OrdinalIgnoreCase)) return false;
            if (ContainsAny(subset, "Autograph", "Auto", "Signature", "Scripts")) return false;
            if (LooksLikeParallel(subset)) return false;
            // What's left — named themed subsets like "Visionary", "Stained Glass", "Bang!" — are inserts.
            return !subset.StartsWith("Base", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeCardNumber(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            // Card numbers: short tokens dominated by digits/dashes (e.g. "1", "BCP-1", "BCPA-RA").
            if (s.Length > 12) return false;
            return s.Any(char.IsDigit);
        }

        private static bool LooksLikeSubsetName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            if (s.Length < 3) return false;
            if (s.Any(char.IsDigit)) return false;
            return s.Contains(' ') || char.IsLetter(s[0]);
        }

        private static bool IsDisclaimer(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var lower = s.ToLowerInvariant();
            return DisclaimerSnippets.Any(d => lower.Contains(d));
        }

        private static string ToTitleCase(string upper)
        {
            if (string.IsNullOrWhiteSpace(upper)) return upper;
            var parts = upper.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Length switch
                {
                    0 => w,
                    1 => w.ToUpperInvariant(),
                    _ => char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant(),
                });
            return string.Join(' ', parts);
        }
    }
}
