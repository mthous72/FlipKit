using Microsoft.AspNetCore.Mvc;
using FlipKit.Core.Services;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Web.Models;
using System.Text.Json;

namespace FlipKit.Web.Controllers
{
    public class ScanController : Controller
    {
        private readonly IScannerService _scannerService;
        private readonly ICardRepository _cardRepository;
        private readonly IVariationVerifier _variationVerifier;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<ScanController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IPricerService _pricerService;

        public ScanController(
            IScannerService scannerService,
            ICardRepository cardRepository,
            IVariationVerifier variationVerifier,
            ISettingsService settingsService,
            ILogger<ScanController> logger,
            IWebHostEnvironment environment,
            IPricerService pricerService)
        {
            _scannerService = scannerService;
            _cardRepository = cardRepository;
            _variationVerifier = variationVerifier;
            _settingsService = settingsService;
            _logger = logger;
            _environment = environment;
            _pricerService = pricerService;
        }

        // GET: Scan
        public IActionResult Index(string? mode)
        {
            // Store mode in session
            if (!string.IsNullOrEmpty(mode))
            {
                HttpContext.Session.SetString("ScanMode", mode);
            }

            var scanMode = HttpContext.Session.GetString("ScanMode") ?? "selling";

            // Get Ximilar mode from session (persists user's selection)
            var ximilarModeStr = HttpContext.Session.GetString("XimilarScanMode") ?? "Standard";
            var ximilarMode = Enum.TryParse<XimilarScanMode>(ximilarModeStr, out var parsedMode)
                ? parsedMode
                : XimilarScanMode.Standard;

            var viewModel = new ScanUploadViewModel
            {
                ScanMode = scanMode,
                XimilarMode = ximilarMode
            };

            return View(viewModel);
        }

