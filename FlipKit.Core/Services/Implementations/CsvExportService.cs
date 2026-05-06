using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services.Export;
using CsvHelper;
using CsvHelper.Configuration;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// CSV export dispatcher. Validates the input cards via <see cref="ExportValidator"/>
    /// then routes to the platform-specific exporter (Whatnot/COMC/Generic).
    /// eBay listing creation is handled by <see cref="EbayPublishingService"/> instead.
    /// </summary>
    public class CsvExportService : IExportService
    {
        private readonly ISettingsService _settingsService;
        private readonly TitleTemplateService _titleTemplateService;
        private readonly WhatnotExporter _whatnotExporter;
        private readonly ExportValidator _validator;

        public CsvExportService(
            ISettingsService settingsService,
            WhatnotExporter whatnotExporter,
            ExportValidator validator)
        {
            _settingsService = settingsService;
            _whatnotExporter = whatnotExporter;
            _validator = validator;
            _titleTemplateService = new TitleTemplateService();
        }

        public string GenerateTitle(Card card)
        {
            var settings = _settingsService.Load();
            var template = GetTemplateForPlatform(settings.ActiveExportPlatform, settings);
            return _titleTemplateService.GenerateTitle(card, template);
        }

        /// <summary>
        /// Generate title for a specific platform (overload for flexibility).
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

        /// <summary>
        /// Per-card pre-flight validator (legacy entry point — returns human-readable
        /// strings for the existing ViewModel). Delegates to <see cref="ExportValidator"/>
        /// so the rules stay in one place. Validates against the active export platform
        /// from settings.
        /// </summary>
        public List<string> ValidateCardForExport(Card card)
        {
            var settings = _settingsService.Load();
            var errors = ValidateBatch(new List<Card> { card }, settings.ActiveExportPlatform);
            return errors
                .Where(e => e.Severity == ExportErrorSeverity.Error)
                .Select(e => e.Message)
                .ToList();
        }

        public IReadOnlyList<ExportRowError> ValidateBatch(IList<Card> cards, ExportPlatform platform)
        {
            return _validator.ValidateForWhatnot(cards);
        }

        public async Task ExportCsvAsync(List<Card> cards, string outputPath)
        {
            var settings = _settingsService.Load();
            await ExportCsvAsync(cards, outputPath, settings.ActiveExportPlatform);
        }

        public async Task ExportCsvAsync(List<Card> cards, string outputPath, ExportPlatform platform)
        {
            var errors = _validator.ValidateForWhatnot(cards);
            var blockers = errors.Where(e => e.Severity == ExportErrorSeverity.Error).ToList();
            if (blockers.Count > 0)
                throw new ExportValidationException(blockers);

            var titleFor = (Card c) => GenerateTitle(c, platform);
            var descFor = (Card c) => GenerateDescription(c);

            await _whatnotExporter.WriteAsync(cards, outputPath, titleFor, descFor, new WhatnotExportOptions());
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
    }
}
