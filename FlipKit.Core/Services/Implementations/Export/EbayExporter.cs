using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Services.Export
{
    /// <summary>
    /// Legacy eBay CSV field serializer — retained for reference only.
    /// eBay File Exchange (CSV bulk upload) is defunct. Direct listing creation
    /// is handled by <see cref="EbayPublishingService"/> via the Sell Inventory REST API.
    /// The <see cref="SerializeRow"/> method is preserved because it contains the
    /// authoritative Card→eBay field mapping; production code now goes through
    /// <see cref="EbayListingMapper"/>.
    /// </summary>
    public class EbayExporter
    {
        private const int MaxImageUrls = 24;
        private const int MaxTitleLength = 80;

        private readonly ShippingProfileNormalizer _shipping;

        public EbayExporter(ShippingProfileNormalizer shipping)
        {
            _shipping = shipping;
        }

        /// <summary>
        /// Builds the column-name → value map for one Card. Pure function; the dispatcher
        /// owns persistence and validation.
        /// </summary>
        public IDictionary<string, string> SerializeRow(
            Card card,
            Func<Card, string> titleFor,
            Func<Card, string> descriptionFor,
            EbayExportOptions options)
        {
            var row = new Dictionary<string, string>(StringComparer.Ordinal);

            row["CustomLabel"]  = card.Sku ?? string.Empty;
            row["*Category"]    = options.CategoryId;
            row["*Title"]       = Truncate(titleFor(card) ?? string.Empty, MaxTitleLength);
            row["*ConditionID"] = ConditionMapper.MapToEbayConditionId(card).ToString(CultureInfo.InvariantCulture);

            if (card.IsGraded)
            {
                if (!string.IsNullOrEmpty(card.GradeCompany))
                    row["CD:Professional Grader"] = MapGraderToEbayLabel(card.GradeCompany);
                if (!string.IsNullOrEmpty(card.GradeValue))
                    row["CD:Grade"] = card.GradeValue;
                if (!string.IsNullOrEmpty(card.CertNumber))
                    row["CDA:Certification Number"] = card.CertNumber;
            }
            else
            {
                row["CD:Card Condition"] = MapRawConditionToEbayLabel(card.Condition);
            }

            // C: (Item Specifics) — searchable structured attributes. Populate the ones
            // we know about; unknown columns stay blank, which eBay accepts.
            row["*C:Sport"]              = SportToString(card.Sport);
            row["C:Player/Athlete"]      = card.PlayerName ?? string.Empty;
            row["C:Year Manufactured"]   = card.Year?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            row["C:Manufacturer"]        = card.Manufacturer ?? string.Empty;
            row["C:Set"]                 = card.SetName ?? string.Empty;
            row["C:Card Number"]         = card.CardNumber ?? string.Empty;
            row["C:Team"]                = card.Team ?? string.Empty;
            row["C:League"]              = SportToLeague(card.Sport);
            row["C:Graded"]              = card.IsGraded ? "Yes" : "No";
            row["C:Autographed"]         = card.IsAuto ? "Yes" : "No";
            row["C:Parallel/Variety"]    = card.ParallelName ?? card.VariationType ?? string.Empty;
            row["C:Features"]            = BuildFeatures(card);
            if (card.IsGraded && !string.IsNullOrEmpty(card.GradeCompany))
                row["C:Professional Grader"] = MapGraderToEbayLabel(card.GradeCompany);
            if (card.IsGraded && !string.IsNullOrEmpty(card.GradeValue))
                row["C:Grade"] = card.GradeValue;

            // Images — pipe-delimited, spaces encoded as %20, capped at 24 (spec §3.8).
            var picUrls = EnumerateImageUrls(card)
                .Where(u => !string.IsNullOrEmpty(u))
                .Take(MaxImageUrls)
                .Select(u => u!.Replace(" ", "%20"));
            row["PicURL"]      = string.Join("|", picUrls);
            row["GalleryType"] = "Gallery";

            // Description — HTML allowed. Existing GenerateDescription returns plain text,
            // so the dispatcher wraps it as it sees fit.
            row["*Description"] = descriptionFor(card) ?? string.Empty;

            // Format / Duration / Price / Quantity — spec §3.4.
            row["*Format"]      = "FixedPrice";
            row["*Duration"]    = options.Duration ?? "GTC";
            row["*StartPrice"]  = (card.ListingPrice ?? 0m).ToString("F2", CultureInfo.InvariantCulture);
            row["*Quantity"]    = Math.Max(1, card.Quantity).ToString(CultureInfo.InvariantCulture);
            row["*Location"]   = options.SellerLocation ?? string.Empty;
            row["*DispatchTimeMax"] = options.DispatchTimeMax.ToString(CultureInfo.InvariantCulture);

            // Shipping — resolve from the (already-normalized) Whatnot bucket. Sets
            // ShippingType + ShippingService-1:Option + ShippingService-1:Cost together.
            var resolvedProfile = _shipping.NormalizeForWhatnot(card.ShippingProfile);
            var (svc, cost, type) = _shipping.ResolveEbayShipping(resolvedProfile);
            row["ShippingType"]               = type;
            row["ShippingService-1:Option"]   = svc;
            row["ShippingService-1:Cost"]     = cost.ToString("F2", CultureInfo.InvariantCulture);

            // Returns block — only valid on the ReturnsAccepted branch.
            row["*ReturnsAcceptedOption"] = options.ReturnsAccepted ? "ReturnsAccepted" : "ReturnsNotAccepted";
            if (options.ReturnsAccepted)
            {
                row["ReturnsWithinOption"]      = "Days_30";
                row["RefundOption"]             = "MoneyBack";
                row["ShippingCostPaidByOption"] = "Buyer";
            }

            return row;
        }

        // === helpers ===

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, max);

        private static string SportToString(Sport? sport) => sport switch
        {
            Sport.Baseball   => "Baseball",
            Sport.Basketball => "Basketball",
            Sport.Football   => "Football",
            Sport.Hockey     => "Ice Hockey",
            Sport.Soccer     => "Soccer",
            null             => string.Empty,
            _                => sport.ToString() ?? string.Empty,
        };

        private static string SportToLeague(Sport? sport) => sport switch
        {
            Sport.Baseball   => "MLB",
            Sport.Basketball => "NBA",
            Sport.Football   => "NFL",
            Sport.Hockey     => "NHL",
            _                => string.Empty,
        };

        private static string MapGraderToEbayLabel(string? grader)
        {
            if (string.IsNullOrEmpty(grader)) return string.Empty;
            // eBay's recommended labels for the Professional Grader item-specific.
            return grader.ToUpperInvariant() switch
            {
                "PSA"  => "Professional Sports Authenticator (PSA)",
                "BGS"  => "Beckett Grading Services (BGS)",
                "BVG"  => "Beckett Vintage Grading (BVG)",
                "BCCG" => "Beckett Collectors Club Grading (BCCG)",
                "CGC"  => "Certified Guaranty Company (CGC)",
                "SGC"  => "Sportscard Guaranty Corporation (SGC)",
                _      => grader,   // free-text custom companies pass through
            };
        }

        private static string MapRawConditionToEbayLabel(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var lower = raw.ToLowerInvariant();
            if (lower.Contains("near mint") || lower.Contains("mint")) return "Near mint or better";
            if (lower.Contains("excellent")) return "Excellent";
            if (lower.Contains("very good")) return "Very good";
            if (lower.Contains("poor") || lower.Contains("damaged")) return "Poor";
            return string.Empty;
        }

        private static string BuildFeatures(Card card)
        {
            var features = new List<string>();
            if (card.IsRookie) features.Add("Rookie");
            if (card.IsAuto)   features.Add("Autograph");
            if (card.IsRelic)  features.Add("Memorabilia");
            if (!string.IsNullOrEmpty(card.SerialNumbered)) features.Add("Serial Numbered");
            if (card.IsShortPrint) features.Add("Short Print");
            if (card.IsSSP)        features.Add("Super Short Print");
            return string.Join(";", features);
        }

        private static IEnumerable<string?> EnumerateImageUrls(Card card)
        {
            yield return card.ImageUrl1;
            yield return card.ImageUrl2;
            yield return card.ImageUrl3;
            yield return card.ImageUrl4;
            yield return card.ImageUrl5;
            yield return card.ImageUrl6;
            yield return card.ImageUrl7;
            yield return card.ImageUrl8;
        }
    }

    public class EbayExportOptions
    {
        /// <summary>eBay leaf category ID. Defaults to Sports Trading Cards (261328).</summary>
        public string CategoryId { get; set; } = "261328";

        /// <summary>Listing duration. <c>"GTC"</c> = Good Til Cancelled (FixedPrice only).</summary>
        public string Duration { get; set; } = "GTC";

        /// <summary>Seller location (zip code or city, state) for *Location. Required.</summary>
        public string? SellerLocation { get; set; }

        /// <summary>Handling time in days. eBay default is 1; we conservatively use 2.</summary>
        public int DispatchTimeMax { get; set; } = 2;

        /// <summary>If true, fills out ReturnsWithinOption=Days_30 / MoneyBack / Buyer.</summary>
        public bool ReturnsAccepted { get; set; } = true;

        /// <summary>If true, the Action column gets "VerifyAdd" instead of "Add" — eBay
        /// validates the listing without creating it. Useful for dry-run testing.</summary>
        public bool UseVerifyAdd { get; set; }
    }
}
