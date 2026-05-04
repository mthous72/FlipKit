using System.Collections.Generic;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Models
{
    /// <summary>
    /// Tier outcome from <c>IChecklistVerificationMatcher</c>. Drives how the editor
    /// renders the card on save: Tier 1 = green ✓ + one-click save, Tier 2 = yellow
    /// ⚠ with uncertain fields highlighted, Tier 3 = amber Pick-from-checklist panel.
    /// </summary>
    public enum VerificationTier
    {
        Verified = 1,
        BestGuess = 2,
        NoMatch = 3,
    }

    /// <summary>Per-field confidence carried alongside the tier so the editor can highlight uncertain inputs.</summary>
    public class FieldConfidenceScore
    {
        public string FieldName { get; set; } = string.Empty;
        public string? Value { get; set; }
        public double Confidence { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Single result from running a <see cref="Card"/> through the checklist matcher.
    /// Identifies the tier, the exact match (if any), the top fuzzy-ranked candidates
    /// (Tier 3 picker fodder), and per-field confidences for the editor decoration.
    /// </summary>
    public class ChecklistMatchResult
    {
        public VerificationTier Tier { get; set; }

        /// <summary>The matched ChecklistCard when Tier is Verified or BestGuess; null on NoMatch.</summary>
        public ChecklistCard? ExactMatch { get; set; }

        /// <summary>The SetChecklist the match was scored against; null when no checklist exists for this set.</summary>
        public SetChecklist? Checklist { get; set; }

        /// <summary>The composite key written to <c>Card.MatchedChecklistKey</c> on save. Empty on NoMatch.</summary>
        public string MatchKey { get; set; } = string.Empty;

        /// <summary>Top fuzzy-ranked candidates for the Pick-from-checklist picker (max ~10).</summary>
        public List<ChecklistCard> Candidates { get; set; } = new();

        /// <summary>Per-field confidences (card_number, player_name, parallel, etc.).</summary>
        public List<FieldConfidenceScore> FieldConfidences { get; set; } = new();

        /// <summary>Set when no SetChecklist exists for this Card's (Manufacturer, Brand, Year, Sport) tuple.</summary>
        public bool ChecklistMissing { get; set; }
    }

    /// <summary>
    /// One option in the Parallel dropdown shown in the editor when a card's set has
    /// known parallels. Bundled in <c>ParallelFamilyCatalog.json</c> for common modern
    /// releases; sets without a catalog entry fall through to free-text Parallel.
    /// </summary>
    public class ParallelOption
    {
        public string Name { get; set; } = string.Empty;
        public bool Numbered { get; set; }
        public int? PrintRun { get; set; }
    }
}
