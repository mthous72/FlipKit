using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Desktop.Services
{
    /// <summary>
    /// Heuristic OCR text parser for sports cards. Holds vocabulary (English filler,
    /// grading jargon) and shape rules (regex for years, card numbers, serial format)
    /// — never card-catalog facts. Real-world card data (manufacturers, brands,
    /// teams, player names) is supplied at parse time via <see cref="OcrParseContext"/>,
    /// which the OCR service builds from the user's imported checklists.
    /// </summary>
    public static class OcrTextParser
    {
        // Words that, if present in a candidate line, mean it is a sentence/bio
        // rather than a player name. Kept lowercase; comparisons are case-insensitive.
        private static readonly HashSet<string> BioWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "is", "are", "was", "were", "of", "with", "for", "by",
            "in", "on", "at", "to", "from", "an", "a", "but", "or", "as", "has",
            "have", "had", "be", "been", "this", "that", "those", "these",
            "his", "her", "their", "they", "him", "she", "he", "we", "us", "our",
            "you", "your", "if", "when", "while", "who", "what", "which",
            "than", "then", "into", "out", "over", "after", "before", "all",
            "any", "no", "not", "now", "still", "just", "long", "ago",
        };

        // Condition / grading / generic-card terms that should never appear
        // inside a player name. Anchored on the words real graded-card slabs
        // and promotional/marketing labels print, so 3-4 word slab phrases
        // (e.g. "BECKETT GRADING SERVICES", "PROFESSIONAL SPORTS AUTHENTICATOR")
        // get rejected as candidates.
        private static readonly HashSet<string> ConditionWords = new(StringComparer.OrdinalIgnoreCase)
        {
            // Condition / grade language
            "mint", "near", "gem", "pristine", "altered", "good", "fair", "poor",
            // Grading authorities + their support words
            "psa", "bgs", "cgc", "sgc", "csg",
            "professional", "sports", "authenticator", "authentication",
            "beckett", "grading", "grader", "graded",
            "service", "services", "certified", "certification",
            "guaranty", "guaranteed", "company", "registry", "authenticity",
            "holder", "label", "slab", "trading", "collectible", "collectibles",
            "score", "grade", "subgrades",
            // Card-type / promotional / marketing / insert language. Plural forms
            // matter: "Rookies", "Stars", "Kings" all appear in insert subset names
            // ("Red Hot Rookies", "Future Stars", "Diamond Kings") and would pass
            // the shape gates as 2-4-word title-case phrases without these guards.
            "rc", "rookie", "rookies", "auto", "autograph", "autographed",
            "relic", "patch", "rated", "draft", "prospect", "prospects",
            "card", "cards", "checklist", "serial", "numbered", "edition",
            "future", "star", "stars", "legend", "legends", "hall",
            "season", "first", "second", "third",
            "kings", "gridiron", "diamond", "all", "team", "teams",
            // Common insert / parallel labels seen on baseball/football fronts
            "hot", "highlights", "highlight", "image", "vision",
            "crew", "icons", "icon", "milestones", "milestone",
            "moments", "moment", "throwback", "throwbacks", "vintage",
            "combo", "variation", "variations",
            "leader", "leaders", "league",
        };

        public static (Card card, List<FieldConfidence> confidences) Parse(List<string> ocrLines)
            => Parse(ocrLines, backLines: null, context: null);

        public static (Card card, List<FieldConfidence> confidences) Parse(
            List<string> frontLines, List<string>? backLines)
            => Parse(frontLines, backLines, context: null);

        /// <summary>
        /// Parses OCR output into a Card. When <paramref name="backLines"/> is supplied,
        /// candidates that appear on both sides receive a confidence boost — repeated
        /// text across front and back is almost always the player name (or team name),
        /// not bio paragraphs or slab labels. <paramref name="context"/> supplies the
        /// catalog data (manufacturers, brands, team tokens) the parser needs to
        /// recognize fields without hardcoding the catalog. When omitted, the parser
        /// runs without those gates — appropriate for a fresh install with no
        /// imported checklists, or for tests that don't care about catalog awareness.
        /// </summary>
        public static (Card card, List<FieldConfidence> confidences) Parse(
            List<string> frontLines,
            List<string>? backLines,
            OcrParseContext? context)
        {
            context ??= OcrParseContext.Empty;
            var card = new Card
            {
                DataSource = CardDataSource.Ocr,
                Status = CardStatus.Draft,
            };
            var confidences = new List<FieldConfidence>();

            // Combined view for extractors that don't care about side.
            var allLines = new List<string>(frontLines);
            if (backLines != null) allLines.AddRange(backLines);

            var (year, yearConf) = ExtractYear(allLines);
            if (year.HasValue)
            {
                card.Year = year;
                confidences.Add(new FieldConfidence { FieldName = "year", Confidence = yearConf, Reason = "OCR pattern match" });
            }

            var (cardNum, cardNumConf) = ExtractCardNumber(allLines);
            if (!string.IsNullOrEmpty(cardNum))
            {
                card.CardNumber = cardNum;
                confidences.Add(new FieldConfidence { FieldName = "card_number", Confidence = cardNumConf, Reason = "OCR pattern match" });
            }

            var (serialNum, serialConf) = ExtractSerialNumber(allLines);
            if (!string.IsNullOrEmpty(serialNum))
            {
                card.SerialNumbered = serialNum;
                confidences.Add(new FieldConfidence { FieldName = "serial_numbered", Confidence = serialConf, Reason = "OCR pattern match" });
            }

            var (mfr, mfrConf) = ExtractManufacturer(allLines, context.Manufacturers);
            if (!string.IsNullOrEmpty(mfr))
            {
                card.Manufacturer = mfr;
                confidences.Add(new FieldConfidence { FieldName = "manufacturer", Confidence = mfrConf, Reason = "OCR keyword match" });
            }

            var (brand, brandConf) = ExtractBrand(allLines, context.Brands);
            if (!string.IsNullOrEmpty(brand))
            {
                card.Brand = brand;
                confidences.Add(new FieldConfidence { FieldName = "brand", Confidence = brandConf, Reason = "OCR keyword match" });
            }

            var (parallel, parallelConf) = ExtractParallel(allLines, context.Parallels);
            if (!string.IsNullOrEmpty(parallel))
            {
                card.ParallelName = parallel;
                confidences.Add(new FieldConfidence { FieldName = "parallel_name", Confidence = parallelConf, Reason = "OCR keyword match" });
            }

            var (player, playerConf) = ExtractPlayerName(frontLines, backLines, context);
            if (!string.IsNullOrEmpty(player))
            {
                card.PlayerName = NormalizePlayerName(player);
                confidences.Add(new FieldConfidence { FieldName = "player_name", Confidence = playerConf, Reason = "OCR text heuristic" });
            }

            var (isGraded, company, grade) = DetectGrading(allLines);
            if (isGraded)
            {
                card.IsGraded = true;
                card.GradeCompany = company;
                card.GradeValue = grade;
                confidences.Add(new FieldConfidence { FieldName = "is_graded", Confidence = VerificationConfidence.High, Reason = "OCR grading keyword" });
                if (!string.IsNullOrEmpty(company))
                    confidences.Add(new FieldConfidence { FieldName = "grade_company", Confidence = VerificationConfidence.Medium, Reason = "OCR grading keyword" });
                if (!string.IsNullOrEmpty(grade))
                    confidences.Add(new FieldConfidence { FieldName = "grade_value", Confidence = VerificationConfidence.Low, Reason = "OCR pattern match" });
            }

            if (DetectRookie(allLines))
            {
                card.IsRookie = true;
                confidences.Add(new FieldConfidence { FieldName = "is_rookie", Confidence = VerificationConfidence.Medium, Reason = "OCR rookie keyword" });
            }

            return (card, confidences);
        }

        private static (int? year, VerificationConfidence conf) ExtractYear(List<string> lines)
        {
            var yearRegex = new Regex(@"\b(19[5-9]\d|20[0-3]\d)\b");
            foreach (var line in lines)
            {
                var m = yearRegex.Match(line);
                if (m.Success && int.TryParse(m.Value, out var y))
                    return (y, VerificationConfidence.Medium);
            }
            return (null, VerificationConfidence.Low);
        }

        private static (string? cardNum, VerificationConfidence conf) ExtractCardNumber(List<string> lines)
        {
            // Prefer explicit #NNN or No. NNN patterns
            var explicitRegex = new Regex(@"(?:#|No\.?\s*)(\d{1,4})\b", RegexOptions.IgnoreCase);
            foreach (var line in lines)
            {
                var m = explicitRegex.Match(line);
                if (m.Success)
                    return (m.Groups[1].Value, VerificationConfidence.Medium);
            }

            // Bare isolated number as low-confidence fallback (not a serial, not a year)
            var bareRegex = new Regex(@"^\s*(\d{1,4})\s*$");
            foreach (var line in lines)
            {
                var m = bareRegex.Match(line);
                if (m.Success)
                {
                    // Don't match if it looks like a year
                    if (int.TryParse(m.Groups[1].Value, out var n) && n >= 1950 && n <= 2040)
                        continue;
                    return (m.Groups[1].Value, VerificationConfidence.Low);
                }
            }
            return (null, VerificationConfidence.Low);
        }

        private static (string? serialNum, VerificationConfidence conf) ExtractSerialNumber(List<string> lines)
        {
            var serialRegex = new Regex(@"\b(\d+)/(\d+)\b");
            foreach (var line in lines)
            {
                var m = serialRegex.Match(line);
                if (m.Success)
                    return ($"/{m.Groups[2].Value}", VerificationConfidence.Medium);
            }
            return (null, VerificationConfidence.Low);
        }

        private static (string? mfr, VerificationConfidence conf) ExtractManufacturer(
            List<string> lines, IReadOnlyCollection<string> manufacturers)
        {
            if (manufacturers.Count == 0) return (null, VerificationConfidence.Low);

            // Check multi-word first (longest match wins) so "Upper Deck" beats
            // a substring "Deck" that might appear elsewhere.
            var sorted = manufacturers.OrderByDescending(k => k.Length);
            var joined = string.Join("\n", lines);
            foreach (var mfr in sorted)
            {
                if (Regex.IsMatch(joined, @"\b" + Regex.Escape(mfr) + @"\b", RegexOptions.IgnoreCase))
                    return (mfr, VerificationConfidence.Medium);
            }
            return (null, VerificationConfidence.Low);
        }

        /// <summary>
        /// Scans OCR lines for any seeded parallel / insert name. Longest match
        /// wins so multi-word parallels ("Press Proof Silver", "Cracked Ice")
        /// beat any single-word substring they contain ("Silver", "Ice").
        /// First single-line hit wins to keep behavior deterministic.
        /// </summary>
        private static (string? parallel, VerificationConfidence conf) ExtractParallel(
            List<string> lines, IReadOnlyCollection<string> parallels)
        {
            if (parallels.Count == 0) return (null, VerificationConfidence.Low);

            var sorted = parallels.OrderByDescending(p => p.Length);
            var joined = string.Join("\n", lines);
            foreach (var p in sorted)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                if (Regex.IsMatch(joined, @"\b" + Regex.Escape(p) + @"\b", RegexOptions.IgnoreCase))
                    return (p, VerificationConfidence.Medium);
            }
            return (null, VerificationConfidence.Low);
        }

        private static (string? brand, VerificationConfidence conf) ExtractBrand(
            List<string> lines, IReadOnlyCollection<string> brands)
        {
            if (brands.Count == 0) return (null, VerificationConfidence.Low);

            var sorted = brands.OrderByDescending(k => k.Length);
            var joined = string.Join("\n", lines);
            var matches = new List<string>();
            foreach (var brand in sorted)
            {
                if (Regex.IsMatch(joined, @"\b" + Regex.Escape(brand) + @"\b", RegexOptions.IgnoreCase))
                    matches.Add(brand);
            }
            if (matches.Count == 1)
                return (matches[0], VerificationConfidence.Medium);
            if (matches.Count > 1)
                return (matches[0], VerificationConfidence.Low); // ambiguous
            return (null, VerificationConfidence.Low);
        }

        /// <summary>
        /// Picks the best candidate player-name line from the OCR output.
        /// Real player names are 2–4 capitalized words with no English filler,
        /// no grading/condition keywords, and no team words. Anything failing
        /// those gates is rejected so we don't pull bio paragraphs, slab labels,
        /// or OCR garbage (e.g. "Ill II Ill") into the PlayerName field.
        /// Shorter candidates outrank longer ones — real names rarely exceed 3 words.
        /// Lines that appear on both front AND back receive a confidence-boosting
        /// score bump — repeated text across sides is overwhelmingly the player
        /// name (or team) rather than bio prose.
        /// </summary>
        private static (string? player, VerificationConfidence conf) ExtractPlayerName(
            List<string> frontLines, List<string>? backLines, OcrParseContext context)
        {
            // Build a normalized lookup of back-side lines so we can detect
            // when a front-side candidate also appears on the back. Use loose
            // matching (uppercase + collapsed whitespace) since OCR rarely
            // captures the same line byte-for-byte on each side.
            HashSet<string> backNormalized = new(StringComparer.OrdinalIgnoreCase);
            if (backLines != null)
            {
                foreach (var l in backLines)
                {
                    var n = NormalizeForOverlap(l);
                    if (n.Length > 0) backNormalized.Add(n);
                }
            }

            // Catalog-known keywords that appear in card metadata (manufacturer,
            // brand). Sourced from the imported checklists via OcrParseContext —
            // empty when no checklists exist, in which case the gate is a no-op.
            var allKeywords = context.AllCatalogKeywords;

            string? best = null;
            int bestScore = -1;
            bool bestRepeated = false;

            // Walk every line from both sides — a repeated line is detectable
            // from either side, and the bump is symmetric.
            var allLines = new List<string>(frontLines);
            if (backLines != null) allLines.AddRange(backLines);

            foreach (var line in allLines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (allKeywords.Contains(trimmed)) continue;
                if (Regex.IsMatch(trimmed, @"^\d+$")) continue;
                if (trimmed.Length < 3) continue;

                var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                // Real names are 2–4 words. Single-word lines are too risky
                // (could be a team city, brand, etc.); 5+ is a sentence.
                if (words.Length < 2 || words.Length > 4) continue;

                // Reject OCR noise where most tokens are 1–2 chars
                // (e.g. "Ill II Ill Ill II II II I").
                var shortTokens = words.Count(w => w.Length <= 2);
                if (shortTokens > words.Length / 2) continue;

                // Reject sentences — any English filler word disqualifies the line.
                if (words.Any(w => BioWords.Contains(w))) continue;

                // Reject grading / condition / generic card keywords mixed in.
                if (words.Any(w => ConditionWords.Contains(w))) continue;

                // Reject team / city words sourced from imported checklists.
                if (words.Any(w => context.TeamTokens.Contains(w))) continue;

                // Each word must look like a name token: alpha with proper
                // capitalization (allowing hyphens / apostrophes for names like
                // "Smith-Njigba" / "O'Neal"), OR an all-caps initials run
                // ("C.J.", "J.P.").
                if (!words.All(IsLikelyNameWord)) continue;

                // Prefer 2-word names, then 3, then 4. Shorter wins.
                int score = 100 - (words.Length * 10);

                // Front+back overlap is the strongest signal we have without an LLM —
                // bump matches by 50 so a repeated 4-word name beats a non-repeated 2-word.
                bool repeated = backNormalized.Contains(NormalizeForOverlap(trimmed));
                if (repeated) score += 50;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = trimmed;
                    bestRepeated = repeated;
                }
            }

            if (best == null) return (null, VerificationConfidence.Low);

            // Repeated front+back match → Medium confidence, otherwise Low.
            return (best, bestRepeated ? VerificationConfidence.Medium : VerificationConfidence.Low);
        }

        private static string NormalizeForOverlap(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            return Regex.Replace(s.Trim(), @"\s+", " ").ToUpperInvariant();
        }

        /// <summary>
        /// Converts a player-name candidate to standard title casing for display
        /// and storage. OCR usually reads card-front names in all caps
        /// ("ELI MANNING", "JAXSON SMITH-NJIGBA"); we want them stored as the
        /// reader expects them written ("Eli Manning", "Jaxson Smith-Njigba").
        /// Preserves all-caps initials with periods (e.g. "C.J.", "J.P.") and
        /// title-cases each hyphen-separated segment so compound surnames stay
        /// readable. Apostrophes get the same per-segment treatment for names
        /// like "O'Neal" / "D'Angelo".
        /// </summary>
        public static string NormalizePlayerName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;
            var words = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
                words[i] = NormalizeNameWord(words[i]);
            return string.Join(' ', words);
        }

        private static readonly Regex InitialsPattern =
            new("^([A-Z]\\.)+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex AlphaNameWordPattern =
            new(@"^[A-Z][a-zA-Z'\-]*$", RegexOptions.Compiled);

        /// <summary>
        /// True when <paramref name="word"/> looks like one token of a player
        /// name — either a regular alpha word (with hyphen / apostrophe for
        /// compound surnames) or an all-caps initials run like "C.J." or
        /// "J.P." that title-casing would mangle.
        /// </summary>
        private static bool IsLikelyNameWord(string word) =>
            AlphaNameWordPattern.IsMatch(word) || InitialsPattern.IsMatch(word);

        private static string NormalizeNameWord(string word)
        {
            // Initials like "C.J." / "J.P." stay uppercase verbatim — title-casing
            // them produces nonsense like "C.j.".
            if (InitialsPattern.IsMatch(word))
                return word.ToUpperInvariant();

            // Compound surnames: title-case each hyphen-separated piece.
            return string.Join('-', word.Split('-').Select(TitleCaseSegment));
        }

        private static string TitleCaseSegment(string segment)
        {
            if (string.IsNullOrEmpty(segment)) return segment;
            // Apostrophe-bearing segments (O'Neal, D'Angelo): title-case each part
            // around the apostrophe so the prefix initial stays capitalized.
            return string.Join('\'', segment.Split('\'').Select(p =>
                p.Length switch
                {
                    0 => p,
                    1 => p.ToUpperInvariant(),
                    _ => char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant(),
                }));
        }

        /// <summary>
        /// Returns every player-name candidate that passes the shape gates, with the
        /// heuristic score and a flag indicating whether the line repeated across
        /// front and back. Callers (e.g. WindowsOcrService) can re-rank these against
        /// the checklist directory: a candidate that fuzzy-matches a real player name
        /// should win over one that has only a higher shape score, since insert /
        /// promo phrases (e.g. "Red Hot Rookies") often pass the shape gates but
        /// have no checklist equivalent.
        /// </summary>
        public static IReadOnlyList<PlayerNameCandidate> ExtractPlayerNameCandidates(
            List<string> frontLines, List<string>? backLines)
            => ExtractPlayerNameCandidates(frontLines, backLines, OcrParseContext.Empty);

        public static IReadOnlyList<PlayerNameCandidate> ExtractPlayerNameCandidates(
            List<string> frontLines, List<string>? backLines, OcrParseContext context)
        {
            HashSet<string> backNormalized = new(StringComparer.OrdinalIgnoreCase);
            if (backLines != null)
            {
                foreach (var l in backLines)
                {
                    var n = NormalizeForOverlap(l);
                    if (n.Length > 0) backNormalized.Add(n);
                }
            }

            var allLines = new List<string>(frontLines);
            if (backLines != null) allLines.AddRange(backLines);

            // Use a dict keyed by the candidate text so duplicates from front/back
            // collapse to one entry — the repeated flag captures the front+back
            // signal without inflating the candidate list.
            var seen = new Dictionary<string, PlayerNameCandidate>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in allLines)
            {
                var trimmed = line.Trim();
                if (!IsShapeValidPlayerLine(trimmed, context, out var words)) continue;

                int shapeScore = 100 - (words.Length * 10);
                bool repeated = backNormalized.Contains(NormalizeForOverlap(trimmed));
                if (repeated) shapeScore += 50;

                // Store the normalized form so callers (enhance ticker, candidate
                // display, fuzzy lookup) all see a single consistent capitalization.
                // Keying off the normalized text also collapses "ELI MANNING" and
                // "Eli Manning" — the same name printed in different cases on the
                // front and back — into one candidate with the repeated flag set.
                var normalized = NormalizePlayerName(trimmed);
                if (seen.TryGetValue(normalized, out var existing))
                {
                    if (shapeScore > existing.ShapeScore)
                        seen[normalized] = existing with { ShapeScore = shapeScore, RepeatedAcrossSides = repeated };
                    else if (repeated && !existing.RepeatedAcrossSides)
                        seen[normalized] = existing with { RepeatedAcrossSides = true };
                }
                else
                {
                    seen[normalized] = new PlayerNameCandidate(normalized, shapeScore, repeated);
                }
            }

            return seen.Values
                .OrderByDescending(c => c.ShapeScore)
                .ToList();
        }

        /// <summary>
        /// Runs the shape gates used by <see cref="ExtractPlayerNameCandidates"/> and
        /// the legacy <see cref="ExtractPlayerName"/>. Returns true when the line
        /// could plausibly be a player name; sets <paramref name="words"/> to the
        /// split tokens for callers that want to re-use them.
        /// </summary>
        private static bool IsShapeValidPlayerLine(string trimmed, OcrParseContext context, out string[] words)
        {
            words = Array.Empty<string>();
            if (string.IsNullOrEmpty(trimmed)) return false;
            if (context.AllCatalogKeywords.Contains(trimmed)) return false;
            if (Regex.IsMatch(trimmed, @"^\d+$")) return false;
            if (trimmed.Length < 3) return false;

            words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2 || words.Length > 4) return false;

            var shortTokens = words.Count(w => w.Length <= 2);
            if (shortTokens > words.Length / 2) return false;

            if (words.Any(w => BioWords.Contains(w))) return false;
            if (words.Any(w => ConditionWords.Contains(w))) return false;
            if (words.Any(w => context.TeamTokens.Contains(w))) return false;

            if (!words.All(IsLikelyNameWord)) return false;

            return true;
        }

        private static bool DetectRookie(List<string> lines)
        {
            var rookieRegex = new Regex(@"\b(RC|Rated Rookie|Rookie Card)\b", RegexOptions.IgnoreCase);
            return lines.Any(l => rookieRegex.IsMatch(l));
        }

        private static (bool isGraded, string? company, string? grade) DetectGrading(List<string> lines)
        {
            var gradingRegex = new Regex(@"\b(PSA|BGS|CGC|SGC|CSG)\b");
            var gradeValueRegex = new Regex(@"\b(\d{1,2}(?:\.\d)?|Authentic|Auth)\b");
            var joined = string.Join("\n", lines);

            var companyMatch = gradingRegex.Match(joined);
            if (!companyMatch.Success)
                return (false, null, null);

            var company = companyMatch.Value.ToUpperInvariant();
            var gradeMatch = gradeValueRegex.Match(joined);
            var grade = gradeMatch.Success ? gradeMatch.Value : null;

            return (true, company, grade);
        }
    }

    /// <summary>
    /// Shape-validated candidate produced by
    /// <see cref="OcrTextParser.ExtractPlayerNameCandidates"/>. The score is purely
    /// from the OCR-shape heuristics; downstream code can combine it with a
    /// directory match score to pick the final winner.
    /// </summary>
    public record PlayerNameCandidate(string Name, int ShapeScore, bool RepeatedAcrossSides);

    /// <summary>
    /// Catalog data the OCR parser needs to recognize fields without hardcoding
    /// the catalog. Caller (the OCR service) builds this from the user's
    /// imported checklists; the parser stays a pure heuristic helper that
    /// never carries card-domain facts of its own.
    /// </summary>
    public sealed class OcrParseContext
    {
        public static readonly OcrParseContext Empty = new();

        /// <summary>Manufacturer names from imported checklists ("Panini", "Topps").</summary>
        public IReadOnlyCollection<string> Manufacturers { get; init; } = Array.Empty<string>();

        /// <summary>Brand names from imported checklists ("Mosaic", "Prizm").</summary>
        public IReadOnlyCollection<string> Brands { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Parallel / insert / variation names sourced from the directory.
        /// Populates <c>Card.ParallelName</c> when the OCR text contains a
        /// matching token. Empty when no checklists / reference seed loaded.
        /// </summary>
        public IReadOnlyCollection<string> Parallels { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Per-word tokens of every team name from imported checklists, used
        /// to reject team-derived single words from being mistaken for a
        /// player name. Lookup is case-insensitive O(1).
        /// </summary>
        public IReadOnlySet<string> TeamTokens { get; init; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Catalog-known multi-word strings (manufacturer + brand) the parser
        /// should never treat as a player-name line. Computed lazily on first
        /// use from <see cref="Manufacturers"/> and <see cref="Brands"/>.
        /// </summary>
        public HashSet<string> AllCatalogKeywords
        {
            get
            {
                if (_allCatalogKeywords != null) return _allCatalogKeywords;
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in Manufacturers) set.Add(m);
                foreach (var b in Brands) set.Add(b);
                _allCatalogKeywords = set;
                return set;
            }
        }
        private HashSet<string>? _allCatalogKeywords;
    }
}
