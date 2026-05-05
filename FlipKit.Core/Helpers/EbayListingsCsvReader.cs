using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;

namespace FlipKit.Core.Helpers
{
    /// <summary>
    /// Reads an eBay Seller Hub "All active listings" CSV export. Tolerant of
    /// the BOM eBay prepends to the file, blank trailing rows, and quoted
    /// numerics like <c>"1"</c> in the quantity column. Only pulls the columns
    /// the importer actually maps to <c>Card</c> — the export has 30+ columns,
    /// most of which are eBay-internal taxonomy noise.
    /// </summary>
    public static class EbayListingsCsvReader
    {
        // eBay's "Start date" / "End date" columns look like "Apr-29-26 17:02:34 PDT".
        // We parse the date portion only and drop the timezone (Card.ListedAt is a
        // local DateTime; the importer stores UTC if needed).
        private const string EbayDateFormat = "MMM-dd-yy HH:mm:ss";

        public static IReadOnlyList<EbayListingRow> Read(Stream csvStream)
        {
            if (csvStream is null) throw new ArgumentNullException(nameof(csvStream));

            var rows = new List<EbayListingRow>();
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,        // CSV has variable-width tail columns; tolerate
                BadDataFound = null,             // Skip embedded quote weirdness rather than throw
                TrimOptions = TrimOptions.Trim,
                IgnoreBlankLines = true,
            };

            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, config);

            csv.Read();
            csv.ReadHeader();

            while (csv.Read())
            {
                var row = new EbayListingRow
                {
                    EbayItemId = TryGet(csv, "Item number"),
                    Title = TryGet(csv, "Title") ?? string.Empty,
                    VariationDetails = TryGet(csv, "Variation details"),
                    CustomLabelSku = TryGet(csv, "Custom label (SKU)"),
                    AvailableQuantity = TryGetInt(csv, "Available quantity"),
                    Format = TryGet(csv, "Format"),
                    Currency = TryGet(csv, "Currency"),
                    StartPrice = TryGetDecimal(csv, "Start price"),
                    CurrentPrice = TryGetDecimal(csv, "Current price"),
                    SoldQuantity = TryGetInt(csv, "Sold quantity"),
                    Watchers = TryGetInt(csv, "Watchers"),
                    StartDate = TryGetEbayDate(csv, "Start date"),
                    EndDate = TryGetEbayDate(csv, "End date"),
                    Condition = TryGet(csv, "Condition"),
                    GraderProfessional = TryGet(csv, "CD:Professional Grader - (ID: 27501)"),
                    GradeValue = TryGet(csv, "CD:Grade - (ID: 27502)"),
                    CertificationNumber = TryGet(csv, "CDA:Certification Number - (ID: 27503)"),
                    CardCondition = TryGet(csv, "CD:Card Condition - (ID: 40001)"),
                };

                // Skip rows missing the upsert key entirely — they're useless to us.
                if (string.IsNullOrWhiteSpace(row.EbayItemId)) continue;
                rows.Add(row);
            }

            return rows;
        }

        private static string? TryGet(CsvReader csv, string field)
        {
            try
            {
                var v = csv.GetField(field);
                return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            }
            catch { return null; }
        }

        private static int? TryGetInt(CsvReader csv, string field)
        {
            var raw = TryGet(csv, field);
            if (raw is null) return null;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
        }

        private static decimal? TryGetDecimal(CsvReader csv, string field)
        {
            var raw = TryGet(csv, field);
            if (raw is null) return null;
            return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
        }

        private static DateTime? TryGetEbayDate(CsvReader csv, string field)
        {
            var raw = TryGet(csv, field);
            if (raw is null) return null;

            // "Apr-29-26 17:02:34 PDT" — strip the trailing TZ token and parse the rest.
            var lastSpace = raw.LastIndexOf(' ');
            var datePart = lastSpace > 0 ? raw[..lastSpace] : raw;

            if (DateTime.TryParseExact(datePart, EbayDateFormat,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;

            // Fallback: let DateTime do its best with the full string in case eBay
            // changes the format on a future export.
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dt)
                ? dt
                : null;
        }
    }

    public sealed class EbayListingRow
    {
        public string? EbayItemId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? VariationDetails { get; set; }
        public string? CustomLabelSku { get; set; }
        public int? AvailableQuantity { get; set; }
        public string? Format { get; set; }
        public string? Currency { get; set; }
        public decimal? StartPrice { get; set; }
        public decimal? CurrentPrice { get; set; }
        public int? SoldQuantity { get; set; }
        public int? Watchers { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Condition { get; set; }
        public string? GraderProfessional { get; set; }
        public string? GradeValue { get; set; }
        public string? CertificationNumber { get; set; }
        public string? CardCondition { get; set; }
    }
}
