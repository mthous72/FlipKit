using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services.Export
{
    /// <summary>
    /// Serializes <see cref="Card"/>s into the 21-column Whatnot bulk-import CSV format.
    /// Spec reference: card_listings_export_spec.md §2 (column structure + rules) and §4
    /// (Card → Whatnot field mapping).
    /// </summary>
    public class WhatnotExporter
    {
        public static readonly string[] Columns = new[]
        {
            "Category", "Sub Category", "Title", "Description", "Quantity", "Type", "Price",
            "Shipping Profile", "Offerable", "Hazmat", "Condition", "Cost Per Item", "SKU",
            "Image URL 1", "Image URL 2", "Image URL 3", "Image URL 4",
            "Image URL 5", "Image URL 6", "Image URL 7", "Image URL 8",
        };

        private static readonly HashSet<string> ValidTypes = new(StringComparer.Ordinal)
        {
            "Auction", "Buy it Now", "Giveaway",
        };

        private readonly WhatnotValuesProvider _whatnot;
        private readonly ShippingProfileNormalizer _shipping;

        public WhatnotExporter(WhatnotValuesProvider whatnot, ShippingProfileNormalizer shipping)
        {
            _whatnot = whatnot;
            _shipping = shipping;
        }

        /// <summary>
        /// Builds the Whatnot row dictionary for a single card. Pure function — does not
        /// touch the database or filesystem. The dispatcher is responsible for assigning
        /// SKUs and persisting them before calling this; if <c>card.Sku</c> is blank the
        /// SKU column will be blank.
        /// </summary>
        public IDictionary<string, string> SerializeRow(
            Card card,
            Func<Card, string> titleFor,
            Func<Card, string> descriptionFor,
            WhatnotExportOptions options)
        {
            var listingType = NormalizeListingType(options.DefaultListingType ?? card.ListingType);

            var category = card.WhatnotCategory ?? string.Empty;
            var subcategory = card.WhatnotSubcategory ?? string.Empty;

            // Condition — sub-category lookup → category fallback → blank.
            var allowedConds = _whatnot.ConditionsFor(category, subcategory);
            var condition = ConditionMapper.MapToWhatnot(card, allowedConds) ?? string.Empty;

            // Shipping — pass-through valid buckets, normalize legacy weight strings.
            var shippingProfile = _shipping.NormalizeForWhatnot(card.ShippingProfile);

            // Price — positive integer, no decimals (spec §2.4 #2).
            var rawPrice = card.ListingPrice ?? 0m;
            var price = Math.Max(1, (int)Math.Round(rawPrice, MidpointRounding.AwayFromZero));

            // Title — hard cap 80 chars (spec §2.4 #7).
            var title = Truncate(titleFor(card) ?? string.Empty, 80);

            // Offerable — only meaningful for Buy it Now; blank otherwise (spec §2.3).
            var offerable = listingType == "Buy it Now"
                ? (card.Offerable ? "TRUE" : "FALSE")
                : string.Empty;

            var row = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Category"]         = category,
                ["Sub Category"]     = subcategory,
                ["Title"]            = title,
                ["Description"]      = descriptionFor(card) ?? string.Empty,
                ["Quantity"]         = Math.Max(1, card.Quantity).ToString(CultureInfo.InvariantCulture),
                ["Type"]             = listingType,
                ["Price"]            = price.ToString(CultureInfo.InvariantCulture),
                ["Shipping Profile"] = shippingProfile,
                ["Offerable"]        = offerable,
                ["Hazmat"]           = "Not Hazmat",
                ["Condition"]        = condition,
                ["Cost Per Item"]    = card.CostBasis?.ToString("F2", CultureInfo.InvariantCulture) ?? string.Empty,
                ["SKU"]              = card.Sku ?? string.Empty,
            };
            for (int i = 1; i <= 8; i++)
                row["Image URL " + i] = GetImageUrlSlot(card, i) ?? string.Empty;

            return row;
        }

        /// <summary>
        /// Writes the 21-column Whatnot CSV (UTF-8 no-BOM) to <paramref name="outputPath"/>.
        /// Returns the number of rows written.
        /// </summary>
        public async Task<int> WriteAsync(
            IList<Card> cards,
            string outputPath,
            Func<Card, string> titleFor,
            Func<Card, string> descriptionFor,
            WhatnotExportOptions options)
        {
            await using var writer = new StreamWriter(outputPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            });

            foreach (var col in Columns)
                csv.WriteField(col);
            await csv.NextRecordAsync();

            int written = 0;
            foreach (var card in cards)
            {
                var row = SerializeRow(card, titleFor, descriptionFor, options);
                foreach (var col in Columns)
                    csv.WriteField(row.TryGetValue(col, out var v) ? v : string.Empty);
                await csv.NextRecordAsync();
                written++;
            }

            return written;
        }

        // === helpers ===

        private static string NormalizeListingType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type)) return "Buy it Now";
            // Whatnot's enum uses lowercase 'it'; the Card model historically defaults to
            // "Buy It Now" (capital I), so we normalize here. Spec §2.4 #1.
            if (string.Equals(type, "Buy It Now", StringComparison.OrdinalIgnoreCase))
                return "Buy it Now";
            if (ValidTypes.Contains(type)) return type;
            // Last-resort fallback — anything unrecognized becomes Buy it Now to keep the
            // export from being rejected outright. Validator should have caught this earlier.
            return "Buy it Now";
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, max);

        private static string? GetImageUrlSlot(Card card, int slot) => slot switch
        {
            1 => card.ImageUrl1,
            2 => card.ImageUrl2,
            3 => card.ImageUrl3,
            4 => card.ImageUrl4,
            5 => card.ImageUrl5,
            6 => card.ImageUrl6,
            7 => card.ImageUrl7,
            8 => card.ImageUrl8,
            _ => null,
        };
    }

    public class WhatnotExportOptions
    {
        /// <summary>
        /// Per-export listing type. Falls back to <see cref="Card.ListingType"/> if not set.
        /// Must be one of: "Auction", "Buy it Now", "Giveaway" (lowercase 'it' is intentional).
        /// </summary>
        public string? DefaultListingType { get; set; }
    }
}
