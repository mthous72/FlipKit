using System.Collections.Generic;
using System.Threading.Tasks;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Asks the user to pick a paid OpenRouter model before any paid scan is dispatched.
    /// This is a hard billing-safety gate: every callsite that could end up putting a
    /// paid model on the wire (free-rotation fallback, an explicitly-picked paid
    /// dropdown choice, a saved settings.DefaultModel that resolves paid) must route
    /// through this service so the user can confirm or change the model.
    /// </summary>
    public interface IPaidModelConsentService
    {
        /// <summary>
        /// Shows a picker over <paramref name="availableModels"/> with
        /// <paramref name="suggestedModel"/> selected by default. Returns the chosen
        /// <see cref="OpenRouterModel"/> (which may differ from the suggestion if the
        /// user picks something else from the list), or <c>null</c> if the user
        /// cancels. Cancelling means "stop the scan cleanly" — callers should NOT
        /// raise an error.
        /// </summary>
        /// <param name="availableModels">Full paid-model catalog the user can pick from.
        /// Should be sorted cheapest-first by the catalog layer; the dialog renders
        /// them in supplied order.</param>
        /// <param name="suggestedModel">The model the system would have used by
        /// default (e.g. the resolved settings value or the cheapestPaid). Used as
        /// the picker's initial selection so a confirming user can just hit OK.</param>
        /// <param name="contextMessage">Free-form text shown above the picker
        /// describing why this prompt fired (e.g. "Free models exhausted on 4 cards").</param>
        Task<OpenRouterModel?> AskAsync(
            IReadOnlyList<OpenRouterModel> availableModels,
            OpenRouterModel suggestedModel,
            string contextMessage);
    }
}
