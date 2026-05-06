using System.Threading.Tasks;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Shown once before the first AI scan to inform the user that card images
    /// will be sent to OpenRouter (and optionally Ximilar). Callers check
    /// AppSettings.AiScanConsentGiven first and only call AskAsync when consent
    /// has not yet been recorded.
    /// </summary>
    public interface IAiScanConsentService
    {
        /// <summary>
        /// Shows the consent prompt. Returns (Proceed=true, Remember=true/false)
        /// when the user agrees. Returns (Proceed=false, Remember=false) when the
        /// user cancels — callers must abort the scan without raising an error.
        /// </summary>
        Task<AiScanConsentResult> AskAsync();
    }

    public record AiScanConsentResult(bool Proceed, bool Remember);
}
