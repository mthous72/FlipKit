using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Helpers
{
    /// <summary>
    /// Extracts structured fields from an eBay listing title using regex + a
    /// manufacturer dictionary. This is the cheap "rule pass" of the hybrid
    /// title-parse pipeline: anything it can't fill confidently is left null
    /// and surfaced via <see cref="EbayParsedTitle.LowConfidenceFields"/> so
    /// the caller can decide whether to send the title to an LLM for a second
    /// pass or ask the user to fill it in.
    ///
    /// Player name, brand, set name, parallel, and team are intentionally not
    /// extracted by rules — they need either an LLM or the user. They always
    /// land in LowConfidenceFields when null.
    /// </summary>
    public static class EbayTitleParser
    {
        private static readonly Regex YearRegex = new(
            @"(?<![\d/])((?:19|20)\d{2})(?:[-–](\d{2}))?(?![\d/])",
            RegexOptions.Compiled);

        private static readonly Regex SerialRegex = new(
            @"(?:^|\s|/)(\d{1,4})/(\d{1,4})(?!\d)",
            RegexOptions.Compiled);

        // Allow '#' before the slash too — sellers commonly write "#/49" as
        // shorthand for "numbered to 49 with no numerator visible".
        private static readonly Regex SerialDenomOnlyRegex = new(
            @"(?:^|\s|#)/(\d{1,4})(?!\d)",
            RegexOptions.Compiled);

        private static readonly Regex CardNumberRegex = new(
            @"#([A-Za-z0-9]+(?:-[A-Za-z0-9]+)*)",
            RegexOptions.Compiled);

        private static readonly Regex AutoRegex = new(
            @"\b(?:Auto(?:graph(?:ed)?)?|AUTO)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RelicRegex = new(
            @"\b(?:Patch|Relic|Jersey|Mem|Game[- ]Used|GU)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RookieRegex = new(
            @"\b(?:RC|Rookies?)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SspRegex = new(
            @"\bSSP\b",
            RegexOptions.Compiled);

        // SP but not SSP — short-print without a leading 'S' before the SP token.
        private static readonly Regex SpRegex = new(
            @"(?<![A-Z])SP\b",
            RegexOptions.Compiled);

        /// <summary>
        /// Parses without a manufacturer dictionary — leaves
        /// <see cref="EbayParsedTitle.Manufacturer"/> null and adds it to
        /// <c>LowConfidenceFields</c>. Use the overload that accepts a
        /// <paramref name="manufacturers"/> collection (sourced from the
        /// checklist directory) to enable manufacturer extraction.
        /// </summary>
        public static EbayParsedTitle Parse(string title)
            => Parse(title, manufacturers: Array.Empty<string>());

        /// <summary>
        /// Parses an eBay listing title using regex + the supplied manufacturer
        /// dictionary. Manufacturers come from
        /// <see cref="FlipKit.Core.Services.IPlayerNameDirectory.Manufacturers"/>
        /// at the call site so this helper carries no card-catalog facts of its
        /// own — the dictionary is data, not code.
        /// </summary>
        public static EbayParsedTitle Parse(string title, IReadOnlyCollection<string> manufacturers)
        {
            var result = new EbayParsedTitle { OriginalTitle = title ?? string.Empty };
            if (string.IsNullOrWhiteSpace(title))
            {
                result.LowConfidenceFields = AllSoftFields();
                return result;
            }

            var yearMatch = YearRegex.Match(title);
            if (yearMatch.Success)
            {
                result.Year = int.Parse(yearMatch.Groups[1].Value);
                if (yearMatch.Groups[2].Success)
                {
                    // "1997-98" → end year is 1998. "2020-21" → 2021.
                    var century = result.Year.Value / 100;
                    var endTwo = int.Parse(yearMatch.Groups[2].Value);
                    var endYear = century * 100 + endTwo;
                    if (endYear < result.Year.Value) endYear += 100;
                    result.YearEnd = endYear;
                }
            }

            // Manufacturer match — order longest-first so multi-word entries
            // ("Upper Deck", "Press Pass") win over any single-word substring.
            // When `manufacturers` is empty (caller didn't supply a dictionary),
            // this loop is a no-op and Manufacturer stays null.
            foreach (var m in manufacturers.OrderByDescending(x => x.Length))
            {
                if (Regex.IsMatch(title, $@"\b{Regex.Escape(m)}\b", RegexOptions.IgnoreCase))
                {
                    result.Manufacturer = m;
                    break;
                }
            }

            var serialMatch = SerialRegex.Match(title);
            if (serialMatch.Success)
            {
                result.SerialNumbered = $"{serialMatch.Groups[1].Value}/{serialMatch.Groups[2].Value}";
            }
            else
            {
                var denomMatch = SerialDenomOnlyRegex.Match(title);
                if (denomMatch.Success)
                    result.SerialNumbered = $"/{denomMatch.Groups[1].Value}";
            }

            var cardNumMatch = CardNumberRegex.Match(title);
            if (cardNumMatch.Success)
                result.CardNumber = cardNumMatch.Groups[1].Value;

            result.IsAuto = AutoRegex.IsMatch(title);
            result.IsRelic = RelicRegex.IsMatch(title);
            result.IsRookie = RookieRegex.IsMatch(title);
            result.IsSSP = SspRegex.IsMatch(title);
            // SP only counts as a short-print flag when SSP didn't already match —
            // titles like "...SSP..." would otherwise double-flag.
            result.IsShortPrint = !result.IsSSP && SpRegex.IsMatch(title);

            var low = new List<string>();
            if (result.Year is null)         low.Add(nameof(EbayParsedTitle.Year));
            if (result.Manufacturer is null) low.Add(nameof(EbayParsedTitle.Manufacturer));
            // Player / Brand / SetName / ParallelName / Team always need a second pass.
            low.Add(nameof(EbayParsedTitle.PlayerName));
            low.Add(nameof(EbayParsedTitle.Brand));
            low.Add(nameof(EbayParsedTitle.SetName));
            low.Add(nameof(EbayParsedTitle.ParallelName));
            low.Add(nameof(EbayParsedTitle.Team));
            result.LowConfidenceFields = low;

            return result;
        }

        private static IReadOnlyList<string> AllSoftFields() => new[]
        {
            nameof(EbayParsedTitle.Year),
            nameof(EbayParsedTitle.Manufacturer),
            nameof(EbayParsedTitle.PlayerName),
            nameof(EbayParsedTitle.Brand),
            nameof(EbayParsedTitle.SetName),
            nameof(EbayParsedTitle.ParallelName),
            nameof(EbayParsedTitle.Team),
        };

        /// <summary>
        /// Best-effort map of an eBay listing title to a <see cref="Sport"/> using
        /// league acronyms supplied by the caller. Returns null when no signal is
        /// present so the caller can leave the field blank rather than guessing.
        /// </summary>
        /// <remarks>
        /// Caller passes a <c>(acronym, sport)</c> dictionary built from the
        /// directory's seeded <c>LeagueAcronyms</c> table. Whole-word matching
        /// is case-insensitive; first match wins, so callers should order
        /// entries from most-specific to least if there's overlap.
        /// </remarks>
        public static Sport? InferSport(string? title, IReadOnlyDictionary<string, Sport> leagueAcronyms)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            if (leagueAcronyms.Count == 0) return null;

            foreach (var (token, sport) in leagueAcronyms)
            {
                if (Regex.IsMatch(title, $@"\b{Regex.Escape(token)}\b", RegexOptions.IgnoreCase))
                    return sport;
            }
            return null;
        }

        /// <summary>
        /// No-arg overload for callers that don't have a directory wired in
        /// (or for scripts/tests). Always returns null since the dictionary
        /// is empty.
        /// </summary>
        public static Sport? InferSport(string? title)
            => InferSport(title, new Dictionary<string, Sport>());
    }

    public class EbayParsedTitle
    {
        public string OriginalTitle { get; set; } = string.Empty;

        // Confidently parsed by rules
        public int? Year { get; set; }
        public int? YearEnd { get; set; }
        public string? Manufacturer { get; set; }
        public string? CardNumber { get; set; }
        public string? SerialNumbered { get; set; }
        public bool IsAuto { get; set; }
        public bool IsRelic { get; set; }
        public bool IsRookie { get; set; }
        public bool IsSSP { get; set; }
        public bool IsShortPrint { get; set; }

        // LLM / user territory — null when the rule pass can't fill confidently.
        public string? PlayerName { get; set; }
        public string? Brand { get; set; }
        public string? SetName { get; set; }
        public string? ParallelName { get; set; }
        public string? Team { get; set; }

        public IReadOnlyList<string> LowConfidenceFields { get; set; } = Array.Empty<string>();
    }
}
