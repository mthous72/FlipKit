using System;
using System.Collections.Generic;

namespace FlipKit.Core.Models
{
    /// <summary>
    /// Carries OCR-extracted (and optionally directory-validated) field values
    /// into a downstream LLM scan call. Two modes:
    ///   * Soft-hint mode (legacy, default): all fields are suggestions; the
    ///     LLM is told to verify with its vision and may override anything.
    ///   * Verified-fields mode: when <see cref="VerifiedFieldNames"/> is
    ///     non-empty, the LLM is told to echo those fields verbatim and spend
    ///     its capacity on the unspecified visual-pattern fields. Used by the
    ///     Bulk Scan "Enhance with AI" flow on OCR-scanned cards.
    /// </summary>
    public class OcrHint
    {
        // Identity
        public string? PlayerName { get; set; }
        public int? Year { get; set; }
        public string? CardNumber { get; set; }
        public string? Manufacturer { get; set; }
        public string? Brand { get; set; }
        public string? SetName { get; set; }
        public string? Team { get; set; }
        public string? Sport { get; set; }

        // Variation
        public string? ParallelName { get; set; }
        public string? SerialNumbered { get; set; }

        // Card-type flags
        public bool? IsRookie { get; set; }
        public bool? IsAuto { get; set; }
        public bool? IsRelic { get; set; }

        // Grading
        public bool? IsGraded { get; set; }
        public string? GradeCompany { get; set; }
        public string? GradeValue { get; set; }

        /// <summary>Raw OCR lines from the front + back. Helps the LLM see
        /// peripheral text (set name, copyright lines, mascot) without
        /// re-running its own OCR pass.</summary>
        public List<string> AllVisibleText { get; set; } = new();

        /// <summary>
        /// Names of hint fields whose values are confirmed (matched against
        /// the checklist directory or extracted with high confidence). The
        /// LLM is instructed to echo these verbatim in its JSON response and
        /// to avoid re-deriving them. Field names match the JSON schema keys
        /// (e.g. "player_name", "year", "manufacturer", "parallel_name",
        /// "is_graded"). Empty by default → soft-hint mode.
        /// </summary>
        public HashSet<string> VerifiedFieldNames { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }
}
