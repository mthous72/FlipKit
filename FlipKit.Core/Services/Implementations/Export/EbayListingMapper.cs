using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Services.Export
{
    /// <summary>
    /// Maps a <see cref="Card"/> to eBay Sell Inventory API request objects.
    /// Extracted from the legacy EbayExporter.SerializeRow() CSV pipeline.
    /// </summary>
    public static class EbayListingMapper
    {
        private const int MaxImageUrls = 24;
        private const int MaxTitleLength = 80;

        public static EbayInventoryItemRequest BuildInventoryItemRequest(Card card, string title, string description)
        {
            var imageUrls = EnumerateImageUrls(card)
                .Where(u => !string.IsNullOrEmpty(u))
                .Take(MaxImageUrls)
                .Select(u => u!)
                .ToList();

            var aspects = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            SetAspect(aspects, "Sport", SportToString(card.Sport));
            SetAspect(aspects, "Player/Athlete", card.PlayerName);
            SetAspect(aspects, "Year Manufactured", card.Year?.ToString(CultureInfo.InvariantCulture));
            SetAspect(aspects, "Manufacturer", card.Manufacturer);
            SetAspect(aspects, "Set", card.SetName);
            SetAspect(aspects, "Card Number", card.CardNumber);
            SetAspect(aspects, "Team", card.Team);
            SetAspect(aspects, "League", SportToLeague(card.Sport));
            SetAspect(aspects, "Graded", card.IsGraded ? "Yes" : "No");
            SetAspect(aspects, "Autographed", card.IsAuto ? "Yes" : "No");
            SetAspect(aspects, "Parallel/Variety", card.ParallelName ?? card.VariationType);

            var features = BuildFeatures(card);
            if (!string.IsNullOrEmpty(features))
                SetAspect(aspects, "Features", features);

            if (card.IsGraded && !string.IsNullOrEmpty(card.GradeCompany))
                SetAspect(aspects, "Professional Grader", MapGraderToEbayLabel(card.GradeCompany));
            if (card.IsGraded && !string.IsNullOrEmpty(card.GradeValue))
                SetAspect(aspects, "Grade", card.GradeValue);

            return new EbayInventoryItemRequest
            {
                Availability = new EbayShipToLocationAvailability
                {
                    Quantity = Math.Max(1, card.Quantity)
                },
                Condition = MapToInventoryApiCondition(card),
                Product = new EbayInventoryProduct
                {
                    Title = Truncate(title, MaxTitleLength),
                    Description = description,
                    ImageUrls = imageUrls,
                    Aspects = aspects
                }
            };
        }

        public static EbayOfferRequest BuildOfferRequest(Card card, string description, AppSettings settings)
        {
            return new EbayOfferRequest
            {
                Sku = card.Sku ?? string.Empty,
                MarketplaceId = "EBAY_US",
                Format = "FIXED_PRICE",
                ListingDescription = description,
                AvailableQuantity = Math.Max(1, card.Quantity),
                CategoryId = "261328",
                ListingPolicies = new EbayListingPolicies
                {
                    FulfillmentPolicyId = settings.EbayFulfillmentPolicyId ?? string.Empty,
                    PaymentPolicyId = settings.EbayPaymentPolicyId ?? string.Empty,
                    ReturnPolicyId = settings.EbayReturnPolicyId ?? string.Empty
                },
                PricingSummary = new EbayPricingSummary
                {
                    Price = new EbayAmount
                    {
                        Value = (card.ListingPrice ?? 0m).ToString("F2", CultureInfo.InvariantCulture),
                        Currency = "USD"
                    }
                }
            };
        }

        // === condition mapping ===

        private static string MapToInventoryApiCondition(Card card)
        {
            if (card.IsGraded)
            {
                if (double.TryParse(card.GradeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var grade))
                {
                    if (grade >= 10) return "NEW";
                    if (grade >= 9)  return "LIKE_NEW";
                    if (grade >= 7)  return "USED_EXCELLENT";
                    if (grade >= 5)  return "USED_VERY_GOOD";
                    return "USED_GOOD";
                }
                return "USED_EXCELLENT";
            }

            var raw = (card.Condition ?? string.Empty).ToLowerInvariant();
            if (ContainsAny(raw, "for parts", "not working", "damaged")) return "FOR_PARTS_OR_NOT_WORKING";
            if (ContainsAny(raw, "brand new", "sealed", "factory", "nwt")) return "NEW";
            if (ContainsAny(raw, "near mint", "mint")) return "USED_EXCELLENT";
            if (ContainsAny(raw, "excellent", "very good")) return "USED_VERY_GOOD";
            if (ContainsAny(raw, "good")) return "USED_GOOD";
            if (ContainsAny(raw, "poor", "acceptable")) return "USED_ACCEPTABLE";
            return "USED_EXCELLENT";
        }

        // === field helpers ===

        private static void SetAspect(Dictionary<string, List<string>> aspects, string key, string? value)
        {
            if (!string.IsNullOrEmpty(value))
                aspects[key] = new List<string> { value };
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, max);

        public static string SportToString(Sport? sport) => sport switch
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

        public static string MapGraderToEbayLabel(string? grader)
        {
            if (string.IsNullOrEmpty(grader)) return string.Empty;
            return grader.ToUpperInvariant() switch
            {
                "PSA"  => "Professional Sports Authenticator (PSA)",
                "BGS"  => "Beckett Grading Services (BGS)",
                "BVG"  => "Beckett Vintage Grading (BVG)",
                "BCCG" => "Beckett Collectors Club Grading (BCCG)",
                "CGC"  => "Certified Guaranty Company (CGC)",
                "SGC"  => "Sportscard Guaranty Corporation (SGC)",
                _      => grader,
            };
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

        private static bool ContainsAny(string s, params string[] needles)
        {
            foreach (var n in needles)
                if (s.Contains(n)) return true;
            return false;
        }
    }

    // === eBay Inventory API request POCOs ===

    public class EbayInventoryItemRequest
    {
        public EbayShipToLocationAvailability Availability { get; set; } = new();
        public string Condition { get; set; } = "USED_EXCELLENT";
        public EbayInventoryProduct Product { get; set; } = new();
    }

    public class EbayShipToLocationAvailability
    {
        public int Quantity { get; set; } = 1;
    }

    public class EbayInventoryProduct
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = new();
        public Dictionary<string, List<string>> Aspects { get; set; } = new();
    }

    public class EbayOfferRequest
    {
        public string Sku { get; set; } = string.Empty;
        public string MarketplaceId { get; set; } = "EBAY_US";
        public string Format { get; set; } = "FIXED_PRICE";
        public string ListingDescription { get; set; } = string.Empty;
        public int AvailableQuantity { get; set; } = 1;
        public string CategoryId { get; set; } = "261328";
        public EbayListingPolicies ListingPolicies { get; set; } = new();
        public EbayPricingSummary PricingSummary { get; set; } = new();
    }

    public class EbayListingPolicies
    {
        public string FulfillmentPolicyId { get; set; } = string.Empty;
        public string PaymentPolicyId { get; set; } = string.Empty;
        public string ReturnPolicyId { get; set; } = string.Empty;
    }

    public class EbayPricingSummary
    {
        public EbayAmount Price { get; set; } = new();
    }

    public class EbayAmount
    {
        public string Value { get; set; } = "0.00";
        public string Currency { get; set; } = "USD";
    }
}
