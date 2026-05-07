using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Composite scanner that tries Ximilar first (if configured) for cost efficiency,
    /// then falls back to OpenRouter LLM if Ximilar doesn't find a match.
    /// </summary>
    public class CompositeScannerService : IScannerService
    {
        private readonly IXimilarService _ximilarService;
        private readonly OpenRouterScannerService _openRouterService;
        private readonly ILogger<CompositeScannerService> _logger;

        public CompositeScannerService(
            IXimilarService ximilarService,
            OpenRouterScannerService openRouterService,
            ILogger<CompositeScannerService> logger)
        {
            _ximilarService = ximilarService;
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
            // Check if Ximilar should be used based on mode
            var useXimilar = ximilarMode != XimilarScanMode.Disabled && _ximilarService.IsConfigured;
            var useMagicAi = ximilarMode == XimilarScanMode.Magic;

            // Try Ximilar first if enabled and configured (cheaper, uses existing card database)
            if (useXimilar)
            {
                _logger.LogInformation("Attempting Ximilar recognition (mode: {Mode}, magic_ai: {MagicAi})...",
                    ximilarMode, useMagicAi);

                var ximilarResult = await _ximilarService.RecognizeCardAsync(imagePath, useMagicAi);

                if (ximilarResult?.Success == true && ximilarResult.Card != null && ximilarResult.Confidence >= 0.8)
                {
                    _logger.LogInformation("Ximilar found high-confidence match ({Confidence:P0}), using Ximilar result",
                        ximilarResult.Confidence);

                    // Set back image if provided
                    if (!string.IsNullOrEmpty(backImagePath))
                        ximilarResult.Card.ImagePathBack = backImagePath;

                    ximilarResult.Card.DataSource = CardDataSource.Ai;

                    return new ScanResult
                    {
                        Card = ximilarResult.Card,
                        VisualCues = null,
                        AllVisibleText = new List<string>(),
                        Confidences = new List<FieldConfidence>
                        {
                            new() { FieldName = "ximilar_match", Value = "true", Confidence = VerificationConfidence.High, Reason = $"Ximilar match score: {ximilarResult.Confidence:P0}" }
                        }
                    };
                }

                if (ximilarResult?.Success == true && ximilarResult.Card != null)
                {
                    // Medium confidence - still fall back to LLM but log the Ximilar result
                    _logger.LogInformation("Ximilar found match but confidence too low ({Confidence:P0}), falling back to LLM",
                        ximilarResult.Confidence);
                }
                else
                {
                    _logger.LogInformation("Ximilar did not find match, falling back to LLM");
                }
            }
            else
            {
                if (ximilarMode == XimilarScanMode.Disabled)
                    _logger.LogInformation("Ximilar disabled by user, using OpenRouter directly");
                else
                    _logger.LogDebug("Ximilar not configured, using OpenRouter directly");
            }

            // Fall back to OpenRouter LLM
            _logger.LogInformation("Using OpenRouter LLM for card recognition...");
            return await _openRouterService.ScanCardAsync(imagePath, backImagePath, model, scanDepth: scanDepth, ocrHint: ocrHint, ct: ct);
        }

        public async Task<string> SendCustomPromptAsync(string imagePath, string prompt, string? backImagePath = null, string model = OpenRouterModelDefaults.DefaultFreeModelId)
        {
            // Custom prompts always go to OpenRouter (Ximilar doesn't support arbitrary prompts)
            return await _openRouterService.SendCustomPromptAsync(imagePath, prompt, backImagePath, model);
        }
    }
}
