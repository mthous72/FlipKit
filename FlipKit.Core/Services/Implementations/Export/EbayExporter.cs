using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Services.Export
{
    /// <summary>
    /// Serializes <see cref="Card"/>s into eBay's "Create new listings" CSV for the
    /// Sports Trading Cards category (CategoryID 261328). Spec reference:
    /// card_listings_export_spec.md §3 (column structure, gotchas) and §4 (Card → eBay
    /// field mapping).
    ///
    /// The output preserves eBay's shipped template header verbatim — including the
    /// UTF-8 BOM, CR-only line endings, the long parameterized *Action(SiteID=...)
    /// column name, the empty rows, and the Info,&gt;&gt;&gt; recommendation rows. Data
    /// rows are appended after the template using the parsed column order, so the
    /// final file is byte-compatible with what eBay's bulk-listing tool expects.
    /// </summary>
    public class EbayExporter
    {
        private const int MaxImageUrls = 24;            // eBay accepts up to 24 in PicURL
        private const int MaxTitleLength = 80;

        private readonly EbayTemplateProvider _template;
        private readonly ShippingProfileNormalizer _shipping;

        public EbayExporter(EbayTemplateProvider template, ShippingProfileNormalizer shipping)
        {
            _template = template;
            _shipping = shipping;
        }

        public async Task<int> WriteAsync(
            IList<Card> cards,
            string outputPath,
            Func<Card, string> titleFor,
            Func<Card, string> descriptionFor,
            EbayExportOptions options)
        {
            // Step 1: write the shipped header bytes verbatim. This preserves the BOM,
            // the CR-only line endings, and every Info,>>> row exactly as eBay shipped
            // them — anything we re-encode risks rejection.
            await using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await fs.WriteAsync(_template.HeaderBytes, 0, _template.HeaderBytes.Length);
            }

            // Step 2: append data rows. We open in append mode and use a fresh CsvWriter
            // configured WITHOUT a header (the template already provided it). Rows use
            // CRLF here — that's fine because eBay's parser handles either CR or CRLF
            // for data rows; mixing is only risky in the header section we left intact.
            await using var writer = new StreamWriter(outputPath, append: true, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
                NewLine = "\r\n",
            });

            // Ensure we start on a fresh line — the template header doesn't end with a
            // line break in some cases.
            await writer.WriteAsync("\r\n");

            int written = 0;
            foreach (var card in cards)
            {
                var values = SerializeRow(card, titleFor, descriptionFor, options);
                foreach (var col in _template.Columns)
                    csv.WriteField(values.TryGetValue(col, out var v) ? v : string.Empty);
                await csv.NextRecordAsync();
                written++;
            }
            return written;
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

            // Action — the literal column name carries SiteID + Currency parameters and
            // changes between template versions. Use FindColumnStartingWith so we don't
            // hard-code the parenthesized parameters.
            var actionCol = _template.FindColumnStartingWith("*Action");
            if (actionCol != null) row[actionCol] = options.UseVerifyAdd ? "VerifyAdd" : "Add";

            row["CustomLabel"]  = card.Sku ?? string.Empty;
            row["*Category"]    = options.CategoryId;
            row["*Title"]       = Truncate(titleFor(card) ?? string.Empty, MaxTitleLength);
            row["*ConditionID"] = ConditionMapper.MapToEbayConditionId(card).ToString(CultureInfo.InvariantCulture);

            // Condition Descriptor columns for graded cards. Spec §3.6 — the literal
            // column names include " - (ID: NNNNN)" suffixes, parsed verbatim from the
            // template. Use the parser-found names so we tolerate eBay revising them.
            if (card.IsGraded)
            {
                var graderCol = _template.FindColumnStartingWith("CD:Professional Grader");
                if (graderCol != null && !string.IsNullOrEmpty(card.GradeCompany))
                    row[graderCol] = MapGraderToEbayLabel(card.GradeCompany);

                var gradeCol = _template.FindColumnStartingWith("CD:Grade");
                if (gradeCol != null && !string.IsNullOrEmpty(card.GradeValue))
                    row[gradeCol] = card.GradeValue;

                var certCol = _template.FindColumnStartingWith("CDA:Certification Number");
                if (certCol != null && !string.IsNullOrEmpty(card.CertNumber))
                    row[certCol] = card.CertNumber;
            }
            else
            {
                // Raw cards: surface the verbal condition via CD:Card Condition (40001).
                var cardCondCol = _template.FindColumnStartingWith("CD:Card Condition");
                if (cardCondCol != null)
                    row[cardCondCol] = MapRawConditionToEbayLabel(card.Condition);
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
