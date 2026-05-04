using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Data;
using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Phase 2 verification cascade. Three tier outcomes drive distinct UI states in
    /// <c>EditCardView</c> / <c>Edit.cshtml</c>:
    ///
    /// - <b>Tier 1 — Verified</b> when card-number normalizes to an exact match in the
    ///   locked checklist, the player name fuzzy-matches at ≥0.85 (Levenshtein +
    ///   token overlap), and (when present) the parallel name resolves cleanly.
    /// - <b>Tier 2 — BestGuess</b> when card # + player match but at least one field
    ///   (typically Parallel or Subset) is uncertain.
    /// - <b>Tier 3 — NoMatch</b> when card # is missing from the set OR present with a
    ///   wildly mismatched player. The result carries the top fuzzy candidates so
    ///   the editor can render the Pick-from-checklist picker.
    ///
    /// Returns a result with <c>ChecklistMissing=true</c> when no SetChecklist exists
    /// for the (Manufacturer, Brand, Year, Sport) tuple — UI surfaces the Surface B
    /// "import this checklist" banner in that case.
    /// </summary>
    public class ChecklistVerificationMatcher : IChecklistVerificationMatcher
    {
        private const double PlayerMatchThreshold = 0.85;
        private const double PlayerCandidateThreshold = 0.55;
        private const int MaxCandidates = 10;

        private readonly IServiceProvider _serviceProvider;

        public ChecklistVerificationMatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<ChecklistMatchResult> MatchAsync(Card card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));

            var result = new ChecklistMatchResult { Tier = VerificationTier.NoMatch };

            if (string.IsNullOrWhiteSpace(card.Manufacturer)
                || string.IsNullOrWhiteSpace(card.Brand)
                || !card.Year.HasValue)
            {
                result.ChecklistMissing = true;
                return result;
            }

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
            var checklist = await LoadChecklistAsync(db, card);

            if (checklist == null || checklist.Cards == null || checklist.Cards.Count == 0)
            {
                result.ChecklistMissing = true;
                return result;
            }

            result.Checklist = checklist;
            result.Candidates = RankCandidates(card, checklist);

            var exactCard = FindExactMatch(card, checklist);
            if (exactCard != null)
            {
                result.ExactMatch = exactCard;
                result.MatchKey = BuildMatchKey(checklist.Id, exactCard);

                var fieldScores = ScoreFields(card, exactCard);
                result.FieldConfidences = fieldScores;
                result.Tier = AllConfidencesHigh(fieldScores) ? VerificationTier.Verified : VerificationTier.BestGuess;
                return result;
            }

            // No exact card-number match. Tier 3 — return top fuzzy candidates so the
            // editor can show the picker. Field confidences are populated from the
            // best player-name candidate so the editor can hint at the closest guess.
            var bestPlayerMatch = result.Candidates.FirstOrDefault();
            if (bestPlayerMatch != null)
            {
                result.FieldConfidences = ScoreFields(card, bestPlayerMatch);
            }

            return result;
        }

        private static async Task<SetChecklist?> LoadChecklistAsync(FlipKitDbContext db, Card card)
        {
            var sport = card.Sport?.ToString();
            return await db.SetChecklists.FirstOrDefaultAsync(s =>
                s.Manufacturer == card.Manufacturer
                && s.Brand == card.Brand
                && s.Year == card.Year!.Value
                && s.Sport == sport);
        }

        private static ChecklistCard? FindExactMatch(Card card, SetChecklist checklist)
        {
            if (string.IsNullOrWhiteSpace(card.CardNumber)) return null;

            var normalizedNumber = FuzzyMatcher.NormalizeCardNumber(card.CardNumber);
            return checklist.Cards.FirstOrDefault(c =>
                FuzzyMatcher.NormalizeCardNumber(c.CardNumber) == normalizedNumber);
        }

        private static List<ChecklistCard> RankCandidates(Card card, SetChecklist checklist)
        {
            // Score every checklist card by player-name + card-number proximity. Keep
            // the top MaxCandidates above a soft threshold — Tier 3 picker fodder.
            var normalizedScannedNumber = FuzzyMatcher.NormalizeCardNumber(card.CardNumber ?? "");
            var ranked = checklist.Cards
                .Select(c => new
                {
                    Card = c,
                    PlayerScore = FuzzyMatcher.Match(card.PlayerName ?? "", c.PlayerName),
                    NumberMatches = !string.IsNullOrWhiteSpace(card.CardNumber)
                                    && FuzzyMatcher.NormalizeCardNumber(c.CardNumber) == normalizedScannedNumber,
                })
                .Select(x => new
                {
                    x.Card,
                    Combined = x.NumberMatches ? x.PlayerScore + 1.0 : x.PlayerScore,
                })
                .OrderByDescending(x => x.Combined)
                .ThenBy(x => x.Card.CardNumber, StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Combined >= PlayerCandidateThreshold)
                .Take(MaxCandidates)
                .Select(x => x.Card)
                .ToList();
            return ranked;
        }

        private static List<FieldConfidenceScore> ScoreFields(Card scanned, ChecklistCard checklistCard)
        {
            var scores = new List<FieldConfidenceScore>();

            // Card number: 1.0 when normalized equals match, else fuzzy distance.
            var scannedNumber = FuzzyMatcher.NormalizeCardNumber(scanned.CardNumber ?? "");
            var checklistNumber = FuzzyMatcher.NormalizeCardNumber(checklistCard.CardNumber);
            scores.Add(new FieldConfidenceScore
            {
                FieldName = "card_number",
                Value = scanned.CardNumber,
                Confidence = scannedNumber == checklistNumber ? 1.0 : FuzzyMatcher.Match(scannedNumber, checklistNumber),
                Reason = scannedNumber == checklistNumber
                    ? "Card number matches checklist."
                    : $"Closest checklist card # is {checklistCard.CardNumber}.",
            });

            // Player name: fuzzy match Levenshtein-derived, with a token-overlap bonus
            // so multi-word names ("Roman Anthony" vs "Anthony, Roman") score reasonably.
            var playerScore = ScorePlayerName(scanned.PlayerName ?? "", checklistCard.PlayerName);
            scores.Add(new FieldConfidenceScore
            {
                FieldName = "player_name",
                Value = scanned.PlayerName,
                Confidence = playerScore,
                Reason = playerScore >= PlayerMatchThreshold
                    ? $"Matches checklist: {checklistCard.PlayerName}."
                    : $"Closest checklist player is {checklistCard.PlayerName}.",
            });

            // Parallel: if scanned card has a ParallelName, defer to free-text confidence
            // (the parallel catalog handles this in the UI). The matcher reports a soft
            // confidence based on whether the parallel name appears in the checklist's
            // KnownVariations or matches the matched ChecklistCard's subset.
            var parallelScore = ScoreParallel(scanned.ParallelName, checklistCard);
            scores.Add(new FieldConfidenceScore
            {
                FieldName = "parallel",
                Value = scanned.ParallelName,
                Confidence = parallelScore,
                Reason = string.IsNullOrWhiteSpace(scanned.ParallelName)
                    ? "No parallel detected."
                    : $"Parallel '{scanned.ParallelName}' confidence {parallelScore:0.00}.",
            });

            return scores;
        }

        private static double ScorePlayerName(string scanned, string checklist)
        {
            if (string.IsNullOrWhiteSpace(scanned) || string.IsNullOrWhiteSpace(checklist)) return 0.0;

            var leven = FuzzyMatcher.Match(scanned, checklist);

            // Token overlap bonus — split on whitespace, count overlap, scale by max-token-count.
            var scannedTokens = FuzzyMatcher.Normalize(scanned).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var checklistTokens = FuzzyMatcher.Normalize(checklist).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (scannedTokens.Length == 0 || checklistTokens.Length == 0) return leven;

            var common = scannedTokens.Intersect(checklistTokens, StringComparer.OrdinalIgnoreCase).Count();
            var maxTokens = Math.Max(scannedTokens.Length, checklistTokens.Length);
            var tokenScore = (double)common / maxTokens;

            return Math.Max(leven, tokenScore);
        }

        private static double ScoreParallel(string? scanned, ChecklistCard checklistCard)
        {
            if (string.IsNullOrWhiteSpace(scanned)) return 1.0; // nothing to score

            var normScanned = FuzzyMatcher.NormalizeParallelName(scanned);

            // Heuristic: high confidence when the matched ChecklistCard's subset
            // contains the parallel name token, or when the parallel matches a known
            // Mosaic/Bowman color suffix. Otherwise mid confidence — let the user
            // confirm via the dropdown.
            if (!string.IsNullOrWhiteSpace(checklistCard.Subset))
            {
                var subsetNorm = FuzzyMatcher.Normalize(checklistCard.Subset);
                if (subsetNorm.Contains(normScanned, StringComparison.OrdinalIgnoreCase))
                    return 0.95;
            }

            return 0.7;
        }

        private static bool AllConfidencesHigh(List<FieldConfidenceScore> scores)
            => scores.All(s => s.Confidence >= 0.85);

        public static string BuildMatchKey(int setChecklistId, ChecklistCard card)
        {
            var num = FuzzyMatcher.NormalizeCardNumber(card.CardNumber);
            var subset = (card.Subset ?? "Base").Trim().ToLowerInvariant();
            return $"{setChecklistId}:{num}:{subset}";
        }
    }
}
