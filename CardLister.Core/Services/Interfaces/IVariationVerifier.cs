using System.Threading.Tasks;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    public interface IVariationVerifier
    {
        Task<VerificationResult> VerifyCardAsync(ScanResult scanResult, string imagePath);
        Task<SetChecklist?> GetChecklistAsync(string manufacturer, string brand, int year, string? sport = null);
        bool NeedsConfirmationPass(VerificationResult result);
        Task<VerificationResult> RunConfirmationPassAsync(ScanResult scanResult, VerificationResult verification, string imagePath);
    }
}
