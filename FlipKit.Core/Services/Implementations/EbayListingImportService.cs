using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services.Implementations
{
    /// <summary>
    /// Composes the eBay listings import pipeline: CSV reader → rule-pass
    /// title parser → LLM enricher → Card mapper → repository upsert.
    /// Two-phase API: <see cref="ParseAsync"/> returns a preview the user can
    /// review/edit; <see cref="CommitAsync"/> writes it to the repo.
    /// </summary>
    public class EbayListingImportService : IEbayListingImportService
    {
        private readonly IEbayTitleEnricher _enricher;
        private readonly ICardRepository _cardRepository;
        private readonly IPlayerNameDirectory? _playerDirectory;
        private readonly ILogger<EbayListingImportService> _logger;

        public EbayListingImportService(
            IEbayTitleEnricher enricher,
            ICardRepository cardRepository,
            ILogger<EbayListingImportService> logger,
            IPlayerNameDirectory? playerDirectory = null)
        {
            _enricher = enricher;
            _cardRepository = cardRepository;
            _playerDirectory = playerDirectory;
            _logger = logger;
        }

        public async Task<EbayListingImportPreview> ParseAsync(
            Stream csvStream,
            string sourceFileName,
            CancellationToken ct = default)
        {
            var preview = new EbayListingImportPreview { SourceFileName = sourceFileName };

            IReadOnlyList<EbayListingRow> rows;
            try
            {
                rows = EbayListingsCsvReader.Read(csvStream);
            }
            catch (Exception ex)
            {
                preview.Warnings.Add($"CSV parse failed: {ex.Message}");
                return preview;
            }

            if (rows.Count == 0)
            {
                preview.Warnings.Add("No rows with an Item number found in the CSV.");
                return preview;
            }

            // Rule pass first — synchronous, deterministic. The manufacturer
            // dictionary comes from the checklist directory (reference seed +
            // user imports + saved cards); empty when the directory hasn't
            // loaded, in which case Manufacturer stays null and the LLM pass
            // can still enrich it.
            var manufacturers = _playerDirectory?.IsReady == true
                ? _playerDirectory.Manufacturers
                : (IReadOnlyCollection<string>)Array.Empty<string>();
            var leagueAcronyms = BuildLeagueAcronymMap();
            var parsed = rows.Select(r => EbayTitleParser.Parse(r.Title, manufacturers)).ToList();

            // LLM enrichment — one batch over all titles. Failures inside the
            // enricher are swallowed per-batch (see OpenRouterEbayTitleEnricher),
            // so we always get a result list of the right length.
            IReadOnlyList<EbayTitleEnrichment> enrichments;
            try
            {
                enrichments = await _enricher.EnrichAsync(rows.Select(r => r.Title).ToList(), ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "LLM enrichment failed entirely; falling back to rule-pass-only previews. The user can fill PlayerName/Brand/etc. manually.");
                preview.Warnings.Add(
                    $"LLM enrichment failed: {ex.Message}. Rows are populated from regex only; player/brand/team need manual review.");
                enrichments = rows.Select(_ => new EbayTitleEnrichment(null, null, null, null, null)).ToList();
            }

            for (int i = 0; i < rows.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var csv = rows[i];
                var rule = parsed[i];
                var llm = enrichments[i];
                var existing = await _cardRepository.GetCardByEbayItemIdAsync(csv.EbayItemId!).ConfigureAwait(false);

                var proposed = MergeIntoCard(csv, rule, llm, existing, leagueAcronyms);
                var rowPreview = new EbayImportRowPreview
                {
                    CsvRow = csv,
                    ParsedTitle = rule,
                    Enrichment = llm,
                    ProposedCard = proposed,
                    IsExistingMatch = existing is not null,
                };

                preview.Rows.Add(rowPreview);
                if (existing is null) preview.InsertCount++;
                else preview.UpdateCount++;
            }

            return preview;
        }

        public async Task<EbayListingImportResult> CommitAsync(
            EbayListingImportPreview preview,
            CancellationToken ct = default)
        {
            var result = new EbayListingImportResult();

            foreach (var row in preview.Rows)
            {
                ct.ThrowIfCancellationRequested();
                if (row.Skip)
                {
                    result.Skipped++;
                    continue;
                }

                try
                {
                    if (row.IsExistingMatch && row.ProposedCard.Id > 0)
                    {
                        await _cardRepository.UpdateCardAsync(row.ProposedCard).ConfigureAwait(false);
                        result.Updated++;
                    }
                    else
                    {
                        await _cardRepository.InsertCardAsync(row.ProposedCard).ConfigureAwait(false);
                        result.Inserted++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Import commit failed for EbayItemId {ItemId}", row.CsvRow.EbayItemId);
                    result.Errors.Add($"Item {row.CsvRow.EbayItemId}: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// Builds an acronym→sport dictionary from the directory's seeded
        /// league acronyms. Returns empty when the directory isn't loaded; in
        /// that case <see cref="EbayTitleParser.InferSport(string?, IReadOnlyDictionary{string, Sport})"/>
        /// returns null and Sport stays unfilled. Acronyms whose sport string
        /// can't be parsed into the <see cref="Sport"/> enum are silently
        /// dropped — better to skip than guess wrong.
        /// </summary>
        private IReadOnlyDictionary<string, Sport> BuildLeagueAcronymMap()
        {
            if (_playerDirectory?.IsReady != true)
                return new Dictionary<string, Sport>();

            var dict = new Dictionary<string, Sport>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _playerDirectory.LeagueAcronymToSport)
            {
                if (Enum.TryParse<Sport>(kv.Value, ignoreCase: true, out var parsed))
                    dict[kv.Key] = parsed;
            }
            return dict;
        }

        /// <summary>
        /// Builds the proposed <see cref="Card"/> for one CSV row. When an
        /// existing card matches via <see cref="Card.EbayItemId"/> we mutate
        /// it (preserving image paths, custom notes, etc.) — otherwise we
        /// build a fresh one. LLM nulls don't overwrite existing values.
        /// </summary>
        internal static Card MergeIntoCard(
            EbayListingRow csv,
            EbayParsedTitle rule,
            EbayTitleEnrichment llm,
            Card? existing,
            IReadOnlyDictionary<string, Sport> leagueAcronyms)
        {
            var card = existing ?? new Card();

            // Marketplace linkage — set both whether new or update.
            card.EbayItemId = csv.EbayItemId;
            if (csv.StartDate.HasValue) card.ListedAt = csv.StartDate.Value;

            // Identity. For updates, only fill blanks so user-corrected values stick.
            card.PlayerName = ChooseString(card.PlayerName, llm.PlayerName) ?? string.Empty;
            card.Year = card.Year ?? rule.Year;
            card.CardNumber ??= rule.CardNumber;
            card.Manufacturer ??= rule.Manufacturer;
            card.Brand ??= llm.Brand;
            card.SetName ??= llm.SetName;
            card.Team ??= llm.Team;
            card.ParallelName ??= llm.ParallelName;
            card.SerialNumbered ??= rule.SerialNumbered;

            // Sport heuristic — only fills when blank (preserves user corrections on
            // re-import). Returns null for ambiguous titles and we leave it null
            // rather than guessing wrong. League dictionary comes from the directory.
            if (card.Sport is null)
                card.Sport = EbayTitleParser.InferSport(csv.Title, leagueAcronyms);

            // Default VariationType: anything with a parallel name leaves "Base".
            // (Card initialises VariationType="Base"; we don't override it here
            // because eBay listings rarely tell us the variation type directly.)
            if (string.IsNullOrEmpty(card.VariationType)) card.VariationType = "Base";

            // Boolean flags only flip true (rule pass found evidence) — never
            // back to false on update, otherwise a user-corrected "this was
            // never a rookie" flag would get clobbered by re-import.
            if (rule.IsAuto) card.IsAuto = true;
            if (rule.IsRelic) card.IsRelic = true;
            if (rule.IsRookie) card.IsRookie = true;
            if (rule.IsSSP) card.IsSSP = true;
            if (rule.IsShortPrint) card.IsShortPrint = true;

            // Listing/pricing data straight from CSV columns.
            if (csv.CurrentPrice.HasValue) card.ListingPrice = csv.CurrentPrice.Value;
            else if (csv.StartPrice.HasValue) card.ListingPrice = csv.StartPrice.Value;
            if (csv.AvailableQuantity.HasValue && csv.AvailableQuantity.Value > 0)
                card.Quantity = csv.AvailableQuantity.Value;
            if (!string.IsNullOrWhiteSpace(csv.CustomLabelSku) && string.IsNullOrEmpty(card.Sku))
                card.Sku = csv.CustomLabelSku;

            // Grading — eBay's CD:Professional Grader / Grade columns are
            // populated for graded listings. CertificationNumber goes to CertNumber.
            if (!string.IsNullOrWhiteSpace(csv.GraderProfessional))
            {
                card.IsGraded = true;
                card.GradeCompany ??= csv.GraderProfessional;
            }
            if (!string.IsNullOrWhiteSpace(csv.GradeValue) && string.IsNullOrEmpty(card.GradeValue))
                card.GradeValue = csv.GradeValue;
            if (!string.IsNullOrWhiteSpace(csv.CertificationNumber) && string.IsNullOrEmpty(card.CertNumber))
                card.CertNumber = csv.CertificationNumber;

            // Condition mapping: prefer CD:Card Condition (e.g. "Excellent",
            // "Near mint or better") over the eBay top-level Condition column.
            var condition = csv.CardCondition ?? csv.Condition;
            if (!string.IsNullOrWhiteSpace(condition) && card.Condition == "Near Mint")
                card.Condition = condition;

            // Status: actively listed cards are Listed.
            if (existing is null)
                card.Status = CardStatus.Listed;

            return card;
        }

        private static string? ChooseString(string? existing, string? candidate)
        {
            // Prefer existing only when it's a non-empty user value. The default
            // PlayerName initialiser is empty string, not null, so we treat
            // empty as "no value" here.
            if (!string.IsNullOrWhiteSpace(existing)) return existing;
            return candidate;
        }
    }
}
