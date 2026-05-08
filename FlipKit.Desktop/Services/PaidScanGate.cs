using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Scanning;
using Microsoft.Extensions.Logging;

namespace FlipKit.Desktop.Services
{
    /// <summary>
    /// Gates any scan call that's about to use a paid OpenRouter model behind the
    /// model picker. Free-model calls pass through unchanged. The point is a single
    /// chokepoint — every Enhance / Scan flow routes through here before calling
    /// <c>IScannerService.ScanCardAsync</c>, so a saved <c>settings.DefaultModel</c>
    /// that resolves to paid can never silently ship a request the user wasn't
    /// asked about. (Real $2.50/4-card billing surprise that triggered this gate.)
    /// </summary>
    public interface IPaidScanGate
    {
        /// <summary>
        /// Returns the model id the caller should use, or <c>null</c> if the user
        /// cancelled. Free models pass through; paid models trigger the picker.
        /// </summary>
        Task<string?> GateAsync(string resolvedModelId, string contextMessage);
    }

    public sealed class PaidScanGate : IPaidScanGate
    {
        private readonly IOpenRouterModelCatalog _catalog;
        private readonly IPaidModelConsentService _consent;
        private readonly ILogger<PaidScanGate>? _logger;

        public PaidScanGate(
            IOpenRouterModelCatalog catalog,
            IPaidModelConsentService consent,
            ILogger<PaidScanGate>? logger = null)
        {
            _catalog = catalog;
            _consent = consent;
            _logger = logger;
        }

        public async Task<string?> GateAsync(string resolvedModelId, string contextMessage)
        {
            // The UI sentinel "auto" must never reach the wire — but it should
            // already have been folded by ResolveModelId at the callsite. If it
            // somehow leaks here, treat it as "ask for paid model" since auto-router
            // routes to whoever-OpenRouter-picks (typically a paid premium model).
            if (string.IsNullOrWhiteSpace(resolvedModelId)
                || resolvedModelId == OpenRouterModelDefaults.AutoModelValue)
            {
                _logger?.LogWarning(
                    "PaidScanGate received empty/auto model id — falling back to picker. Callers should pre-resolve via OpenRouterModelDefaults.ResolveModelId.");
                return await PromptPickerAsync(contextMessage, suggestedId: null);
            }

            ModelCatalog catalog;
            try
            {
                catalog = await _catalog.GetAsync();
            }
            catch
            {
                // Without the catalog we can't classify free vs paid. Pass through —
                // the caller's intent is whatever id they resolved. The defensive
                // guard inside the scanner handles "auto" leaks separately.
                return resolvedModelId;
            }

            // Free passes through silently — that's the whole point.
            if (catalog.FreeVisionModels.Any(m => m.Id == resolvedModelId))
                return resolvedModelId;

            // Anything else is treated as paid, including ids not in the catalog at
            // all (stale settings, deprecated models). Erring on the side of asking.
            return await PromptPickerAsync(contextMessage, resolvedModelId);
        }

        private async Task<string?> PromptPickerAsync(string contextMessage, string? suggestedId)
        {
            ModelCatalog catalog;
            try { catalog = await _catalog.GetAsync(); }
            catch { return null; } // can't show a picker without options

            if (catalog.PaidVisionModels.Count == 0)
            {
                _logger?.LogWarning("PaidScanGate: no paid models available to offer; cancelling.");
                return null;
            }

            var suggested = (suggestedId != null
                ? catalog.PaidVisionModels.FirstOrDefault(m => m.Id == suggestedId)
                : null) ?? catalog.PaidVisionModels[0];

            var chosen = await _consent.AskAsync(catalog.PaidVisionModels, suggested, contextMessage);
            return chosen?.Id;
        }
    }
}
