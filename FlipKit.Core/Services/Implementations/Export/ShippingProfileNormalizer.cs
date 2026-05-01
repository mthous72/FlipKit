using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FlipKit.Core.Services.Export
{
    /// <summary>
    /// Translates the legacy free-form <c>Card.ShippingProfile</c> string (e.g. "4 oz",
    /// "2 lbs", "PWE") into:
    ///   • A spec-valid Whatnot bucket name (must match whatnot_values.json → shipping_profiles).
    ///   • A resolved eBay shipping service + flat cost (USPSGroundAdvantage / USPSFirstClass /
    ///     Calculated, paired with a USD amount).
    ///
    /// If the string already matches a Whatnot bucket exactly, it's passed through unchanged
    /// — the user may have a custom shipping profile saved on Whatnot that we don't need to
    /// reinterpret.
    /// </summary>
    public class ShippingProfileNormalizer
    {
        private readonly WhatnotValuesProvider _whatnot;

        public ShippingProfileNormalizer(WhatnotValuesProvider whatnot)
        {
            _whatnot = whatnot;
        }

        // Legacy-string → Whatnot-bucket fallback chain. Only fires when the input doesn't
        // already match a real bucket. Keep ordered most-specific-first.
        private static readonly Regex LbsPattern  = new(@"^\s*(\d+(?:\.\d+)?)\s*lbs?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex OzPattern   = new(@"^\s*(\d+(?:\.\d+)?)\s*oz\s*$",   RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex GramPattern = new(@"^\s*(\d+(?:\.\d+)?)\s*g(rams)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Returns a valid Whatnot shipping profile string for the given input, or the input
        /// unchanged if it already matches a known bucket or a presumed custom profile.
        /// Never returns null (worst case the original string is passed through).
        /// </summary>
        public string NormalizeForWhatnot(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "0-1 oz"; // smallest possible bucket as a safe default for cards

            var trimmed = input.Trim();

            // Already a valid bucket or a known custom profile — pass through.
            if (_whatnot.IsValidShippingProfile(trimmed))
                return trimmed;

            // Try ounces.
            var oz = OzPattern.Match(trimmed);
            if (oz.Success && double.TryParse(oz.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ozVal))
                return BucketByOunces(ozVal);

            // Try pounds.
            var lb = LbsPattern.Match(trimmed);
            if (lb.Success && double.TryParse(lb.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lbVal))
                return BucketByPounds(lbVal);

            // Try grams.
            var gm = GramPattern.Match(trimmed);
            if (gm.Success && double.TryParse(gm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var gmVal))
                return BucketByOunces(gmVal / 28.3495); // grams → oz

            // Unknown shape — caller (validator) will warn; pass through and hope it matches a custom profile.
            return trimmed;
        }

        /// <summary>
        /// Resolves an eBay flat-rate shipping service + cost for the given Whatnot-style
        /// shipping profile or weight string. Returns:
        ///   <c>service</c>: the value for <c>ShippingService-1:Option</c> (e.g. "USPSGroundAdvantage").
        ///   <c>cost</c>:    the value for <c>ShippingService-1:Cost</c> (USD).
        ///   <c>shippingType</c>: "Flat" or "Calculated".
        /// Heuristic — falls back to USPSGroundAdvantage for anything ≥ 4 oz, USPSFirstClass under that.
        /// Card sellers can override the cost via settings later.
        /// </summary>
        public (string Service, decimal Cost, string ShippingType) ResolveEbayShipping(string? whatnotProfile)
        {
            // Estimate weight in ounces from the profile name. Anything we can't classify
            // falls through to Calculated, which makes eBay use the buyer's zip + the seller's
            // package dimensions (we don't track those).
            var ounces = ApproximateOuncesFromProfile(whatnotProfile);

            if (ounces is null)
                return ("USPSGroundAdvantage", 4.50m, "Calculated");

            // Flat-rate cost ladder. These are conservative defaults — sellers should review.
            // The spec leaves cost as a per-listing decision; we just need to emit a value.
            decimal cost;
            string service;
            if (ounces <= 3)        { service = "USPSFirstClass";       cost = 1.00m; }
            else if (ounces <= 7)   { service = "USPSFirstClass";       cost = 4.50m; }
            else if (ounces <= 15)  { service = "USPSGroundAdvantage";  cost = 5.50m; }
            else if (ounces <= 16)  { service = "USPSGroundAdvantage";  cost = 7.00m; }   // 1 lb
            else if (ounces <= 32)  { service = "USPSGroundAdvantage";  cost = 9.00m; }   // 1-2 lbs
            else if (ounces <= 48)  { service = "USPSGroundAdvantage";  cost = 12.00m; }  // 2-3 lbs
            else                    { service = "USPSGroundAdvantage";  cost = 15.00m; }  // > 3 lbs

            return (service, cost, "Flat");
        }

        // === bucket pickers ===

        private static string BucketByOunces(double oz)
        {
            if (oz <= 1)   return "0-1 oz";
            if (oz <= 3)   return "1-3 oz";
            if (oz <= 7)   return "4-7 oz";
            if (oz <= 11)  return "8-11 oz";
            if (oz <= 15)  return "12-15 oz";
            if (oz <= 16)  return "1 lb";
            return BucketByPounds(oz / 16.0);
        }

        private static string BucketByPounds(double lb)
        {
            if (lb <= 1)   return "1 lb";
            if (lb <= 2)   return "1-2 lbs";
            if (lb <= 3)   return "2-3 lbs";
            if (lb <= 4)   return "3-4 lbs";
            if (lb <= 6)   return "4-6 lbs";
            return "10-14 lbs";
        }

        // Approximates the upper-bound ounces a Whatnot profile name implies. Returns null
        // if the profile is non-weight (e.g. a custom seller profile).
        private static double? ApproximateOuncesFromProfile(string? profile)
        {
            if (string.IsNullOrEmpty(profile)) return null;

            // Sport singles bucket — most cards
            if (profile.Equals("Sports singles (3oz)", StringComparison.OrdinalIgnoreCase)) return 3;

            // oz buckets
            if (profile == "0-1 oz")    return 1;
            if (profile == "1-3 oz")    return 3;
            if (profile == "4-7 oz")    return 7;
            if (profile == "8-11 oz")   return 11;
            if (profile == "12-15 oz")  return 15;

            // lb buckets
            if (profile == "1 lb")      return 16;
            if (profile == "1-2 lbs")   return 32;
            if (profile == "2-3 lbs")   return 48;
            if (profile == "3-4 lbs")   return 64;
            if (profile == "4-6 lbs")   return 96;
            if (profile == "10-14 lbs") return 224;

            // Gram / KG buckets — convert upper bound
            var kgMatch = Regex.Match(profile, @"<\s*(\d+(?:\.\d+)?)\s*(KGs?|grams?)", RegexOptions.IgnoreCase);
            if (kgMatch.Success && double.TryParse(kgMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
            {
                var unit = kgMatch.Groups[2].Value.ToLowerInvariant();
                var grams = unit.StartsWith("k") ? num * 1000 : num;
                return grams / 28.3495;
            }

            return null;
        }
    }
}
