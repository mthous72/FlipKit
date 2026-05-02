using System.Threading.Tasks;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Asks the user whether to proceed with a paid OpenRouter model when all free
    /// models have failed. The Desktop implementation shows a modal dialog; a Web
    /// implementation could redirect the user to a confirmation page (deferred).
    /// </summary>
    public interface IPaidModelConsentService
    {
        /// <summary>
        /// Returns true if the user agreed to use the proposed paid model. Returning
        /// false means "cancel the scan cleanly" — callers should NOT raise an error.
        /// </summary>
        Task<bool> AskAsync(OpenRouterModel proposedModel, string contextMessage);
    }
}
