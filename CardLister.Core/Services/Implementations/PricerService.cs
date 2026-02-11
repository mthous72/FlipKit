using System;
using System.Collections.Generic;
using FlipKit.Core.Helpers;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    public class PricerService : IPricerService
    {
        private readonly ISettingsService _settingsService;
        private readonly TitleTemplateService _titleTemplateService;

        public PricerService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _titleTemplateService = new TitleTemplateService();
        }

        public string BuildTerapeakUrl(Card card)
        {
            // Use customizable search template for Terapeak
            var settings = _settingsService.Load();
            var searchQuery = _titleTemplateService.GenerateTitle(card, settings.TerapeakSearchTemplate);

            var query = Uri.EscapeDataString(searchQuery);
            return $"https://www.ebay.com/sh/research?marketplace=EBAY-US&keywords={query}&tabName=SOLD";
        }

        public string BuildEbaySoldUrl(Card card)
        {
            // Use customizable search template for eBay
            var settings = _settingsService.Load();
            var searchQuery = _titleTemplateService.GenerateTitle(card, settings.EbaySearchTemplate);

            var query = Uri.EscapeDataString(searchQuery);
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
