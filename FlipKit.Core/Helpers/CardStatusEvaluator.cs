using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Helpers
{
    /// <summary>
    /// Single source of truth for translating "what state is this card in?" into a
    /// <see cref="CardStatus"/>. The save flow on every editor (Scan, Edit, BulkScan)
    /// runs this after applying the user's edits and after auto-uploading any pending
    /// local images, so the persisted status reflects the post-save reality.
    /// </summary>
    public static class CardStatusEvaluator
    {
        /// <summary>
        /// Returns true if the card has any image attached — local path or hosted URL —
        /// in any of the eight slots.
        /// </summary>
        public static bool HasAnyImage(Card card)
        {
            if (!string.IsNullOrEmpty(card.ImagePathFront) || !string.IsNullOrEmpty(card.ImageUrl1)) return true;
            if (!string.IsNullOrEmpty(card.ImagePathBack)  || !string.IsNullOrEmpty(card.ImageUrl2)) return true;
            if (!string.IsNullOrEmpty(card.ImagePath3) || !string.IsNullOrEmpty(card.ImageUrl3)) return true;
            if (!string.IsNullOrEmpty(card.ImagePath4) || !string.IsNullOrEmpty(card.ImageUrl4)) return true;
            if (!string.IsNullOrEmpty(card.ImagePath5) || !string.IsNullOrEmpty(card.ImageUrl5)) return true;
            if (!string.IsNullOrEmpty(card.ImagePath6) || !string.IsNullOrEmpty(card.ImageUrl6)) return true;
            if (!string.IsNullOrEmpty(card.ImagePath7) || !string.IsNullOrEmpty(card.ImageUrl7)) return true;
            if (!string.IsNullOrEmpty(card.ImagePath8) || !string.IsNullOrEmpty(card.ImageUrl8)) return true;
            return false;
        }

        public static bool HasPrice(Card card) =>
            card.ListingPrice.HasValue && card.ListingPrice.Value > 0m;

        /// <summary>
        /// Computes the post-save status for a card.
        /// Listed and Sold are terminal states set elsewhere (export flow, sale recording)
        /// and are preserved unchanged. Otherwise: Ready when both images and price are
        /// present; Draft when either is missing.
        /// </summary>
        public static CardStatus Evaluate(Card card)
        {
            // Terminal states set by specific workflows; never overridden by image/price checks.
            if (card.Status is CardStatus.Listed or CardStatus.Sold or CardStatus.SoldInSet)
                return card.Status;

            // ReservedForSet is intentionally NOT preserved here: a card being removed from
            // a set should have its SurpriseSetId nulled before Evaluate is called, at which
            // point it re-evaluates to Draft/Ready based on image and price.

            return HasAnyImage(card) && HasPrice(card)
                ? CardStatus.Ready
                : CardStatus.Draft;
        }
    }
}
