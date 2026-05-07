using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Data;
using FlipKit.Core.Models.Enums;
using FuzzySharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// In-memory cache of distinct catalog values pulled from three sources:
    ///   1. Reference seed (LeagueTeams, KnownManufacturers, KnownBrands) —
    ///      the bootstrap catalog shipped with the app.
    ///   2. User-imported checklists (SetChecklist + nested ChecklistCards) —
    ///      the user's intentional catalog.
    ///   3. User-saved cards (Cards table) — the actual cards the user has
    ///      catalogued. Lets the directory grow from real-world usage.
    /// Singleton-scoped; refresh after each import or batch save to pick up
    /// new values. Lookups are O(1) for set membership and O(N) fuzzy.
    /// </summary>
    public class PlayerNameDirectory : IPlayerNameDirectory
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<PlayerNameDirectory>? _logger;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        // Volatile reference swap on refresh — readers see either the old or new
        // list, never a half-built one. Lists themselves are treated as immutable
        // once assigned so we don't need to lock during lookups.
        private List<string> _names = new();
        private List<string> _brands = new();
        private List<string> _setNames = new();
        private List<string> _subsets = new();
        private HashSet<int> _years = new();
        private HashSet<string> _manufacturers = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _teams = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _teamTokens = new(StringComparer.OrdinalIgnoreCase);

        // Maps every team-recognizable string (full name, city, mascot, alias) to
        // its sport. Populated from LeagueTeam rows; lets the OCR pipeline fill
        // Card.Sport when a team token matches.
        private Dictionary<string, string> _teamSportLookup = new(StringComparer.OrdinalIgnoreCase);

        // Maps league acronym → sport. Sourced from LeagueAcronym reference rows;
        // replaces the hardcoded SportKeywords table that used to live in
        // EbayTitleParser.
        private Dictionary<string, string> _leagueAcronymToSport = new(StringComparer.OrdinalIgnoreCase);

        // Parallel / insert names — sourced from KnownVariation reference rows
        // and ChecklistCard subsets so the OCR parser can populate ParallelName.
        private List<string> _parallels = new();

        // Grading-authority codes (PSA / BGS / CGC / SGC / CSG / BCCG) sourced
        // from GradingAuthority reference rows.
        private List<string> _gradingAuthorityCodes = new();

        private bool _ready;

        public PlayerNameDirectory(IServiceProvider services, ILogger<PlayerNameDirectory>? logger = null)
        {
            _services = services;
            _logger = logger;
        }

        public bool IsReady => _ready;
        public int Count => _names.Count;

        public async Task RefreshAsync(CancellationToken ct = default)
        {
            await _refreshLock.WaitAsync(ct);
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();

                // Source 1: Reference seed
                var leagueTeams = await db.LeagueTeams.AsNoTracking().ToListAsync(ct);
                var knownMfrs = await db.KnownManufacturers.AsNoTracking().ToListAsync(ct);
                var knownBrandsRef = await db.KnownBrands.AsNoTracking().ToListAsync(ct);
                var knownVariations = await db.KnownVariations.AsNoTracking().ToListAsync(ct);
                var gradingAuthorities = await db.GradingAuthorities.AsNoTracking().ToListAsync(ct);
                var leagueAcronyms = await db.LeagueAcronyms.AsNoTracking().ToListAsync(ct);

                // Source 2: Imported checklists
                var sets = await db.SetChecklists
                    .AsNoTracking()
                    .Select(s => new
                    {
                        s.Manufacturer,
                        s.Brand,
                        s.Year,
                        s.Cards,
                    })
                    .ToListAsync(ct);

                // Source 3: Cards the user has actually saved — the directory
                // grows from real usage, not just bootstrap data.
                var savedCardFields = await db.Cards
                    .AsNoTracking()
                    .Select(c => new
                    {
                        c.PlayerName,
                        c.Manufacturer,
                        c.Brand,
                        c.SetName,
                        c.Team,
                        c.Year,
                        c.ParallelName,
                    })
                    .ToListAsync(ct);

                // Build aggregated caches. Each source is unioned into the same
                // collection — duplicates are eliminated by case-insensitive set
                // semantics. Empty / null values are filtered out.

                _names = sets.SelectMany(s => s.Cards).Select(c => c.PlayerName)
                    .Concat(savedCardFields.Select(c => c.PlayerName))
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _brands = knownBrandsRef.Select(b => b.Name)
                    .Concat(sets.Select(s => s.Brand))
                    .Concat(savedCardFields.Select(c => c.Brand))
                    .Where(b => !string.IsNullOrWhiteSpace(b))
                    .Select(b => b!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Synthesize a display name per imported set ("2025 Panini Mosaic"),
                // and pull the user's typed SetName values from saved cards too.
                var importedSetNames = sets
                    .Where(s => !string.IsNullOrWhiteSpace(s.Brand))
                    .Select(s => string.Join(" ", new[]
                    {
                        s.Year > 0 ? s.Year.ToString() : null,
                        string.IsNullOrWhiteSpace(s.Manufacturer) ? null : s.Manufacturer.Trim(),
                        s.Brand!.Trim(),
                    }.Where(p => !string.IsNullOrEmpty(p))));

                _setNames = importedSetNames
                    .Concat(savedCardFields.Select(c => c.SetName))
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _subsets = sets
                    .SelectMany(s => s.Cards)
                    .Select(c => c.Subset)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _years = sets.Select(s => s.Year)
                    .Concat(savedCardFields.Where(c => c.Year.HasValue).Select(c => c.Year!.Value))
                    .Where(y => y > 0)
                    .ToHashSet();

                _manufacturers = knownMfrs.Select(m => m.Name)
                    .Concat(sets.Select(s => s.Manufacturer))
                    .Concat(savedCardFields.Select(c => c.Manufacturer))
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => m!.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Teams: everything from reference + imports + saved cards.
                _teams = leagueTeams.Select(t => t.TeamName)
                    .Concat(sets.SelectMany(s => s.Cards).Select(c => c.Team))
                    .Concat(savedCardFields.Select(c => c.Team))
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t!.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                _teamTokens = _teams
                    .SelectMany(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    .Where(w => w.Length > 1)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Team→sport lookup is built ONLY from reference data. Imported
                // checklists carry team values without a guaranteed Sport column,
                // and saved cards have Sport set already — neither helps here.
                var sportLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in leagueTeams)
                {
                    if (string.IsNullOrWhiteSpace(t.Sport)) continue;
                    AddSportLookup(sportLookup, t.TeamName, t.Sport);
                    AddSportLookup(sportLookup, t.City, t.Sport);
                    AddSportLookup(sportLookup, t.Mascot, t.Sport);
                    foreach (var alias in t.Aliases)
                        AddSportLookup(sportLookup, alias, t.Sport);
                }
                _teamSportLookup = sportLookup;

                // League-acronym → sport lookup. Replaces the hardcoded
                // SportKeywords table previously baked into EbayTitleParser.
                _leagueAcronymToSport = leagueAcronyms
                    .Where(l => !string.IsNullOrWhiteSpace(l.Sport))
                    .GroupBy(l => l.Acronym, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => g.First().Sport,
                        StringComparer.OrdinalIgnoreCase);

                // Parallels: union of seeded universal finishes / inserts and
                // every Subset value from imported ChecklistCards (since those
                // are the user's actual subset names). Saved cards' ParallelName
                // values join the same set so user-typed parallels survive.
                _parallels = knownVariations.Select(v => v.Name)
                    .Concat(sets.SelectMany(s => s.Cards).Select(c => c.Subset))
                    .Concat(savedCardFields.Select(c => c.ParallelName))
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _gradingAuthorityCodes = gradingAuthorities
                    .Select(g => g.Code)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _ready = true;
                _logger?.LogInformation(
                    "PlayerNameDirectory refreshed: {Names} names, {Brands} brands, {Mfrs} mfrs, {Teams} teams ({Tokens} tokens), {Sets} sets, {Subsets} subsets, {Years} years, {SportMap} team-sport entries.",
                    _names.Count, _brands.Count, _manufacturers.Count, _teams.Count,
                    _teamTokens.Count, _setNames.Count, _subsets.Count, _years.Count,
                    _teamSportLookup.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "PlayerNameDirectory refresh failed; lookups will continue against the previous cache.");
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private static void AddSportLookup(Dictionary<string, string> dict, string? key, string sport)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            // Last-write-wins: rare cross-sport name collisions (e.g. "Giants"
            // = NFL/NY and MLB/SF) can't both be the answer for a single
            // candidate, so we accept that the seed order chooses. The OCR
            // pipeline's other gates (city + full team name) typically
            // disambiguate before this lookup ever sees an ambiguous mascot.
            dict[key.Trim()] = sport;
        }

        public PlayerNameMatch? FindBestMatch(string candidate, int minScore = 88)
        {
            var (value, score) = FuzzyExtract(candidate, _names, minScore);
            return value == null ? null : new PlayerNameMatch(value, score);
        }

        public ChecklistFieldMatch? FindBrand(string candidate, int minScore = 85)
        {
            var (value, score) = FuzzyExtract(candidate, _brands, minScore);
            return value == null ? null : new ChecklistFieldMatch(value, score);
        }

        public ChecklistFieldMatch? FindSetName(string candidate, int minScore = 85)
        {
            var (value, score) = FuzzyExtract(candidate, _setNames, minScore);
            return value == null ? null : new ChecklistFieldMatch(value, score);
        }

        public ChecklistFieldMatch? FindSubset(string candidate, int minScore = 85)
        {
            var (value, score) = FuzzyExtract(candidate, _subsets, minScore);
            return value == null ? null : new ChecklistFieldMatch(value, score);
        }

        public bool IsKnownYear(int year) => _ready && _years.Contains(year);

        public string? GetSportForTeam(string candidate)
        {
            if (!_ready || string.IsNullOrWhiteSpace(candidate)) return null;
            return _teamSportLookup.TryGetValue(candidate.Trim(), out var sport) ? sport : null;
        }

        public string? GetSportForLeagueAcronym(string acronym)
        {
            if (!_ready || string.IsNullOrWhiteSpace(acronym)) return null;
            return _leagueAcronymToSport.TryGetValue(acronym.Trim(), out var sport) ? sport : null;
        }

        public IReadOnlyCollection<string> Manufacturers => _manufacturers;
        public IReadOnlyCollection<string> Brands => _brands;
        public IReadOnlyCollection<string> Teams => _teams;
        public IReadOnlySet<string> TeamTokens => _teamTokens;
        public IReadOnlyCollection<string> Parallels => _parallels;
        public IReadOnlyCollection<string> GradingAuthorityCodes => _gradingAuthorityCodes;
        public IReadOnlyDictionary<string, string> LeagueAcronymToSport => _leagueAcronymToSport;

        /// <summary>
        /// Runs FuzzySharp's WeightedRatio extractor against the supplied snapshot
        /// list, returning the best hit at or above the score threshold.
        /// </summary>
        private (string? Value, int Score) FuzzyExtract(string candidate, List<string> snapshot, int minScore)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return (null, 0);
            if (!_ready) return (null, 0);
            if (snapshot.Count == 0) return (null, 0);

            var result = Process.ExtractOne(candidate.Trim(), snapshot);
            if (result == null) return (null, 0);
            if (result.Score < minScore) return (null, 0);
            return (result.Value, result.Score);
        }
    }
}
