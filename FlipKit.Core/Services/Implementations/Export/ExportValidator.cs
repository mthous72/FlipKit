using System;
using System.Collections.Generic;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services.Export
{
    /// <summary>
    /// Per-card pre-flight validation against platform rules. Returns a list of errors;
    /// callers (the export dispatcher) collect across all rows and refuse to write the
    /// file if any blocking errors are present.
    ///
    /// The validator checks the source <see cref="Card"/>, not the serialized row — most
    /// rules are derivable from card properties without running the exporter, and the
    /// looser coupling means exporter refactors don't ripple into the validator.
    /// </summary>
    public class ExportValidator
    {
        private readonly WhatnotValuesProvider _whatnot;

        public ExportValidator(WhatnotValuesProvider whatnot)
        {
            _whatnot = whatnot;
        }

        public List<ExportRowError> ValidateForWhatnot(IList<Card> cards)
        {
            var errors = new List<ExportRowError>();
            foreach (var card in cards)
                errors.AddRange(ValidateOneForWhatnot(card));
            return errors;
        }

        public List<ExportRowError> ValidateForEbay(IList<Card> cards)
        {
            var errors = new List<ExportRowError>();
            foreach (var card in cards)
                errors.AddRange(ValidateOneForEbay(card));
            return errors;
        }

        // === Whatnot rules ===

        private IEnumerable<ExportRowError> ValidateOneForWhatnot(Card card)
        {
            // Identity / title
            if (string.IsNullOrWhiteSpace(card.PlayerName))
                yield return Err(card, nameof(card.PlayerName), "Player name is required.");

            // Price — Whatnot requires a positive integer at write time. Source price must
            // exist and be > 0; the exporter rounds to int and clamps to ≥ 1.
            if (!card.ListingPrice.HasValue || card.ListingPrice.Value <= 0)
                yield return Err(card, nameof(card.ListingPrice), "Listing price must be a positive number.");

            // Category / sub-category enum membership.
            if (!_whatnot.IsValidCategory(card.WhatnotCategory))
            {
                yield return Err(card, nameof(card.WhatnotCategory),
                    $"Whatnot category '{card.WhatnotCategory}' is not in the supported list.");
            }
            else if (!_whatnot.IsValidSubcategory(card.WhatnotCategory, card.WhatnotSubcategory))
            {
                if (string.IsNullOrEmpty(card.WhatnotSubcategory))
                {
                    var examples = string.Join(", ",
                        _whatnot.Subcategories.TryGetValue(card.WhatnotCategory, out var subs)
                            ? subs.Take(3) : System.Array.Empty<string>());
                    yield return Err(card, nameof(card.WhatnotSubcategory),
                        $"'{card.WhatnotCategory}' requires a sub-category. Examples: {examples}.");
                }
                else
                {
                    yield return Err(card, nameof(card.WhatnotSubcategory),
                        $"Sub-category '{card.WhatnotSubcategory}' is not valid for category '{card.WhatnotCategory}'.");
                }
            }

            // Quantity must be ≥ 1.
            if (card.Quantity < 1)
                yield return Err(card, nameof(card.Quantity), "Quantity must be at least 1.");

            // At least one hosted image URL is required (Whatnot rejects listings without
            // images). The user's already-uploaded slot 1 is the gallery image.
            if (string.IsNullOrEmpty(card.ImageUrl1))
                yield return Err(card, nameof(card.ImageUrl1),
                    "At least one image must be uploaded to ImgBB before exporting (use the Upload Images button).");

            // Every non-blank image URL must be HTTPS — Whatnot/eBay both refuse to fetch HTTP.
            foreach (var (slot, url) in EnumerateUrls(card))
            {
                if (string.IsNullOrEmpty(url)) continue;
                if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    yield return Err(card, $"ImageUrl{slot}",
                        $"Image URL must use HTTPS: '{Truncate(url, 60)}'.");
            }

            // Shipping profile — pass-through unknown strings (could be a custom profile),
            // warn rather than block.
            if (!string.IsNullOrWhiteSpace(card.ShippingProfile)
                && !_whatnot.IsValidShippingProfile(card.ShippingProfile)
                && !LooksLikeWeightString(card.ShippingProfile))
            {
                yield return new ExportRowError(card.Id, card.PlayerName, nameof(card.ShippingProfile),
                    $"'{card.ShippingProfile}' is not a known Whatnot bucket — assuming it's a custom seller profile saved on Whatnot.",
                    ExportErrorSeverity.Warning);
            }
        }

        // === eBay rules ===

        private IEnumerable<ExportRowError> ValidateOneForEbay(Card card)
        {
            // Identity / title
            if (string.IsNullOrWhiteSpace(card.PlayerName))
                yield return Err(card, nameof(card.PlayerName), "Player name is required.");

            // Price — eBay accepts decimal but still requires a positive value.
            if (!card.ListingPrice.HasValue || card.ListingPrice.Value <= 0)
                yield return Err(card, nameof(card.ListingPrice), "Listing price must be a positive number.");

            // Sport — required for *C:Sport.
            if (card.Sport is null)
                yield return Err(card, nameof(card.Sport),
                    "Sport is required for eBay's Sports Trading Cards category.");

            // Quantity ≥ 1.
            if (card.Quantity < 1)
                yield return Err(card, nameof(card.Quantity), "Quantity must be at least 1.");

            // At least one hosted image URL.
            if (string.IsNullOrEmpty(card.ImageUrl1))
                yield return Err(card, nameof(card.ImageUrl1),
                    "At least one image must be uploaded to ImgBB before exporting (use the Upload Images button).");

            // HTTPS check on all image URLs.
            foreach (var (slot, url) in EnumerateUrls(card))
            {
                if (string.IsNullOrEmpty(url)) continue;
                if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    yield return Err(card, $"ImageUrl{slot}",
                        $"Image URL must use HTTPS: '{Truncate(url, 60)}'.");
                if (url.Contains(' '))
                    yield return Err(card, $"ImageUrl{slot}",
                        "Image URL contains a space — encode as %20.");
            }

            // Graded-card descriptors — eBay's CD:* fields require company + grade for ConditionID 4000.
            if (card.IsGraded)
            {
                if (string.IsNullOrWhiteSpace(card.GradeCompany))
                    yield return Err(card, nameof(card.GradeCompany),
                        "Graded cards require a grading company (PSA, BGS, etc.).");
                if (string.IsNullOrWhiteSpace(card.GradeValue))
                    yield return Err(card, nameof(card.GradeValue),
                        "Graded cards require a numeric grade.");
            }
        }

        // === helpers ===

        private static IEnumerable<(int Slot, string? Url)> EnumerateUrls(Card card)
        {
            yield return (1, card.ImageUrl1);
            yield return (2, card.ImageUrl2);
            yield return (3, card.ImageUrl3);
            yield return (4, card.ImageUrl4);
            yield return (5, card.ImageUrl5);
            yield return (6, card.ImageUrl6);
            yield return (7, card.ImageUrl7);
            yield return (8, card.ImageUrl8);
        }

        private static ExportRowError Err(Card card, string field, string message) =>
            new(card.Id, card.PlayerName, field, message);

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, max) + "...";

        private static bool LooksLikeWeightString(string s)
        {
            // Cheap check — anything containing "oz", "lb", "kg", "gram" is probably a weight
            // the normalizer can handle, so suppress the custom-profile warning for these.
            var lower = s.ToLowerInvariant();
            return lower.Contains("oz") || lower.Contains("lb") || lower.Contains("kg") || lower.Contains("gram");
        }
    }
}
