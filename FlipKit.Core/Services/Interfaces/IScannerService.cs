using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Services
{
    public interface IScannerService
    {
        Task<ScanResult> ScanCardAsync(
            string imagePath,
            string? backImagePath = null,
            string model = OpenRouterModelDefaults.DefaultFreeModelId,
            ScanDepth scanDepth = ScanDepth.Standard,
            OcrHint? ocrHint = null,
            CancellationToken ct = default);

        Task<string> SendCustomPromptAsync(
            string imagePath,
            string prompt,
            string? backImagePath = null,
            string model = OpenRouterModelDefaults.DefaultFreeModelId);
    }
}
