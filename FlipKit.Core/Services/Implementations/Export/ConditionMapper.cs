using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services.Export
{
    /// <summary>
    /// Translates a card's condition into platform-specific values:
    ///   • Whatnot: a string from the per-(sub)category allowed list, via heuristic ladder.
    ///   • eBay:   a numeric ConditionID (1000/2750/3000/4000/5000/7000) for Sports Cards.
    /// Spec reference: card_listings_export_spec.md §4.2 (Whatnot) and §3.5 / §4.3 (eBay).
    /// </summary>
    public static class ConditionMapper
    {
        // Heuristic ladder from spec §4.2 — ordered Whatnot preferences per raw-text pattern.
        // First match in the regex list wins; within each match, the first preference present
        // in the allowed list wins. Keep these in declaration order — order matters because
        // multiple patterns can match (e.g. "near mint" matches both \bmint\b and \bnear mint\b).
        // Sports-card sub-categories (Football Singles, Baseball Singles, etc.) use a "Raw - X"
        // naming convention; those values are appended to each pref list so the mapper finds
        // them when the standard names aren't allowed.
        private static readonly (Regex Pattern, string[] Prefs)[] Ladder = new (Regex, string[])[]
        {
            (Re(@"\bbrand new\b|\bnew with tags?\b|\bnwt\b|\bsealed\b|^new$"),
                new[] { "New", "Sealed", "Brand New", "Mint", "Raw - Near Mint or Better" }),
            (Re(@"\bnew without\b|\bnwot\b|\bopen box\b"),
                new[] { "New", "Mint", "Near Mint", "Raw - Near Mint or Better" }),
            (Re(@"\blike new\b"),
                new[] { "Mint", "Near Mint", "Raw - Near Mint or Better", "Excellent", "New" }),
            (Re(@"\bnear mint\b"),
                new[] { "Near Mint", "Raw - Near Mint or Better", "Mint", "Excellent", "Raw - Excellent" }),
            (Re(@"\bmint\b"),
                new[] { "Mint", "Near Mint", "Raw - Near Mint or Better" }),
            (Re(@"\bexcellent\b|\bvery good\b"),
                new[] { "Excellent", "Raw - Excellent", "Near Mint", "Raw - Near Mint or Better", "Very Good", "Raw - Very Good", "Good", "Light Played" }),
            (Re(@"\bgood\b"),
                new[] { "Good", "Raw - Very Good", "Light Played", "Used", "Lightly Used" }),
            (Re(@"\bused\b|\bpre[- ]?owned\b"),
                new[] { "Used", "Good", "Light Played", "Raw - Excellent", "Pre-Owned" }),
            (Re(@"\bacceptable\b|\bfair\b"),
                new[] { "Fair", "Moderately Played", "Played", "Used", "Heavily Played", "Raw - Poor" }),
            (Re(@"\bfor parts\b|\bnot working\b|\bpoor\b|\bdamaged\b"),
                new[] { "Damaged", "Heavily Played", "Poor", "Raw - Poor", "For Parts", "Played" }),
        };

        private static Regex Re(string p) =>
            new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Picks the best Whatnot Condition string for the given card, given the allowed values
        /// for the resolved (sub)category. Graded cards prefer "Graded" if it's in the allowed list.
        /// Returns null if no reasonable match exists — caller decides whether to fall back to a
        /// default or leave the column blank.
        /// </summary>
        public static string? MapToWhatnot(Card card, IReadOnlyList<string> allowed)
        {
            if (allowed == null || allowed.Count == 0)
                return null;

            if (card.IsGraded)
            {
                var graded = allowed.FirstOrDefault(c => c.Equals("Graded", StringComparison.OrdinalIgnoreCase));
                if (graded != null) return graded;
            }

            var raw = card.Condition;
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            // Exact match (case-insensitive) wins outright.
            var exact = allowed.FirstOrDefault(c => c.Equals(raw, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            // Walk the heuristic ladder.
            foreach (var (pattern, prefs) in Ladder)
            {
                if (!pattern.IsMatch(raw)) continue;
                foreach (var pref in prefs)
                {
                    var hit = allowed.FirstOrDefault(c => c.Equals(pref, StringComparison.OrdinalIgnoreCase));
                    if (hit != null) return hit;
                }
            }

            return null;
        }

        /// <summary>
        /// Picks the eBay Sports Trading Cards ConditionID for the given card.
        /// Spec §3.5 / §4.3:
        ///   1000 New, 2750 Like New, 3000 Used, 4000 Graded, 5000 Ungraded, 7000 For parts.
        /// Graded cards always 4000. Raw cards default to 5000 (Ungraded), which is the
        /// correct label for any unslabbed card regardless of its card-grade descriptor —
        /// "Mint" / "Near Mint" / "Excellent" describe a card's condition on the grading
        /// scale, NOT a new-from-pack state. Only sealed/factory-new tokens map to 1000.
        /// </summary>
        public static int MapToEbayConditionId(Card card)
        {
            if (card.IsGraded) return 4000;

            var raw = (card.Condition ?? string.Empty).ToLowerInvariant();
            if (raw.Length == 0) return 5000;

            if (ContainsAny(raw, "for parts", "not working", "damaged"))
                return 7000;
            if (ContainsAny(raw, "brand new", "sealed", "factory", "nwt"))
                return 1000;

            return 5000;
        }

        private static bool ContainsAny(string s, params string[] needles)
        {
            foreach (var n in needles)
                if (s.Contains(n)) return true;
            return false;
        }
    }
}
