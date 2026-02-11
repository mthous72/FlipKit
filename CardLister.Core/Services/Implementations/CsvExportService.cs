using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using CsvHelper;
using CsvHelper.Configuration;

namespace FlipKit.Core.Services
{
    public class CsvExportService : IExportService
    {
        private readonly ISettingsService _settingsService;
        private readonly TitleTemplateService _titleTemplateService;

        public CsvExportService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _titleTemplateService = new TitleTemplateService();
        }

        public string GenerateTitle(Card card)
        {
            // Use the active export platform's template
            var settings = _settingsService.Load();
            var template = GetTemplateForPlatform(settings.ActiveExportPlatform, settings);

            return _titleTemplateService.GenerateTitle(card, template);
        }

        /// <summary>
        /// Generate title for a specific platform (overload for flexibility)
        /// </summary>
        public string GenerateTitle(Card card, ExportPlatform platform)
        {
            var settings = _settingsService.Load();
            var template = GetTemplateForPlatform(platform, settings);

            return _titleTemplateService.GenerateTitle(card, template);
        }

        private string GetTemplateForPlatform(ExportPlatform platform, AppSettings settings)
        {
            return platform switch
            {
                ExportPlatform.Whatnot => settings.WhatnotTitleTemplate,
                ExportPlatform.eBay => settings.EbayTitleTemplate,
                ExportPlatform.COMC => settings.ComcTitleTemplate,
                ExportPlatform.Generic or _ => settings.GenericTitleTemplate
            };
        }

        public string GenerateDescription(Card card)
        {
            var sb = new StringBuilder();

            sb.AppendLine(GenerateTitle(card));

            if (!string.IsNullOrEmpty(card.VariationType) && card.VariationType != "Base")
                sb.AppendLine($"{card.ParallelName} {card.VariationType}");

            if (!string.IsNullOrEmpty(card.CardNumber))
                sb.AppendLine($"Card #{card.CardNumber}");

            sb.AppendLine();

            if (!string.IsNullOrEmpty(card.Team))
                sb.AppendLine($"Team: {card.Team}");

            sb.AppendLine($"Condition: {card.Condition}");

            if (card.IsGraded)
            {
                if (!string.IsNullOrEmpty(card.GradeCompany) && !string.IsNullOrEmpty(card.GradeValue))
                    sb.AppendLine($"Grade: {card.GradeCompany} {card.GradeValue}");
                if (!string.IsNullOrEmpty(card.AutoGrade))
                    sb.AppendLine($"Auto Grade: {card.AutoGrade}");
                if (!string.IsNullOrEmpty(card.CertNumber))
                    sb.AppendLine($"Cert #: {card.CertNumber}");
            }

            if (!string.IsNullOrEmpty(card.SerialNumbered))
                sb.AppendLine($"Serial: {card.SerialNumbered}");

            if (card.IsRookie) sb.AppendLine("Rookie Card!");
            if (card.IsAuto) sb.AppendLine("Autograph!");
            if (card.IsRelic) sb.AppendLine("Memorabilia Relic!");

            sb.AppendLine();
            sb.AppendLine("Ships within 2 business days in penny sleeve + top loader + bubble mailer.");

            return sb.ToString().Trim();
        }

        public List<string> ValidateCardForExport(Card card)
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(card.PlayerName))
                errors.Add("Missing player name");
            if (!card.ListingPrice.HasValue || card.ListingPrice <= 0)
                errors.Add("Missing listing price");
            if (string.IsNullOrEmpty(card.WhatnotSubcategory))
                errors.Add("Missing subcategory");

            return errors;
        }

        public async Task ExportCsvAsync(List<Card> cards, string outputPath)
        {
            // Use active export platform from settings (default behavior)
            var settings = _settingsService.Load();
            await ExportCsvAsync(cards, outputPath, settings.ActiveExportPlatform);
        }

        public async Task ExportCsvAsync(List<Card> cards, string outputPath, ExportPlatform platform)
        {
            await using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));
            await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            });

            // Write header
            csv.WriteField("Category");
            csv.WriteField("Sub Category");
            csv.WriteField("Title");
            csv.WriteField("Description");
            csv.WriteField("Quantity");
            csv.WriteField("Type");
            csv.WriteField("Price");
            csv.WriteField("Shipping Profile");
            csv.WriteField("Offerable");
            csv.WriteField("Condition");
            for (int i = 1; i <= 8; i++)
                csv.WriteField($"Image URL {i}");
            await csv.NextRecordAsync();

            // Write rows
            foreach (var card in cards)
            {
                csv.WriteField("Sports Cards");
                csv.WriteField(card.WhatnotSubcategory ?? GetSubcategoryFromSport(card));
                csv.WriteField(GenerateTitle(card, platform));
                csv.WriteField(GenerateDescription(card));
                csv.WriteField(card.Quantity);
                csv.WriteField(card.ListingType);
                csv.WriteField(card.ListingPrice?.ToString("F2") ?? "0.00");
                csv.WriteField(card.ShippingProfile);
                csv.WriteField(card.Offerable ? "TRUE" : "FALSE");
                csv.WriteField(card.Condition);
                csv.WriteField(card.ImageUrl1 ?? "");
                csv.WriteField(card.ImageUrl2 ?? "");
                for (int i = 3; i <= 8; i++)
                    csv.WriteField("");
                await csv.NextRecordAsync();
            }
        }

        public async Task ExportTaxCsvAsync(List<Card> soldCards, string outputPath)
        {
            await using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));
            await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            });

            csv.WriteField("Sale Date");
            csv.WriteField("Item Description");
            csv.WriteField("Cost Basis");
            csv.WriteField("Sale Price");
            csv.WriteField("Platform");
            csv.WriteField("Fees");
            csv.WriteField("Shipping");
            csv.WriteField("Net Profit");
            await csv.NextRecordAsync();

            foreach (var card in soldCards.OrderBy(c => c.SaleDate))
            {
                csv.WriteField(card.SaleDate?.ToString("yyyy-MM-dd") ?? "");
                csv.WriteField(GenerateTitle(card));
                csv.WriteField(card.CostBasis?.ToString("F2") ?? "0.00");
                csv.WriteField(card.SalePrice?.ToString("F2") ?? "0.00");
                csv.WriteField(card.SalePlatform ?? "Whatnot");
                csv.WriteField(card.FeesPaid?.ToString("F2") ?? "0.00");
                csv.WriteField(card.ShippingCost?.ToString("F2") ?? "0.00");
                csv.WriteField(card.NetProfit?.ToString("F2") ?? "0.00");
                await csv.NextRecordAsync();
            }
        }

        private static string GetSubcategoryFromSport(Card card)
        {
            return card.Sport switch
            {
                Models.Enums.Sport.Football => "Football Cards",
                Models.Enums.Sport.Baseball => "Baseball Cards",
                Models.Enums.Sport.Basketball => "Basketball Cards",
                _ => "Other Sports Cards"
            };
        }
    }
}
