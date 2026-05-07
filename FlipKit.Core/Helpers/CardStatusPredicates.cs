using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Helpers
{
    /// <summary>
    /// Single source of truth for status-based card filtering. Centralises the logic
    /// that was previously duplicated across ExportViewModel, InventoryViewModel,
    /// CardRepository, ReportsController, and PricingController.
    /// </summary>
    public static class CardStatusPredicates
    {
        /// <summary>
        /// Statuses that appear in the individual listing export grid and are eligible
        /// for Whatnot / eBay CSV export. Excludes cards in sets or already sold.
        /// </summary>
        public static readonly CardStatus[] IndividualListingStatuses =
            [CardStatus.Draft, CardStatus.Priced, CardStatus.Ready, CardStatus.Listed];

        /// <summary>
        /// Statuses that count as sold for revenue report totals. Includes both
        /// individual sales and sales made as part of a Surprise Set.
        /// </summary>
        public static readonly CardStatus[] SoldStatuses =
            [CardStatus.Sold, CardStatus.SoldInSet];

        /// <summary>
        /// Statuses that represent active, priceable inventory (not in a set, not sold,
        /// not a draft that's already been exported). Used for stale-price detection.
        /// </summary>
        public static readonly CardStatus[] ActiveInventoryStatuses =
            [CardStatus.Priced, CardStatus.Ready, CardStatus.Listed];

        public static bool IsAvailableForIndividualListing(Card card) =>
            card.Status is CardStatus.Draft or CardStatus.Priced or CardStatus.Ready or CardStatus.Listed;

        public static bool IsSold(Card card) =>
            card.Status is CardStatus.Sold or CardStatus.SoldInSet;

        public static bool IsActiveInventory(Card card) =>
            card.Status is CardStatus.Priced or CardStatus.Ready or CardStatus.Listed;
    }
}
