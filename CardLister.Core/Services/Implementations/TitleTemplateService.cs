using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Service for generating listing titles from customizable templates.
    /// Supports platform-specific SEO optimization based on WTSCards research.
    /// </summary>
    public class TitleTemplateService
    {
        /// <summary>
        /// Generate a title from a template string.
        /// Template format: "{Year} {Brand} {Player} {Parallel} {Attributes}"
        /// Available placeholders: Year, Manufacturer, Brand, Set, Player, Team, Parallel,
        /// Attributes (RC/Auto/Relic), Serial, Grade, CardNumber
        /// </summary>
        public string GenerateTitle(Card card, string template)
        {
            if (string.IsNullOrEmpty(template))
            {
                return GenerateFallbackTitle(card);
            }

            var result = template;
            var replacements = GetReplacements(card);

            // Replace each placeholder with its value
            foreach (var (placeholder, value) in replacements)
            {
                result = Regex.Replace(result, $@"\{{{placeholder}\}}", value, RegexOptions.IgnoreCase);
            }

            // Clean up: Remove extra spaces, trim
            result = Regex.Replace(result, @"\s+", " ").Trim();

            // Fallback if template produced empty result
            if (string.IsNullOrWhiteSpace(result))
            {
                return GenerateFallbackTitle(card);
            }

            return result;
        }

        /// <summary>
        /// Get all available placeholder replacements for a card
        /// </summary>
        private Dictionary<string, string> GetReplacements(Card card)
        {
            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Year", card.Year?.ToString() ?? "" },
                { "Manufacturer", card.Manufacturer ?? "" },
                { "Brand", card.Brand ?? "" },
                { "Set", card.Brand ?? "" }, // Set and Brand are typically the same
                { "Player", card.PlayerName ?? "" },
                { "Team", card.Team ?? "" },
                { "Parallel", card.ParallelName ?? "" },
                { "Attributes", BuildAttributes(card) },
                { "Serial", card.SerialNumbered ?? "" },
                { "Grade", BuildGrade(card) },
                { "CardNumber", BuildCardNumber(card) }
            };

            return replacements;
        }

        /// <summary>
        /// Build attributes string (RC, Auto, Relic, etc.)
        /// </summary>
        private string BuildAttributes(Card card)
        {
            var parts = new List<string>();

            if (card.IsRookie) parts.Add("RC");
            if (card.IsAuto) parts.Add("Auto");
            if (card.IsRelic) parts.Add("Relic");

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Build grade string (e.g., "PSA 10", "BGS 9.5")
        /// </summary>
        private string BuildGrade(Card card)
        {
            if (card.IsGraded &&
                !string.IsNullOrEmpty(card.GradeCompany) &&
                !string.IsNullOrEmpty(card.GradeValue))
            {
                return $"{card.GradeCompany} {card.GradeValue}";
            }

            return "";
        }

        /// <summary>
        /// Build card number string (e.g., "#123")
        /// </summary>
        private string BuildCardNumber(Card card)
        {
            if (!string.IsNullOrEmpty(card.CardNumber))
            {
                return $"#{card.CardNumber}";
            }

            return "";
        }

        /// <summary>
        /// Generate a fallback title using default logic (same as original GenerateTitle)
        /// </summary>
        private string GenerateFallbackTitle(Card card)
        {
            var parts = new List<string>();

            if (card.Year.HasValue) parts.Add(card.Year.Value.ToString());
            if (!string.IsNullOrEmpty(card.Manufacturer)) parts.Add(card.Manufacturer);
            if (!string.IsNullOrEmpty(card.Brand)) parts.Add(card.Brand);
            if (!string.IsNullOrEmpty(card.PlayerName)) parts.Add(card.PlayerName);
            if (!string.IsNullOrEmpty(card.ParallelName)) parts.Add(card.ParallelName);
            if (card.IsRookie) parts.Add("RC");
            if (card.IsAuto) parts.Add("Auto");
            if (card.IsRelic) parts.Add("Relic");
            if (!string.IsNullOrEmpty(card.SerialNumbered)) parts.Add(card.SerialNumbered);
            if (card.IsGraded && !string.IsNullOrEmpty(card.GradeCompany) && !string.IsNullOrEmpty(card.GradeValue))
                parts.Add($"{card.GradeCompany} {card.GradeValue}");
            if (!string.IsNullOrEmpty(card.CardNumber)) parts.Add($"#{card.CardNumber}");

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Get default SEO-optimized template for a specific platform
        /// Based on WTSCards research on platform search algorithms
        /// </summary>
        public static string GetDefaultTemplate(Models.Enums.ExportPlatform platform)
        {
            return platform switch
            {
                Models.Enums.ExportPlatform.Whatnot =>
                    // Whatnot: Brand + Player focus (buyers search "Prizm CJ Stroud")
                    "{Year} {Brand} {Player} {Parallel} {Attributes} {Serial} {Grade} {CardNumber}",

                Models.Enums.ExportPlatform.eBay =>
                    // eBay: Comprehensive SEO with manufacturer emphasis
                    "{Year} {Manufacturer} {Brand} {Player} {Team} {Parallel} {Attributes} {Serial} {Grade} {CardNumber}",

                Models.Enums.ExportPlatform.COMC =>
                    // COMC: Similar to eBay but without manufacturer
                    "{Year} {Brand} {Player} {Team} {Parallel} {Attributes} {Serial} {Grade} {CardNumber}",

                Models.Enums.ExportPlatform.Generic or _ =>
                    // Generic: Balanced approach
                    "{Year} {Brand} {Player} {Parallel} {Attributes} {Serial} {Grade} {CardNumber}"
            };
        }

        /// <summary>
        /// Get help text explaining available placeholders
        /// </summary>
        public static string GetPlaceholderHelpText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Available placeholders:");
            sb.AppendLine("  {Year} - Card year (e.g., 2023)");
            sb.AppendLine("  {Manufacturer} - Card manufacturer (e.g., Panini)");
            sb.AppendLine("  {Brand} - Card brand/set (e.g., Prizm, Bowman)");
            sb.AppendLine("  {Set} - Alias for Brand");
            sb.AppendLine("  {Player} - Player name (e.g., CJ Stroud)");
            sb.AppendLine("  {Team} - Team name (e.g., Houston Texans)");
            sb.AppendLine("  {Parallel} - Parallel name (e.g., Silver, Gold)");
            sb.AppendLine("  {Attributes} - RC, Auto, Relic (e.g., RC Auto)");
            sb.AppendLine("  {Serial} - Serial number (e.g., /99, /25)");
            sb.AppendLine("  {Grade} - Grading info (e.g., PSA 10, BGS 9.5)");
            sb.AppendLine("  {CardNumber} - Card number with # (e.g., #123)");
            sb.AppendLine();
            sb.AppendLine("Placeholders with no value are automatically removed.");
            sb.AppendLine("Extra spaces are cleaned up automatically.");
            return sb.ToString();
        }

        /// <summary>
        /// Validate a template string
        /// </summary>
        public static (bool IsValid, string? ErrorMessage) ValidateTemplate(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return (false, "Template cannot be empty");
            }

            // Check for valid placeholders
            var validPlaceholders = new[]
            {
                "Year", "Manufacturer", "Brand", "Set", "Player", "Team",
                "Parallel", "Attributes", "Serial", "Grade", "CardNumber"
            };

            var matches = Regex.Matches(template, @"\{([^}]+)\}");
            foreach (Match match in matches)
            {
                var placeholder = match.Groups[1].Value;
                var isValid = false;

                foreach (var valid in validPlaceholders)
                {
                    if (string.Equals(placeholder, valid, StringComparison.OrdinalIgnoreCase))
                    {
                        isValid = true;
                        break;
                    }
                }

                if (!isValid)
                {
                    return (false, $"Invalid placeholder: {{{placeholder}}}");
                }
            }

            return (true, null);
        }
    }
}
