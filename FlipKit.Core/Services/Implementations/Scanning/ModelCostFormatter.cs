using System.Globalization;

namespace FlipKit.Core.Services.Scanning
{
    /// <summary>
    /// Renders an <see cref="OpenRouterModel"/> as a one-line dropdown label with
    /// cost annotations. Free models get a "FREE" tag; paid models get the prompt
    /// and completion prices per 1M tokens, plus the per-image cost when present.
    /// </summary>
    public static class ModelCostFormatter
    {
        public static string FormatDropdownLabel(OpenRouterModel m)
        {
            if (m.IsFree)
                return $"{m.DisplayName}  •  FREE";

            var prompt = FormatPrice(m.PromptPricePerMillion);
            var completion = FormatPrice(m.CompletionPricePerMillion);
            var imgSuffix = m.ImagePricePerImage is { } img && img > 0m
                ? $"  •  {FormatPrice(img * 1_000_000m).TrimStart('$')}/Mimg"
                : string.Empty;
            return $"{m.DisplayName}  •  ${prompt} in / ${completion} out per 1M{imgSuffix}";
        }

        public static string FormatPrice(decimal usd)
        {
            // Use up to 4 significant digits so $0.075 reads as "0.08", $0.0125 as "0.0125".
            // For prices ≥ $1, use 2 decimals (currency style).
            return usd >= 1m
                ? usd.ToString("0.##", CultureInfo.InvariantCulture)
                : usd.ToString("0.####", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Inline cost summary for the paid-model consent dialog: e.g.
        /// "$3 per 1M prompt tokens, $15 per 1M completion tokens, $0.0048 per image".
        /// </summary>
        public static string FormatConsentSummary(OpenRouterModel m)
        {
            if (m.IsFree) return "Free.";

            var parts = new System.Collections.Generic.List<string>
            {
                $"${FormatPrice(m.PromptPricePerMillion)} per 1M prompt tokens",
                $"${FormatPrice(m.CompletionPricePerMillion)} per 1M completion tokens",
            };
            if (m.ImagePricePerImage is { } img && img > 0m)
                parts.Add($"${FormatPrice(img)} per image");
            return string.Join(", ", parts) + ".";
        }
    }
}
