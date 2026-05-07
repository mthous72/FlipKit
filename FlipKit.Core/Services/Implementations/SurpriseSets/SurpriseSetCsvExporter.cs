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
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services.Export;

namespace FlipKit.Core.Services.Implementations.SurpriseSets
{
    public sealed class SurpriseSetCsvExporter : ISurpriseSetCsvExporter
    {
        private readonly ISurpriseSetRepository _repository;
        private readonly ISurpriseSetValidator _validator;
        private readonly ISurpriseSetDescriptionGenerator _descriptionGenerator;

        public SurpriseSetCsvExporter(
            ISurpriseSetRepository repository,
            ISurpriseSetValidator validator,
            ISurpriseSetDescriptionGenerator descriptionGenerator)
        {
            _repository = repository;
            _validator = validator;
            _descriptionGenerator = descriptionGenerator;
        }

        public async Task<SurpriseSetExportResult> ExportAsync(int setId, string outputPath)
        {
            var set = await _repository.GetByIdWithCardsAsync(setId)
                ?? throw new InvalidOperationException($"Surprise set {setId} not found.");

            if (set.State is SurpriseSetState.Live or SurpriseSetState.Completed or SurpriseSetState.Cancelled)
                throw new InvalidOperationException(
                    $"Cannot export surprise set {setId}: state is {set.State}.");

            var cards = set.Cards
                .OrderBy(c => c.SurpriseSetSlot ?? int.MaxValue)
                .ToList();

            var issues = _validator.Validate(set, cards);
            var blocking = issues.Where(i => i.Severity == IssueSeverity.Error).ToList();
            if (blocking.Count > 0)
                return new SurpriseSetExportResult { Success = false, BlockingIssues = blocking };

            var description = _descriptionGenerator.Generate(set, cards);
            var rowsWritten = await WriteCsvAsync(set, cards, description, outputPath);

            if (set.State == SurpriseSetState.Draft)
            {
                set.State = SurpriseSetState.Exported;
                set.ExportedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(set);
            }

            return new SurpriseSetExportResult { Success = true, RowsWritten = rowsWritten };
        }

        private static async Task<int> WriteCsvAsync(
            SurpriseSet set,
            IList<Card> cards,
            string description,
            string outputPath)
        {
            await using var writer = new StreamWriter(
                outputPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            });

            foreach (var col in WhatnotExporter.Columns)
                csv.WriteField(col);
            await csv.NextRecordAsync();

            var spotPrice = Math.Max(1, (int)Math.Round(set.SpotPrice, MidpointRounding.AwayFromZero));
            var offerable = set.SharedListingType == "Buy it Now"
                ? (set.Offerable ? "TRUE" : "FALSE")
                : string.Empty;

            int written = 0;
            foreach (var card in cards)
            {
                int slot = card.SurpriseSetSlot ?? (written + 1);
                var row = BuildRow(set, card, slot, spotPrice, offerable, description);

                foreach (var col in WhatnotExporter.Columns)
                    csv.WriteField(row.TryGetValue(col, out var v) ? v : string.Empty);
                await csv.NextRecordAsync();
                written++;
            }

            return written;
        }

        private static IDictionary<string, string> BuildRow(
            SurpriseSet set,
            Card card,
            int slot,
            int spotPrice,
            string offerable,
            string description)
        {
            var sku = $"FK-SET-{set.Id:D5}-{slot:D3}";

            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Category"]         = set.SharedWhatnotCategory,
                ["Sub Category"]     = set.SharedWhatnotSubcategory ?? string.Empty,
                ["Title"]            = Truncate(set.Title, 80),
                ["Description"]      = description,
                ["Quantity"]         = "1",
                ["Type"]             = set.SharedListingType,
                ["Price"]            = spotPrice.ToString(CultureInfo.InvariantCulture),
                ["Shipping Profile"] = set.SharedShippingProfile,
                ["Offerable"]        = offerable,
                ["Hazmat"]           = "Not Hazmat",
                ["Condition"]        = set.SharedCondition,
                ["Cost Per Item"]    = card.CostBasis?.ToString("F2", CultureInfo.InvariantCulture) ?? string.Empty,
                ["SKU"]              = sku,
                ["Image URL 1"]      = set.SharedImageUrl1 ?? string.Empty,
                ["Image URL 2"]      = set.SharedImageUrl2 ?? string.Empty,
                ["Image URL 3"]      = set.SharedImageUrl3 ?? string.Empty,
                ["Image URL 4"]      = set.SharedImageUrl4 ?? string.Empty,
                ["Image URL 5"]      = set.SharedImageUrl5 ?? string.Empty,
                ["Image URL 6"]      = set.SharedImageUrl6 ?? string.Empty,
                ["Image URL 7"]      = set.SharedImageUrl7 ?? string.Empty,
                ["Image URL 8"]      = set.SharedImageUrl8 ?? string.Empty,
            };
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s[..max];
    }
}
