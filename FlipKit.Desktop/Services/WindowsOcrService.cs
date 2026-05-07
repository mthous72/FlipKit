using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using Microsoft.Extensions.Logging;

namespace FlipKit.Desktop.Services
{
    public class WindowsOcrService : IOcrService
    {
        private readonly ILogger<WindowsOcrService>? _logger;
        private readonly IPlayerNameDirectory? _playerDirectory;

        public WindowsOcrService(
            ILogger<WindowsOcrService>? logger = null,
            IPlayerNameDirectory? playerDirectory = null)
        {
            _logger = logger;
            _playerDirectory = playerDirectory;
        }

        public bool IsAvailable
        {
            get
            {
                if (!OperatingSystem.IsWindowsVersionAtLeast(10))
                    return false;
                try
                {
                    var lang = new Windows.Globalization.Language("en");
                    return Windows.Media.Ocr.OcrEngine.IsLanguageSupported(lang);
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task<ScanResult> ScanCardAsync(string imagePath, string? backImagePath = null)
        {
            var frontLines = await RunOcrOnImageAsync(imagePath);
            List<string>? backLines = null;

            if (!string.IsNullOrEmpty(backImagePath) && File.Exists(backImagePath))
                backLines = await RunOcrOnImageAsync(backImagePath);

            // Build the parse context from the checklist directory — manufacturers,
            // brands, and team tokens that the parser uses to recognize fields
            // without carrying hardcoded catalog data of its own.
            var parseContext = BuildParseContext();

            // Pass front and back separately so the parser can boost candidates
            // (player name, team) that appear on both sides — a strong signal.
            var (card, confidences) = OcrTextParser.Parse(frontLines, backLines, parseContext);
            card.ImagePathFront = imagePath;
            card.ImagePathBack = backImagePath;
            card.DataSource = CardDataSource.Ocr;
            card.Status = CardStatus.Draft;

            // Reality-check the parsed fields against the checklist directory.
            // The shape heuristics happily pick "Red Hot Rookies" as a player; OCR
            // happily reads "Pannini" as the brand; the directory hits only real
            // values present in the user's imported checklists. Each gate is
            // independent — failure in one doesn't block another.
            if (_playerDirectory != null && _playerDirectory.IsReady)
            {
                ResolvePlayerNameFromDirectory(card, frontLines, backLines, confidences, parseContext);
                ResolveBrandFromDirectory(card, confidences);
                ResolveYearFromDirectory(card, confidences);
                ResolveSportFromTeam(card, frontLines, backLines, confidences);
            }

            // Combined view for downstream consumers (the scan result history,
            // enhance ticker preview, etc.).
            var allText = new List<string>(frontLines);
            if (backLines != null) allText.AddRange(backLines);

            return new ScanResult
            {
                Card = card,
                AllVisibleText = allText,
                Confidences = confidences,
                VisualCues = null,
            };
        }

        /// <summary>
        /// Snapshots the directory's catalog data into a parse context. Returns
        /// <see cref="OcrParseContext.Empty"/> when the directory isn't injected
        /// or hasn't loaded — the parser then runs with no catalog gates, which
        /// is the right behavior on a fresh install before any imports.
        /// </summary>
        private OcrParseContext BuildParseContext()
        {
            if (_playerDirectory == null || !_playerDirectory.IsReady)
                return OcrParseContext.Empty;

            return new OcrParseContext
            {
                Manufacturers = _playerDirectory.Manufacturers,
                Brands = _playerDirectory.Brands,
                Parallels = _playerDirectory.Parallels,
                TeamTokens = _playerDirectory.TeamTokens,
            };
        }

        private void ResolvePlayerNameFromDirectory(
            Card card, List<string> frontLines, List<string>? backLines,
            List<FieldConfidence> confidences, OcrParseContext context)
        {
            var candidates = OcrTextParser.ExtractPlayerNameCandidates(frontLines, backLines, context);
            PlayerNameMatch? best = null;
            foreach (var c in candidates)
            {
                var match = _playerDirectory!.FindBestMatch(c.Name);
                if (match == null) continue;
                if (best == null || match.Score > best.Score) best = match;
            }
            if (best == null) return;

            if (!string.Equals(card.PlayerName, best.Name, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogInformation(
                    "PlayerName overridden by checklist match: '{Old}' -> '{New}' (score {Score})",
                    card.PlayerName, best.Name, best.Score);
            }
            card.PlayerName = best.Name;
            ReplaceConfidence(confidences, "player_name",
                best.Score >= 95 ? VerificationConfidence.High : VerificationConfidence.Medium,
                $"Fuzzy match against checklist (score {best.Score})");
        }

        /// <summary>
        /// If the parser picked a brand from its hardcoded list (Mosaic, Prizm,
        /// etc.), keep it as-is unless the directory has a higher-scoring real
        /// brand match — that catches OCR misspellings ("Pannini" → "Panini") and
        /// updates the field to the canonical capitalization the user has on file.
        /// </summary>
        private void ResolveBrandFromDirectory(Card card, List<FieldConfidence> confidences)
        {
            if (string.IsNullOrWhiteSpace(card.Brand)) return;
            var match = _playerDirectory!.FindBrand(card.Brand);
            if (match == null) return;
            if (string.Equals(card.Brand, match.Value, StringComparison.OrdinalIgnoreCase)) return;
            _logger?.LogInformation("Brand normalized via checklist: '{Old}' -> '{New}' (score {Score})",
                card.Brand, match.Value, match.Score);
            card.Brand = match.Value;
            ReplaceConfidence(confidences, "brand", VerificationConfidence.Medium,
                $"Fuzzy match against checklist (score {match.Score})");
        }

        /// <summary>
        /// OCR's year regex picks any 4-digit number in the range 1950–2039, which
        /// will happily latch onto a copyright "1996" on the card back. Drop the
        /// parsed year if it doesn't appear in any imported checklist.
        /// </summary>
        private void ResolveYearFromDirectory(Card card, List<FieldConfidence> confidences)
        {
            if (!card.Year.HasValue) return;
            if (_playerDirectory!.IsKnownYear(card.Year.Value)) return;
            _logger?.LogInformation("Year {Year} not present in any imported checklist — clearing OCR pick.",
                card.Year.Value);
            card.Year = null;
            confidences.RemoveAll(c => c.FieldName == "year");
        }

        /// <summary>
        /// Walks every line of OCR output and asks the directory for a sport
        /// match — full team name, city, mascot, or alias all hit. The first
        /// match wins, with full-team-name candidates preferred since they
        /// disambiguate cross-sport name collisions ("Giants" = NFL or MLB,
        /// but "New York Giants" / "San Francisco Giants" doesn't).
        /// </summary>
        private void ResolveSportFromTeam(
            Card card, List<string> frontLines, List<string>? backLines, List<FieldConfidence> confidences)
        {
            // Skip if the parser already populated Sport (rare today, but keeps
            // the policy explicit: directory infers, never overrides).
            if (card.Sport.HasValue) return;

            string? matchedSport = null;
            string? matchedSource = null;

            // Full-line scan first — multi-word matches ("Atlanta Falcons")
            // are unambiguous; single-token matches ("Falcons" alone) come
            // second so a clear hit beats an ambiguous one.
            var allLines = new List<string>(frontLines);
            if (backLines != null) allLines.AddRange(backLines);

            foreach (var line in allLines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                var sport = _playerDirectory!.GetSportForTeam(trimmed);
                if (sport != null && trimmed.Contains(' '))
                {
                    matchedSport = sport;
                    matchedSource = trimmed;
                    break;
                }
            }

            // Single-word fallback if no full-name match landed.
            if (matchedSport == null)
            {
                foreach (var line in allLines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Contains(' ')) continue;
                    var sport = _playerDirectory!.GetSportForTeam(trimmed);
                    if (sport != null) { matchedSport = sport; matchedSource = trimmed; break; }
                }
            }

            if (matchedSport == null) return;
            if (!Enum.TryParse<Sport>(matchedSport, ignoreCase: true, out var parsed)) return;

            card.Sport = parsed;
            confidences.Add(new FieldConfidence
            {
                FieldName = "sport",
                Confidence = VerificationConfidence.High,
                Reason = $"Inferred from team match '{matchedSource}'",
            });
            _logger?.LogInformation("Sport inferred from team '{Source}' -> {Sport}", matchedSource, parsed);
        }

        private static void ReplaceConfidence(
            List<FieldConfidence> confidences, string fieldName,
            VerificationConfidence confidence, string reason)
        {
            confidences.RemoveAll(c => c.FieldName == fieldName);
            confidences.Add(new FieldConfidence
            {
                FieldName = fieldName,
                Confidence = confidence,
                Reason = reason,
            });
        }

        private async Task<List<string>> RunOcrOnImageAsync(string imagePath)
        {
            var lines = new List<string>();
            string? preprocessedPath = null;

            try
            {
                preprocessedPath = OcrImagePreprocessor.Preprocess(imagePath);
                var pathToLoad = preprocessedPath != imagePath ? preprocessedPath : imagePath;

                var imageBytes = await File.ReadAllBytesAsync(pathToLoad);

                using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                await stream.WriteAsync(imageBytes.AsBuffer());
                stream.Seek(0);

                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                var bitmap = await decoder.GetSoftwareBitmapAsync(
                    Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                    Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);

                var lang = new Windows.Globalization.Language("en");
                var engine = Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(lang);
                if (engine == null)
                    return lines;

                var result = await engine.RecognizeAsync(bitmap);
                foreach (var line in result.Lines)
                    lines.Add(line.Text);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "OCR failed for image {Path}", imagePath);
            }
            finally
            {
                if (preprocessedPath != null && preprocessedPath != imagePath
                    && File.Exists(preprocessedPath))
                {
                    try { File.Delete(preprocessedPath); } catch { /* best effort */ }
                }
            }

            return lines;
        }
    }
}
