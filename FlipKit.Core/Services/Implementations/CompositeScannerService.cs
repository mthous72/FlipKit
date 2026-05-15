using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services.Implementations;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Tries CardSight first (free 750/mo quota, purpose-built sports-card recognition),
    /// then falls back to OpenRouter LLM on miss / low confidence / error.
    /// Ximilar is no longer in the active chain (code retained in repo).
    /// </summary>
    public class CompositeScannerService : IScannerService
    {
        private readonly CardsightScannerService _cardsightService;
        private readonly OpenRouterScannerService _openRouterService;
        private readonly ILogger<CompositeScannerService> _logger;

        public CompositeScannerService(
            CardsightScannerService cardsightService,
            OpenRouterScannerService openRouterService,
            ILogger<CompositeScannerService> logger)
        {
            _cardsightService = cardsightService;
            _openRouterService = openRouterService;
            _logger = logger;
        }

        public async Task<ScanResult> ScanCardAsync(
            string imagePath,
            string? backImagePath = null,
            string model = OpenRouterModelDefaults.DefaultFreeModelId,
            XimilarScanMode ximilarMode = XimilarScanMode.Standard,
            ScanDepth scanDepth = ScanDepth.Standard,
            OcrHint? ocrHint = null,
            CancellationToken ct = default)
        {
            if (_cardsightService.IsConfigured)
            {
                try
                {
                    _logger.LogInformation("Attempting CardSight recognition...");
                    var cardsightResult = await _cardsightService.ScanCardAsync(imagePath, backImagePath, ct);
                    _logger.LogInformation("CardSight identified card: {Player} ({Year} {Set})",
                        cardsightResult.Card.PlayerName, cardsightResult.Card.Year, cardsightResult.Card.SetName);
                    return cardsightResult;
                }
                catch (CardsightException ex)
                {
                    _logger.LogInformation("CardSight fallback to OpenRouter ({Reason}): {Message}", ex.Reason, ex.Message);
                    // fall through
                }
            }
            else
            {
                _logger.LogDebug("CardSight not configured, using OpenRouter directly");
            }

            return await _openRouterService.ScanCardAsync(imagePath, backImagePath, model, scanDepth: scanDepth, ocrHint: ocrHint, ct: ct);
        }

        public async Task<string> SendCustomPromptAsync(string imagePath, string prompt, string? backImagePath = null, string model = OpenRouterModelDefaults.DefaultFreeModelId)
        {
            // Custom prompts always go to OpenRouter (CardSight only does card identification).
            return await _openRouterService.SendCustomPromptAsync(imagePath, prompt, backImagePath, model);
        }
    }
}
