using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Fuzzy lookup of OCR/AI-extracted candidate text against the population of
    /// metadata values currently in the user's imported checklists. Acts as a
    /// reality-check on free-form text extracted from card images: a candidate
    /// is real only if some published checklist somewhere mentions a value very
    /// similar to it. Lookup is global — the directory is not scoped to any
    /// single set or year because a name/brand/subset should be valid across
    /// the whole catalog.
    /// </summary>
    /// <remarks>
    /// The interface name is preserved as <c>IPlayerNameDirectory</c> for backwards
    /// compatibility with the original wiring; it now exposes brand, set, subset,
    /// and year lookups in addition to player names.
    /// </remarks>
    public interface IPlayerNameDirectory
    {
        /// <summary>
        /// Whether the directory has loaded names from the database. Lookups before
        /// the first refresh return null rather than throwing.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Number of distinct player names currently cached. Exposed for
        /// diagnostics and tests, not user-facing UI.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// (Re)loads all distinct, non-empty values from the checklist tables into
        /// the in-memory cache. Safe to call on every checklist import; cheap
        /// relative to the import itself.
        /// </summary>
        Task RefreshAsync(CancellationToken ct = default);

        /// <summary>
        /// Returns the best fuzzy match for a candidate player name. Scoring uses
        /// FuzzySharp's WeightedRatio (Levenshtein-style + token-set); 100 is exact,
        /// 90+ is near-exact / OCR-noise tolerant, 80 starts to admit false
        /// positives on common surnames.
        /// </summary>
        PlayerNameMatch? FindBestMatch(string candidate, int minScore = 88);

        /// <summary>
        /// Returns the best fuzzy match for a candidate brand string (e.g. OCR
        /// reads "Pannini Mosiac" — should resolve to "Panini Mosaic" if the user
        /// has imported a Mosaic checklist). Brands are pulled from
        /// <c>SetChecklist.Brand</c> across all imported checklists.
        /// </summary>
        ChecklistFieldMatch? FindBrand(string candidate, int minScore = 85);

        /// <summary>
        /// Returns the best fuzzy match for a candidate set name (the
        /// <c>SetChecklist.SetName</c> display string, e.g. "2025 Panini Mosaic").
        /// </summary>
        ChecklistFieldMatch? FindSetName(string candidate, int minScore = 85);

        /// <summary>
        /// Returns the best fuzzy match for a candidate subset name (e.g.
        /// "Future Stars", "Diamond Kings"). Subsets are pulled from
        /// <c>ChecklistCard.Subset</c> across all imported checklists.
        /// </summary>
        ChecklistFieldMatch? FindSubset(string candidate, int minScore = 85);

        /// <summary>
        /// True when the supplied year appears anywhere in the user's imported
        /// checklists. Used as a sanity gate on OCR-extracted years before
        /// trusting them.
        /// </summary>
        bool IsKnownYear(int year);

        /// <summary>
        /// Looks up the sport of a team by any of its known names — full team
        /// name ("Atlanta Falcons"), city ("Atlanta"), mascot ("Falcons"), or
        /// alias ("Bucs"). Returns null when no team matches. Lets the OCR
        /// pipeline populate <c>Card.Sport</c> from team text alone.
        /// </summary>
        string? GetSportForTeam(string candidate);

        /// <summary>
        /// Looks up the sport for a league acronym ("NFL", "F1", "WWE").
        /// Returns null when the acronym isn't seeded. Used by both OCR and
        /// eBay-title parsing to populate Sport from a league mention alone.
        /// </summary>
        string? GetSportForLeagueAcronym(string acronym);

        /// <summary>
        /// Distinct parallel / insert / variation names from the reference seed
        /// (and ChecklistCard subsets). Used by the OCR parser to pick out the
        /// ParallelName from a card's OCR'd text.
        /// </summary>
        IReadOnlyCollection<string> Parallels { get; }

        /// <summary>
        /// Grade-authority codes ("PSA", "BGS", "CGC", "SGC", "CSG", "BCCG")
        /// from the reference seed. Empty until the seed runs.
        /// </summary>
        IReadOnlyCollection<string> GradingAuthorityCodes { get; }

        /// <summary>
        /// Full acronym→sport-name map from the seeded LeagueAcronyms table.
        /// Caller can convert the sport string to the <c>Sport</c> enum at
        /// the call site (the interface is sport-string to keep
        /// FlipKit.Core.Services free of enum-coupling at the directory level).
        /// </summary>
        IReadOnlyDictionary<string, string> LeagueAcronymToSport { get; }

        /// <summary>
        /// Distinct manufacturers from imported checklists (e.g. "Panini",
        /// "Topps"), uppercased and trimmed for case-insensitive lookup.
        /// Empty when no checklists have been imported yet.
        /// </summary>
        IReadOnlyCollection<string> Manufacturers { get; }

        /// <summary>
        /// Distinct brand names from imported checklists (e.g. "Mosaic",
        /// "Prizm"). Empty when no checklists have been imported yet.
        /// </summary>
        IReadOnlyCollection<string> Brands { get; }

        /// <summary>
        /// Distinct team values from imported checklist cards (e.g. "New York
        /// Yankees", "Atlanta Falcons"), uppercased and trimmed. Used by the
        /// OCR parser to reject team names from being mistaken for player
        /// names. Empty when no checklists have been imported yet.
        /// </summary>
        IReadOnlyCollection<string> Teams { get; }

        /// <summary>
        /// Tokenized forms of <see cref="Teams"/> — every word that appears in
        /// any team name, uppercased. So "Atlanta Falcons" contributes
        /// "ATLANTA" and "FALCONS". Lets the parser reject single OCR words
        /// that are clearly part of a team name without needing fuzzy match.
        /// Returned as <see cref="IReadOnlySet{T}"/> so contains-checks stay O(1).
        /// </summary>
        IReadOnlySet<string> TeamTokens { get; }

        /// <summary>
        /// Reconstructs an <see cref="OcrHint"/> from a persisted Card by
        /// re-querying the directory for each field. Used by the Enhance
        /// flow on saved Cards (My Cards / Edit / Surprise Set / Web) where
        /// the original FieldConfidence list has been dropped during DB
        /// persistence. Fields that still resolve to a directory match land
        /// in <see cref="OcrHint.VerifiedFieldNames"/> so the LLM is asked
        /// to echo them verbatim. Returns an empty hint with no verified
        /// fields when the directory hasn't loaded yet.
        /// </summary>
        OcrHint BuildHintFromCard(Card card);
    }

    public record PlayerNameMatch(string Name, int Score);
    public record ChecklistFieldMatch(string Value, int Score);
}
