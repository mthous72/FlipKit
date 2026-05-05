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

        private ModelOption(string label, string value, bool isAuto, OpenRouterModel? model)
        {
            Label = label;
            Value = value;
            IsAuto = isAuto;
            Model = model;
        }

        public static ModelOption Auto() =>
            new("Auto: try free models first, ask before paid", AutoValue, true, null);

        public static ModelOption FromCatalog(OpenRouterModel m) =>
            new(ModelCostFormatter.FormatDropdownLabel(m), m.Id, false, m);

        /// <summary>
        /// Wraps a saved model id that we couldn't find in the catalog (e.g. a stale
        /// settings value). Lets the dropdown still display it so the user can see
        /// what's saved, even if it's no longer offered by OpenRouter.
        /// </summary>
        public static ModelOption Stale(string modelId) =>
            new($"{modelId}  •  unavailable in current catalog", modelId, false, null);

        public override string ToString() => Label;
    }
}
