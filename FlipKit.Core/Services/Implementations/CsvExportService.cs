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
    /// Per-platform CSV export dispatcher. Validates the input cards via
    /// <see cref="ExportValidator"/>, then routes to the platform-specific exporter
    /// (currently <see cref="WhatnotExporter"/>; eBay lands in the next step).
    ///
    /// Title/description generation lives here because both exporters take callbacks
    /// for those — this keeps the dispatcher as the single owner of the platform-aware
    /// title-template logic without the exporters needing to know about
    /// <see cref="ISettingsService"/>.
    /// </summary>
    public class CsvExportService : IExportService
    {
        private readonly ISettingsService _settingsService;
        private readonly TitleTemplateService _titleTemplateService;
        private readonly WhatnotExporter _whatnotExporter;
        private readonly EbayExporter _ebayExporter;
        private readonly ExportValidator _validator;

        public CsvExportService(
            ISettingsService settingsService,
            WhatnotExporter whatnotExporter,
            EbayExporter ebayExporter,
            ExportValidator validator)
        {
            _settingsService = settingsService;
            _whatnotExporter = whatnotExporter;
            _ebayExporter = ebayExporter;
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
            var asList = new List<Card> { card };
            var errors = settings.ActiveExportPlatform == ExportPlatform.eBay
                ? _validator.ValidateForEbay(asList)
                : _validator.ValidateForWhatnot(asList);
            return errors
                .Where(e => e.Severity == ExportErrorSeverity.Error)
                .Select(e => e.Message)
                .ToList();
        }

        public async Task ExportCsvAsync(List<Card> cards, string outputPath)
        {
            var settings = _settingsService.Load();
            await ExportCsvAsync(cards, outputPath, settings.ActiveExportPlatform);
        }

        public async Task ExportCsvAsync(List<Card> cards, string outputPath, ExportPlatform platform)
        {
            // 1. Pre-flight validation. Blocking errors throw; warnings are silently
            //    accepted (the caller can re-run the validator directly to surface them).
            var errors = platform == ExportPlatform.eBay
                ? _validator.ValidateForEbay(cards)
                : _validator.ValidateForWhatnot(cards);
            var blockers = errors.Where(e => e.Severity == ExportErrorSeverity.Error).ToList();
            if (blockers.Count > 0)
                throw new ExportValidationException(blockers);

            // 2. Dispatch to the platform-specific exporter.
            //    eBay lands in the next step; for now, only Whatnot writes a real file
            //    while Generic / COMC fall through to the Whatnot writer (matches the
            //    pre-refactor behavior — those platforms always produced Whatnot-style
            //    CSVs but with platform-specific titles).
            var settings = _settingsService.Load();
            var titleFor = (Card c) => GenerateTitle(c, platform);
            var descFor = (Card c) => GenerateDescription(c);

            switch (platform)
            {
                case ExportPlatform.eBay:
                    await _ebayExporter.WriteAsync(cards, outputPath, titleFor, descFor, new EbayExportOptions
                    {
                        CategoryId      = "261328",
                        Duration        = "GTC",
                        SellerLocation  = settings.EbaySellerLocation,
                        DispatchTimeMax = settings.EbayDispatchTimeMax,
                        ReturnsAccepted = settings.EbayReturnsAccepted,
                        UseVerifyAdd    = settings.EbayUseVerifyAdd,
                    });
                    break;

                default:
                    await _whatnotExporter.WriteAsync(
                        cards, outputPath, titleFor, descFor, new WhatnotExportOptions());
                    break;
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
    }
}
