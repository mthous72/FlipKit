using System;
using System.Collections.Generic;
using CardLister.Helpers;
using CardLister.Models;

namespace CardLister.Services
{
    public class PricerService : IPricerService
    {
        public string BuildTerapeakUrl(Card card)
        {
            var parts = new List<string>();

            // Core identification
            if (card.Year.HasValue) parts.Add(card.Year.Value.ToString());
            if (!string.IsNullOrEmpty(card.Manufacturer)) parts.Add(card.Manufacturer);
            if (!string.IsNullOrEmpty(card.Brand)) parts.Add(card.Brand);
            if (!string.IsNullOrEmpty(card.PlayerName)) parts.Add(card.PlayerName);
            if (!string.IsNullOrEmpty(card.CardNumber)) parts.Add($"#{card.CardNumber}");

            // Variation details
            if (!string.IsNullOrEmpty(card.ParallelName)) parts.Add(card.ParallelName);
            if (!string.IsNullOrEmpty(card.Team)) parts.Add(card.Team);

            // Grading information (CRITICAL for accurate pricing)
            if (card.IsGraded)
            {
                if (!string.IsNullOrEmpty(card.GradeCompany)) parts.Add(card.GradeCompany);
                if (!string.IsNullOrEmpty(card.GradeValue)) parts.Add(card.GradeValue);
            }

            var query = Uri.EscapeDataString(string.Join(" ", parts));
            return $"https://www.ebay.com/sh/research?marketplace=EBAY-US&keywords={query}&tabName=SOLD";
        }

        public string BuildEbaySoldUrl(Card card)
        {
            var parts = new List<string>();

            // Core identification
            if (card.Year.HasValue) parts.Add(card.Year.Value.ToString());
            if (!string.IsNullOrEmpty(card.Manufacturer)) parts.Add(card.Manufacturer);
            if (!string.IsNullOrEmpty(card.Brand)) parts.Add(card.Brand);
            if (!string.IsNullOrEmpty(card.PlayerName)) parts.Add(card.PlayerName);
            if (!string.IsNullOrEmpty(card.CardNumber)) parts.Add($"#{card.CardNumber}");

            // Variation details
            if (!string.IsNullOrEmpty(card.ParallelName)) parts.Add(card.ParallelName);
            if (!string.IsNullOrEmpty(card.Team)) parts.Add(card.Team);

            // Grading information (CRITICAL for accurate pricing)
            if (card.IsGraded)
            {
                if (!string.IsNullOrEmpty(card.GradeCompany)) parts.Add(card.GradeCompany);
                if (!string.IsNullOrEmpty(card.GradeValue)) parts.Add(card.GradeValue);
            }

            var query = Uri.EscapeDataString(string.Join(" ", parts));
            return $"https://www.ebay.com/sch/i.html?_nkw={query}&_sacat=261328&LH_Sold=1&LH_Complete=1";
        }

        public decimal SuggestPrice(decimal estimatedValue, Card card)
        {
            var price = estimatedValue;

            var variation = (card.VariationType ?? "Base").ToLower();

            if (variation == "base")
            {
                price *= 0.80m;
            }
            else if (!string.IsNullOrEmpty(card.SerialNumbered))
            {
                var serial = card.SerialNumbered.Replace("/", "");
                if (int.TryParse(serial, out var num))
                {
                    price *= num <= 10 ? 0.95m : num <= 25 ? 0.92m : 0.88m;
                }
                else
                {
                    price *= 0.88m;
                }
            }
            else
            {
                price *= 0.85m;
            }

            // Boost for special attributes
            if (card.IsRookie) price *= 1.05m;
            if (card.IsAuto) price *= 1.02m;

            // Round to nice price points
            if (price >= 100)
                price = Math.Round(price / 5) * 5;
            else if (price >= 20)
                price = Math.Round(price);
            else if (price >= 5)
                price = Math.Round(price * 2) / 2;
            else
                price = Math.Round(price, 2);

            return Math.Max(price, 0.99m);
        }

        public decimal CalculateNet(decimal salePrice, decimal feePercent = 11m)
        {
            return PriceCalculator.CalculateNet(salePrice, feePercent);
        }
    }
}
