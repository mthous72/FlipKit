using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Desktop.Services
{
    public static class OcrTextParser
    {
        private static readonly string[] KnownManufacturers =
        {
            "Upper Deck", "Topps", "Panini", "Leaf", "Fleer", "Donruss",
            "Bowman", "Score", "Pacific", "Skybox",
        };

        private static readonly string[] KnownBrands =
        {
            "Stadium Club", "Prizm", "Chrome", "Heritage", "Finest", "Mosaic",
            "Select", "Optic", "Contenders", "Phoenix", "Inception", "Hoops",
            "Illusions", "Immaculate", "National Treasures", "Topps Chrome",
            "Bowman Draft", "Bowman Chrome",
        };

        private static readonly HashSet<string> AllKeywords = new(StringComparer.OrdinalIgnoreCase);

        static OcrTextParser()
        {
            foreach (var k in KnownManufacturers) AllKeywords.Add(k);
            foreach (var k in KnownBrands) AllKeywords.Add(k);
        }

        public static (Card card, List<FieldConfidence> confidences) Parse(List<string> ocrLines)
        {
            var card = new Card
            {
                DataSource = CardDataSource.Ocr,
                Status = CardStatus.Draft,
            };
            var confidences = new List<FieldConfidence>();

            var (year, yearConf) = ExtractYear(ocrLines);
            if (year.HasValue)
            {
                card.Year = year;
                confidences.Add(new FieldConfidence { FieldName = "year", Confidence = yearConf, Reason = "OCR pattern match" });
            }

            var (cardNum, cardNumConf) = ExtractCardNumber(ocrLines);
            if (!string.IsNullOrEmpty(cardNum))
            {
                card.CardNumber = cardNum;
                confidences.Add(new FieldConfidence { FieldName = "card_number", Confidence = cardNumConf, Reason = "OCR pattern match" });
            }

            var (serialNum, serialConf) = ExtractSerialNumber(ocrLines);
            if (!string.IsNullOrEmpty(serialNum))
            {
                card.SerialNumbered = serialNum;
                confidences.Add(new FieldConfidence { FieldName = "serial_numbered", Confidence = serialConf, Reason = "OCR pattern match" });
            }

            var (mfr, mfrConf) = ExtractManufacturer(ocrLines);
            if (!string.IsNullOrEmpty(mfr))
            {
                card.Manufacturer = mfr;
                confidences.Add(new FieldConfidence { FieldName = "manufacturer", Confidence = mfrConf, Reason = "OCR keyword match" });
            }

            var (brand, brandConf) = ExtractBrand(ocrLines);
            if (!string.IsNullOrEmpty(brand))
            {
                card.Brand = brand;
                confidences.Add(new FieldConfidence { FieldName = "brand", Confidence = brandConf, Reason = "OCR keyword match" });
            }

            var (player, playerConf) = ExtractPlayerName(ocrLines);
            if (!string.IsNullOrEmpty(player))
            {
                card.PlayerName = player;
                confidences.Add(new FieldConfidence { FieldName = "player_name", Confidence = playerConf, Reason = "OCR text heuristic" });
            }

            var (isGraded, company, grade) = DetectGrading(ocrLines);
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

            if (DetectRookie(ocrLines))
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

        private static (string? mfr, VerificationConfidence conf) ExtractManufacturer(List<string> lines)
        {
            // Check multi-word first (longest match wins)
            var sorted = KnownManufacturers.OrderByDescending(k => k.Length);
            var joined = string.Join("\n", lines);
            foreach (var mfr in sorted)
            {
                if (Regex.IsMatch(joined, @"\b" + Regex.Escape(mfr) + @"\b", RegexOptions.IgnoreCase))
                    return (mfr, VerificationConfidence.Medium);
            }
            return (null, VerificationConfidence.Low);
        }

        private static (string? brand, VerificationConfidence conf) ExtractBrand(List<string> lines)
        {
            var sorted = KnownBrands.OrderByDescending(k => k.Length);
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

        private static (string? player, VerificationConfidence conf) ExtractPlayerName(List<string> lines)
        {
            // Find the longest run of title-case words that aren't known keywords
            string? best = null;
            int bestLen = 0;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                // Skip lines that are entirely known keywords
                if (AllKeywords.Contains(trimmed)) continue;

                // Skip lines that look like numbers or short codes
                if (Regex.IsMatch(trimmed, @"^\d+$")) continue;
                if (trimmed.Length < 3) continue;

                // Check if it looks like a name: starts with capital, contains only letters/spaces/hyphens/apostrophes
                if (Regex.IsMatch(trimmed, @"^[A-Z][a-zA-Z'\-\s]+$") && !AllKeywords.Contains(trimmed))
                {
                    var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length >= 2 && trimmed.Length > bestLen)
                    {
                        bestLen = trimmed.Length;
                        best = trimmed;
                    }
                }
            }

            if (best != null)
                return (best, VerificationConfidence.Low);

            return (null, VerificationConfidence.Low);
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
}
