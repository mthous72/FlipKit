using FlipKit.Core.Services;
using FlipKit.Core.Services.Scanning;

namespace FlipKit.Desktop.ViewModels
{
    /// <summary>
    /// Dropdown entry for a scanner model. Either the special "Auto" entry (which
    /// triggers free-model rotation with paid-model consent) or a specific model
    /// from the live OpenRouter catalog.
    /// </summary>
    public sealed class ModelOption
    {
        // Aliased to the Core-layer constant so all three layers (Core enricher,
        // Desktop dropdown, Web form) point at the same string. const-aliasing a
        // const is fine — the compiler folds it into the same literal.
        public const string AutoValue = OpenRouterModelDefaults.AutoModelValue;

        public string Label { get; }
        public string Value { get; }
        public bool IsAuto { get; }
        public OpenRouterModel? Model { get; }

        // === Quality scoreboard data (Phase 5 — model accuracy ranking) ===
        // Populated when callers attach scoreboard signal (FromCatalog with a
        // ModelQuality argument). Drives the inline pill in pickers and the
        // sort-by-score that floats better-performing models to the top.
        public decimal? QualityScore { get; }
        public int SampleCount { get; }
        public string ConfidenceLabel { get; }

        // Display-ready quality pill text. Three buckets:
        //   "Untested"        — < MinSamplesForScore samples
        //   "Tentative (n)"   — 3-9 samples
        //   "92%"             — 10+ samples (rounded)
        public string QualityScoreDisplay => QualityScore.HasValue
            ? $"{System.Math.Round(QualityScore.Value)}%"
            : ConfidenceLabel;

        // Hide the pill on the Auto sentinel — it's a routing strategy, not
        // a model. Stale entries also have nothing to show.
        public bool ShowQualityPill => !IsAuto && (QualityScore.HasValue || ConfidenceLabel.Length > 0);

        // Sort key — null score (untested) sorts to the bottom, healthy models
        // bubble to the top. Same comparator works for free + paid lists.
        public decimal QualitySortKey => QualityScore ?? -1m;

        private ModelOption(string label, string value, bool isAuto, OpenRouterModel? model,
            decimal? qualityScore, int sampleCount, string confidenceLabel)
        {
            Label = label;
            Value = value;
            IsAuto = isAuto;
            Model = model;
            QualityScore = qualityScore;
            SampleCount = sampleCount;
            ConfidenceLabel = confidenceLabel;
        }

        public static ModelOption Auto() =>
            new("Auto: try free models first, ask before paid", AutoValue, true, null,
                qualityScore: null, sampleCount: 0, confidenceLabel: string.Empty);

        public static ModelOption FromCatalog(OpenRouterModel m) =>
            new(ModelCostFormatter.FormatDropdownLabel(m), m.Id, false, m,
                qualityScore: null, sampleCount: 0, confidenceLabel: string.Empty);

        // Overload that attaches scoreboard data so the dropdown can display
        // and sort by quality. Pass null when no record exists for the model
        // — the entry shows "Untested" and sorts after scored entries.
        public static ModelOption FromCatalog(OpenRouterModel m, ModelQuality? quality) =>
            new(ModelCostFormatter.FormatDropdownLabel(m), m.Id, false, m,
                qualityScore: quality?.Score,
                sampleCount: quality?.SampleCount ?? 0,
                confidenceLabel: quality?.ConfidenceLabel ?? "Untested");

        /// <summary>
        /// Wraps a saved model id that we couldn't find in the catalog (e.g. a stale
        /// settings value). Lets the dropdown still display it so the user can see
        /// what's saved, even if it's no longer offered by OpenRouter.
        /// </summary>
        public static ModelOption Stale(string modelId) =>
            new($"{modelId}  •  unavailable in current catalog", modelId, false, null,
                qualityScore: null, sampleCount: 0, confidenceLabel: string.Empty);

        public override string ToString() => Label;
    }
}
