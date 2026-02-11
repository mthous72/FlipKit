using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FlipKit.Core.Data;
using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace FlipKit.Core.Services
{
    public class VariationVerifierService : IVariationVerifier
    {
        private readonly FlipKitDbContext _db;
        private readonly IScannerService _scannerService;
        private readonly ISettingsService _settingsService;

        private const double PlayerNameThreshold = 0.85;
        private const double ParallelFuzzyThreshold = 0.7;

        public VariationVerifierService(
            FlipKitDbContext db,
            IScannerService scannerService,
            ISettingsService settingsService)
        {
            _db = db;
            _scannerService = scannerService;
            _settingsService = settingsService;
        }

        public async Task<VerificationResult> VerifyCardAsync(ScanResult scanResult, string imagePath)
        {
            var result = new VerificationResult
            {
                OverallConfidence = VerificationConfidence.Medium
            };

            var card = scanResult.Card;

            if (string.IsNullOrWhiteSpace(card.Manufacturer) || string.IsNullOrWhiteSpace(card.Brand) || !card.Year.HasValue)
            {
                result.OverallConfidence = VerificationConfidence.Low;
                result.Warnings.Add("Missing manufacturer, brand, or year — cannot verify against checklist.");
                return result;
            }

            var checklist = await GetChecklistAsync(
                card.Manufacturer,
                card.Brand,
                card.Year.Value,
                card.Sport?.ToString());

            if (checklist == null)
            {
                result.OverallConfidence = VerificationConfidence.Low;
                result.Warnings.Add("Set checklist not available for this product.");
                await LogMissingChecklistAsync(card.Manufacturer, card.Brand, card.Year.Value, card.Sport?.ToString());
                return result;
            }

            result.ChecklistMatch = true;

            // Verify card number
            VerifyCardNumber(card, checklist, result);

            // Verify player name
            VerifyPlayerName(card, checklist, result);

            // Verify parallel/variation
            VerifyVariation(card, checklist, result);

            // Cross-reference visual cues
            if (scanResult.VisualCues != null)
                ValidateVisualCues(scanResult, checklist, result);

            // Calculate overall confidence
            result.OverallConfidence = CalculateOverallConfidence(result);

            return result;
        }

        public async Task<SetChecklist?> GetChecklistAsync(string manufacturer, string brand, int year, string? sport = null)
        {
            var normManufacturer = FuzzyMatcher.Normalize(manufacturer);
            var normBrand = FuzzyMatcher.Normalize(brand);

            var checklists = await _db.SetChecklists.ToListAsync();

            return checklists.FirstOrDefault(c =>
                FuzzyMatcher.Normalize(c.Manufacturer) == normManufacturer &&
                FuzzyMatcher.Normalize(c.Brand) == normBrand &&
                c.Year == year &&
                (sport == null || c.Sport == null ||
                 FuzzyMatcher.Normalize(c.Sport) == FuzzyMatcher.Normalize(sport)));
        }

        public bool NeedsConfirmationPass(VerificationResult result)
        {
            if (result.OverallConfidence is VerificationConfidence.Low or VerificationConfidence.Conflict)
                return true;

            if (result.Suggestions.Count > 0)
                return true;

            if (result.FieldConfidences.Any(f => f.Confidence == VerificationConfidence.Conflict))
                return true;

            return false;
        }

        public async Task<VerificationResult> RunConfirmationPassAsync(
            ScanResult scanResult, VerificationResult verification, string imagePath)
        {
            var prompt = BuildConfirmationPrompt(scanResult, verification);
            var settings = _settingsService.Load();

            try
            {
                var response = await _scannerService.SendCustomPromptAsync(imagePath, prompt, backImagePath: null, model: settings.DefaultModel);
                response = StripCodeBlocks(response);

                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (root.TryGetProperty("variation_confirmed", out var variation))
                {
                    var confirmed = variation.GetString();
                    if (!string.IsNullOrWhiteSpace(confirmed))
                    {
                        verification.SuggestedVariation = confirmed;
                        var existingSuggestion = verification.Suggestions
                            .FirstOrDefault(s => s.Contains("parallel", StringComparison.OrdinalIgnoreCase));
                        if (existingSuggestion != null)
                            verification.Suggestions.Remove(existingSuggestion);

                        verification.Suggestions.Add($"Confirmation pass identified variation as: {confirmed}");

                        // Update variation confidence
                        var variationField = verification.FieldConfidences
                            .FirstOrDefault(f => f.FieldName == "parallel_name");
                        if (variationField != null)
                        {
                            variationField.Confidence = VerificationConfidence.Medium;
                            variationField.Reason = $"Confirmed by targeted re-ask: {confirmed}";
                        }
                    }
                }

                if (root.TryGetProperty("player_confirmed", out var player))
                {
                    var confirmedPlayer = player.GetString();
                    if (!string.IsNullOrWhiteSpace(confirmedPlayer))
                    {
                        verification.SuggestedPlayerName = confirmedPlayer;
                    }
                }

                if (root.TryGetProperty("is_numbered", out var numbered) && numbered.GetString() == "yes")
                {
                    if (root.TryGetProperty("serial_text", out var serialText))
                    {
                        var serial = serialText.GetString();
                        if (!string.IsNullOrWhiteSpace(serial))
                        {
                            verification.Suggestions.Add($"Serial number detected: {serial}");
                        }
                    }
                }
            }
            catch
            {
                verification.Warnings.Add("Confirmation pass failed — using initial scan results.");
            }

            verification.OverallConfidence = CalculateOverallConfidence(verification);
            return verification;
        }

        private void VerifyCardNumber(Card card, SetChecklist checklist, VerificationResult result)
        {
            if (string.IsNullOrWhiteSpace(card.CardNumber))
            {
                result.FieldConfidences.Add(new FieldConfidence
                {
                    FieldName = "card_number",
                    Value = null,
                    Confidence = VerificationConfidence.Low,
                    Reason = "Card number not detected"
                });
                return;
            }

            var normalizedNumber = FuzzyMatcher.NormalizeCardNumber(card.CardNumber);
            var match = checklist.Cards.FirstOrDefault(c =>
                FuzzyMatcher.NormalizeCardNumber(c.CardNumber) == normalizedNumber);

            if (match != null)
            {
                result.CardNumberVerified = true;
                result.FieldConfidences.Add(new FieldConfidence
                {
                    FieldName = "card_number",
                    Value = card.CardNumber,
                    Confidence = VerificationConfidence.High,
                    Reason = $"Card #{normalizedNumber} found in {checklist.Brand} checklist"
                });
            }
            else
            {
                result.FieldConfidences.Add(new FieldConfidence
                {
                    FieldName = "card_number",
                    Value = card.CardNumber,
                    Confidence = VerificationConfidence.Low,
                    Reason = $"Card #{normalizedNumber} not found in {checklist.Brand} checklist (has {checklist.TotalBaseCards} cards)"
                });
                result.Warnings.Add($"Card #{card.CardNumber} not found in the {checklist.Year} {checklist.Brand} checklist.");
            }
        }

        private void VerifyPlayerName(Card card, SetChecklist checklist, VerificationResult result)
        {
            if (string.IsNullOrWhiteSpace(card.PlayerName) || card.PlayerName == "Unknown Player")
            {
                result.FieldConfidences.Add(new FieldConfidence
                {
                    FieldName = "player_name",
                    Value = card.PlayerName,
                    Confidence = VerificationConfidence.Low,
                    Reason = "Player name not detected"
                });
                return;
            }

            // First try exact card number match to verify player
            var normalizedNumber = FuzzyMatcher.NormalizeCardNumber(card.CardNumber ?? "");
            var numberMatch = checklist.Cards.FirstOrDefault(c =>
                FuzzyMatcher.NormalizeCardNumber(c.CardNumber) == normalizedNumber);

            if (numberMatch != null)
            {
                var nameScore = FuzzyMatcher.Match(card.PlayerName, numberMatch.PlayerName);
                if (nameScore >= PlayerNameThreshold)
                {
                    result.PlayerVerified = true;
                    result.FieldConfidences.Add(new FieldConfidence
                    {
                        FieldName = "player_name",
                        Value = card.PlayerName,
                        Confidence = VerificationConfidence.High,
                        Reason = $"Matches checklist: {numberMatch.PlayerName} (#{numberMatch.CardNumber})"
                    });
                }
                else
                {
                    result.FieldConfidences.Add(new FieldConfidence
                    {
                        FieldName = "player_name",
                        Value = card.PlayerName,
                        Confidence = VerificationConfidence.Conflict,
                        Reason = $"Card #{normalizedNumber} should be {numberMatch.PlayerName}, not {card.PlayerName}"
                    });
                    result.SuggestedPlayerName = numberMatch.PlayerName;
                    result.Suggestions.Add($"Player name mismatch: AI said \"{card.PlayerName}\" but card #{card.CardNumber} is {numberMatch.PlayerName}. Accept correction?");
                }
                return;
            }

            // No card number match — try fuzzy name match against all cards
            var bestMatch = checklist.Cards
                .Select(c => new { Card = c, Score = FuzzyMatcher.Match(card.PlayerName, c.PlayerName) })
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (bestMatch != null && bestMatch.Score >= PlayerNameThreshold)
            {
                result.PlayerVerified = true;
                result.FieldConfidences.Add(new FieldConfidence
                {
                    FieldName = "player_name",
                    Value = card.PlayerName,
                    Confidence = VerificationConfidence.Medium,
                    Reason = $"Fuzzy match to {bestMatch.Card.PlayerName} ({bestMatch.Score:P0})"
                });
            }
            else
            {
                result.FieldConfidences.Add(new FieldConfidence
                {
                    FieldName = "player_name",
                    Value = card.PlayerName,
                    Confidence = VerificationConfidence.Low,
                    Reason = "Player not found in set checklist"
                });
            }
        }

        private void VerifyVariation(Card card, SetChecklist checklist, VerificationResult result)
        {
            var parallelName = card.ParallelName;

            if (string.IsNullOrWhiteSpace(parallelName) && card.VariationType == "Base")
            {
                if (checklist.KnownVariations.Any(v =>
                    FuzzyMatcher.Normalize(v) == "base"))
                {
                    result.VariationVerified = true;
                    result.FieldConfidences.Add(new FieldConfidence
                    {
                        FieldName = "parallel_name",
                        Value = "Base",
                        Confidence = VerificationConfidence.High,
                        Reason = "Base card — verified"
                    });
                }
                else
                {
                    result.FieldConfidences.Add(new FieldConfidence
                    {
                        FieldName = "parallel_name",
                        Value = "Base",
                        Confidence = VerificationConfidence.Medium,
                        Reason = "Base card assumed"
                    });
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(parallelName))
            {
                result.FieldConfidences.Add(new FieldConfidence
                {
                    FieldName = "parallel_name",
                    Value = null,
                    Confidence = VerificationConfidence.Low,
                    Reason = "No parallel name detected"
                });
                return;
            }

            var normalizedParallel = FuzzyMatcher.NormalizeParallelName(parallelName);

            // Exact match against known variations
            var exactMatch = checklist.KnownVariations.FirstOrDefault(v =>
                FuzzyMatcher.NormalizeParallelName(v) == normalizedParallel);

            if (exactMatch != null)
            {
                result.VariationVerified = true;
                result.FieldConfidences.Add(new FieldConfidence
                {
                    FieldName = "parallel_name",
                    Value = parallelName,
                    Confidence = VerificationConfidence.High,
                    Reason = $"Matches known variation: {exactMatch}"
                });
                return;
            }

            // Fuzzy match against known variations
            var bestVariation = checklist.KnownVariations
                .Select(v => new { Name = v, Score = FuzzyMatcher.Match(normalizedParallel, FuzzyMatcher.NormalizeParallelName(v)) })
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (bestVariation != null && bestVariation.Score >= ParallelFuzzyThreshold)
            {
                result.FieldConfidences.Add(new FieldConfidence
                {
                    FieldName = "parallel_name",
                    Value = parallelName,
                    Confidence = VerificationConfidence.Medium,
                    Reason = $"Close match to: {bestVariation.Name} ({bestVariation.Score:P0})"
                });
                result.SuggestedVariation = bestVariation.Name;
                result.Suggestions.Add($"AI identified parallel as \"{parallelName}\" — did you mean \"{bestVariation.Name}\"?");
            }
            else
            {
                result.FieldConfidences.Add(new FieldConfidence
                {
                    FieldName = "parallel_name",
                    Value = parallelName,
                    Confidence = VerificationConfidence.Conflict,
                    Reason = $"\"{parallelName}\" not found in known variations for {checklist.Year} {checklist.Brand}"
                });
                result.Warnings.Add($"Parallel \"{parallelName}\" is not a known variation for {checklist.Year} {checklist.Brand}. Possible AI hallucination.");
            }
        }

        private void ValidateVisualCues(ScanResult scanResult, SetChecklist checklist, VerificationResult result)
        {
            var cues = scanResult.VisualCues!;
            var card = scanResult.Card;

            // Serial number visible but card not marked as numbered
            if (cues.HasSerialNumber && string.IsNullOrWhiteSpace(card.SerialNumbered))
            {
                result.Warnings.Add("Visual cue: Serial number detected but card is not marked as numbered.");
                result.Suggestions.Add("A serial number was detected on the card. This may be a numbered parallel.");
            }

            // Card marked as numbered but no serial visible
            if (!cues.HasSerialNumber && !string.IsNullOrWhiteSpace(card.SerialNumbered))
            {
                result.Warnings.Add("Card marked as serial numbered but no serial number was visually detected.");
            }

            // Foil/rainbow on what AI says is Base
            if ((cues.HasFoil || cues.HasRefractorPattern) &&
                card.VariationType == "Base" &&
                string.IsNullOrWhiteSpace(card.ParallelName))
            {
                result.Warnings.Add("Visual cue: Foil or refractor pattern detected on a card identified as Base.");
                result.Suggestions.Add("Foil/shimmer effect suggests this may be a parallel, not a base card.");
            }

            // Rookie logo visible but not marked as rookie
            if (cues.HasRookieLogo && !card.IsRookie)
            {
                result.Warnings.Add("Visual cue: Rookie logo detected but card is not marked as rookie.");
                result.Suggestions.Add("Rookie logo was detected — card should be marked as a rookie.");
            }

            // Auto sticker visible but not marked as auto
            if (cues.HasAutoSticker && !card.IsAuto)
            {
                result.Warnings.Add("Visual cue: Autograph sticker detected but card is not marked as auto.");
                result.Suggestions.Add("Autograph sticker detected — card should be marked as an auto.");
            }

            // Relic swatch visible but not marked as relic
            if (cues.HasRelicSwatch && !card.IsRelic)
            {
                result.Warnings.Add("Visual cue: Relic swatch detected but card is not marked as relic.");
                result.Suggestions.Add("Memorabilia swatch detected — card should be marked as a relic.");
            }
        }

        private string BuildConfirmationPrompt(ScanResult scanResult, VerificationResult verification)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Look at this sports card image again carefully and answer these specific questions.");
            sb.AppendLine("Return ONLY a JSON object with your answers.");
            sb.AppendLine();
            sb.AppendLine("{");

            var fields = new List<string>();

            // Check if variation needs confirmation
            if (verification.FieldConfidences.Any(f =>
                f.FieldName == "parallel_name" &&
                f.Confidence is VerificationConfidence.Conflict or VerificationConfidence.Low))
            {
                fields.Add("  \"variation_confirmed\": \"Exact name of the parallel/variation — look at border color, finish, pattern\"");
            }

            // Check if player name needs confirmation
            if (verification.SuggestedPlayerName != null)
            {
                fields.Add($"  \"player_confirmed\": \"Is this {scanResult.Card.PlayerName} or {verification.SuggestedPlayerName}? Return the correct name\"");
            }

            // Check for serial number questions
            if (verification.Warnings.Any(w => w.Contains("serial", StringComparison.OrdinalIgnoreCase)))
            {
                fields.Add("  \"is_numbered\": \"yes or no — is there a serial number like 045/199 printed on the card?\"");
                fields.Add("  \"serial_text\": \"The exact serial number text if visible, or null\"");
            }

            // Check for surface finish questions
            if (verification.Warnings.Any(w => w.Contains("foil", StringComparison.OrdinalIgnoreCase) ||
                                               w.Contains("refractor", StringComparison.OrdinalIgnoreCase)))
            {
                fields.Add("  \"surface_finish\": \"Describe the card's surface: matte, glossy, holographic, prizm shimmer, chrome refractor, etc.\"");
            }

            // Always ask about border color for variation confirmation
            fields.Add("  \"border_color\": \"What color is the card's border? silver, blue, red, green, gold, standard, etc.\"");

            sb.AppendLine(string.Join(",\n", fields));
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("Return ONLY the JSON, no other text or markdown.");

            return sb.ToString();
        }

        private static VerificationConfidence CalculateOverallConfidence(VerificationResult result)
        {
            if (result.FieldConfidences.Count == 0)
                return result.ChecklistMatch ? VerificationConfidence.Medium : VerificationConfidence.Low;

            if (result.FieldConfidences.Any(f => f.Confidence == VerificationConfidence.Conflict))
                return VerificationConfidence.Conflict;

            var lowCount = result.FieldConfidences.Count(f => f.Confidence == VerificationConfidence.Low);
            var highCount = result.FieldConfidences.Count(f => f.Confidence == VerificationConfidence.High);
            var total = result.FieldConfidences.Count;

            if (lowCount > total / 2)
                return VerificationConfidence.Low;

            if (highCount >= total / 2 && result.ChecklistMatch)
                return VerificationConfidence.High;

            return VerificationConfidence.Medium;
        }

        private async Task LogMissingChecklistAsync(string manufacturer, string brand, int year, string? sport)
        {
            try
            {
                var existing = await _db.MissingChecklists.FirstOrDefaultAsync(m =>
                    m.Manufacturer == manufacturer &&
                    m.Brand == brand &&
                    m.Year == year &&
                    m.Sport == sport);

                if (existing != null)
                {
                    existing.HitCount++;
                    existing.LastSeen = DateTime.UtcNow;
                }
                else
                {
                    _db.MissingChecklists.Add(new MissingChecklist
                    {
                        Manufacturer = manufacturer,
                        Brand = brand,
                        Year = year,
                        Sport = sport,
                        HitCount = 1,
                        FirstSeen = DateTime.UtcNow,
                        LastSeen = DateTime.UtcNow
                    });
                }

                await _db.SaveChangesAsync();
            }
            catch
            {
                // Non-fatal — don't block verification for logging failure
            }
        }

        private static string StripCodeBlocks(string content)
        {
            content = content.Trim();
            if (content.Contains("```json"))
                content = content.Split("```json")[1].Split("```")[0];
            else if (content.Contains("```"))
                content = content.Split("```")[1].Split("```")[0];
            return content.Trim();
        }
    }
}