        // POST: Scan/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile? frontImage, IFormFile? backImage, string? selectedModel, string? ximilarMode)
        {
            if (frontImage == null || frontImage.Length == 0)
            {
                TempData["ErrorMessage"] = "Please upload at least a front image of the card.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Parse and store Ximilar mode in session (persists for future scans)
                var parsedXimilarMode = XimilarScanMode.Standard;
                if (!string.IsNullOrEmpty(ximilarMode) && Enum.TryParse<XimilarScanMode>(ximilarMode, out var mode))
                {
                    parsedXimilarMode = mode;
                    HttpContext.Session.SetString("XimilarScanMode", ximilarMode);
                }

                // Save uploaded images to temp directory
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
                Directory.CreateDirectory(uploadsPath);

                var frontImagePath = Path.Combine(uploadsPath, $"{Guid.NewGuid()}_{frontImage.FileName}");
                using (var stream = new FileStream(frontImagePath, FileMode.Create))
                {
                    await frontImage.CopyToAsync(stream);
                }

                string? backImagePath = null;
                if (backImage != null && backImage.Length > 0)
                {
                    backImagePath = Path.Combine(uploadsPath, $"{Guid.NewGuid()}_{backImage.FileName}");
                    using (var stream = new FileStream(backImagePath, FileMode.Create))
                    {
                        await backImage.CopyToAsync(stream);
                    }
                }

                // Get settings for model selection
                var settings = _settingsService.Load();
                var model = selectedModel ?? settings.DefaultModel ?? "nvidia/nemotron-nano-12b-v2-vl:free";

                // Scan the card using AI with the selected Ximilar mode
                _logger.LogInformation("Scanning card with model {Model}, Ximilar mode: {XimilarMode}", model, parsedXimilarMode);
                var scanResult = await _scannerService.ScanCardAsync(frontImagePath, backImagePath, model, parsedXimilarMode);

                if (scanResult == null)
                {
                    TempData["ErrorMessage"] = "AI scan failed. Please check your API key and try again.";
                    CleanupTempFiles(frontImagePath, backImagePath);
                    return RedirectToAction(nameof(Index));
                }

                // Run variation verification (optional)
                VerificationResult? verificationResult = null;
                try
                {
                    verificationResult = await _variationVerifier.VerifyCardAsync(scanResult, frontImagePath);
                    _logger.LogInformation("Verification completed with confidence: {Confidence}",
                        verificationResult?.OverallConfidence ?? VerificationConfidence.Low);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Verification failed, continuing without verification");
                }

                // Store scan results in TempData for the Results page
                var scanViewModel = new ScanResultViewModel
                {
                    ScannedCard = scanResult.Card,
                    FrontImagePath = frontImagePath,
                    BackImagePath = backImagePath,
                    VerificationResult = verificationResult
                };

                TempData["ScanResult"] = JsonSerializer.Serialize(scanViewModel);
                return RedirectToAction(nameof(Results));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during card scan");
                TempData["ErrorMessage"] = $"Scan failed: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Scan/Results
        public IActionResult Results()
        {
            var scanResultJson = TempData["ScanResult"] as string;
            if (string.IsNullOrEmpty(scanResultJson))
            {
                return RedirectToAction(nameof(Index));
            }

            var scanViewModel = JsonSerializer.Deserialize<ScanResultViewModel>(scanResultJson);
            if (scanViewModel == null || scanViewModel.ScannedCard == null)
            {
                return RedirectToAction(nameof(Index));
            }

            // Keep in TempData for potential Save action
            TempData.Keep("ScanResult");

            return View(scanViewModel);
        }

        // POST: Scan/Save
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(
            decimal? estimatedValue,
            decimal? listingPrice,
            decimal? costBasis)
        {
            var scanResultJson = TempData["ScanResult"] as string;
            if (string.IsNullOrEmpty(scanResultJson))
            {
                TempData["ErrorMessage"] = "Scan results expired. Please scan again.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var scanViewModel = JsonSerializer.Deserialize<ScanResultViewModel>(scanResultJson);
                if (scanViewModel?.ScannedCard == null)
                {
                    TempData["ErrorMessage"] = "Invalid scan data.";
                    return RedirectToAction(nameof(Index));
                }

                var card = scanViewModel.ScannedCard;

                // Set pricing data if provided (from Research page)
                if (estimatedValue.HasValue)
                    card.EstimatedValue = estimatedValue.Value;
                if (listingPrice.HasValue)
                    card.ListingPrice = listingPrice.Value;
                if (costBasis.HasValue)
                    card.CostBasis = costBasis.Value;

                // Ensure required fields have defaults (JSON deserialization may set them to null)
                if (string.IsNullOrEmpty(card.VariationType))
                    card.VariationType = "Base";
                if (string.IsNullOrEmpty(card.Condition))
                    card.Condition = "Near Mint";
                if (string.IsNullOrEmpty(card.PlayerName))
                    card.PlayerName = "Unknown";

                // Set default status
                card.Status = CardStatus.Draft;
                card.CreatedAt = DateTime.UtcNow;
                card.UpdatedAt = DateTime.UtcNow;

                // Store image paths
                card.ImagePathFront = scanViewModel.FrontImagePath;
                card.ImagePathBack = scanViewModel.BackImagePath;

                // Save to database
                await _cardRepository.InsertCardAsync(card);

                _logger.LogInformation("Card saved: {PlayerName} - {Year} {Brand}",
                    card.PlayerName, card.Year, card.Brand);

                TempData["SuccessMessage"] = $"Card '{card.PlayerName}' saved successfully!";
                return RedirectToAction("Index", "Inventory");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving scanned card");
                TempData["ErrorMessage"] = $"Failed to save card: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Scan/ResearchComps
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResearchComps(Card scannedCard, string? frontImagePath, string? backImagePath, string? scanMode)
        {
            if (scannedCard == null)
            {
                TempData["ErrorMessage"] = "No card data provided.";
                return RedirectToAction(nameof(Index));
            }

            // Build comp research URLs from temp card data (no database involved)
            var terapeakUrl = _pricerService.BuildTerapeakUrl(scannedCard);
            var ebaySoldUrl = _pricerService.BuildEbaySoldUrl(scannedCard);

            // Create research view model
            var viewModel = new ScanResearchViewModel
            {
                ScannedCard = scannedCard,
                FrontImagePath = frontImagePath,
                BackImagePath = backImagePath,
                TerapeakUrl = terapeakUrl,
                EbaySoldUrl = ebaySoldUrl,
                ScanMode = scanMode ?? "buying"
            };

            // Store in TempData for back navigation and potential save
            TempData["ScanResult"] = JsonSerializer.Serialize(new ScanResultViewModel
            {
                ScannedCard = scannedCard,
                FrontImagePath = frontImagePath,
                BackImagePath = backImagePath,
                ScanMode = scanMode ?? "buying"
            });
            TempData.Keep("ScanResult");

            _logger.LogInformation("Research comps for {PlayerName} (mode: {Mode})",
                scannedCard.PlayerName, scanMode);

            return View("Research", viewModel);
        }

        // POST: Scan/SaveAndResearch
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAndResearch()
        {
            var scanResultJson = TempData["ScanResult"] as string;
            if (string.IsNullOrEmpty(scanResultJson))
            {
                TempData["ErrorMessage"] = "Scan results expired. Please scan again.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var scanViewModel = JsonSerializer.Deserialize<ScanResultViewModel>(scanResultJson);
                if (scanViewModel?.ScannedCard == null)
                {
                    TempData["ErrorMessage"] = "Invalid scan data.";
                    return RedirectToAction(nameof(Index));
                }

                var card = scanViewModel.ScannedCard;

                // Ensure required fields have defaults (JSON deserialization may set them to null)
                if (string.IsNullOrEmpty(card.VariationType))
                    card.VariationType = "Base";
                if (string.IsNullOrEmpty(card.Condition))
                    card.Condition = "Near Mint";
                if (string.IsNullOrEmpty(card.PlayerName))
                    card.PlayerName = "Unknown";

                // Set defaults and save
                card.Status = CardStatus.Draft;
                card.CreatedAt = DateTime.UtcNow;
                card.UpdatedAt = DateTime.UtcNow;
                card.ImagePathFront = scanViewModel.FrontImagePath;
                card.ImagePathBack = scanViewModel.BackImagePath;

                await _cardRepository.InsertCardAsync(card);

                _logger.LogInformation("Card saved and redirecting to pricing: {PlayerName}", card.PlayerName);

                // Redirect to pricing research (now with database ID)
                TempData["SuccessMessage"] = $"Card '{card.PlayerName}' saved!";
                return RedirectToAction("Research", "Pricing", new { id = card.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SaveAndResearch");
                TempData["ErrorMessage"] = $"Failed to save card: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Scan/Discard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Discard()
        {
            var scanResultJson = TempData["ScanResult"] as string;
            if (!string.IsNullOrEmpty(scanResultJson))
            {
                try
                {
                    var scanViewModel = JsonSerializer.Deserialize<ScanResultViewModel>(scanResultJson);
                    if (scanViewModel != null)
                    {
                        CleanupTempFiles(scanViewModel.FrontImagePath, scanViewModel.BackImagePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error cleaning up temp files");
                }
            }

            TempData["SuccessMessage"] = "Scan discarded.";
            return RedirectToAction(nameof(Index));
        }

        private void CleanupTempFiles(params string?[] paths)
        {
            foreach (var path in paths)
            {
                if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                {
                    try
                    {
                        System.IO.File.Delete(path);
                        _logger.LogDebug("Deleted temp file: {Path}", path);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temp file: {Path}", path);
                    }
                }
            }
        }
    }
}
