using Microsoft.AspNetCore.Mvc;
using FlipKit.Core.Helpers;
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
        private readonly IOpenRouterModelCatalog _modelCatalog;
        private readonly IImageUploadService _imageUploadService;
        private readonly ILogger<ScanController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IPricerService _pricerService;

        public ScanController(
            IScannerService scannerService,
            ICardRepository cardRepository,
            IVariationVerifier variationVerifier,
            ISettingsService settingsService,
            IOpenRouterModelCatalog modelCatalog,
            IImageUploadService imageUploadService,
            ILogger<ScanController> logger,
            IWebHostEnvironment environment,
            IPricerService pricerService)
        {
            _scannerService = scannerService;
            _cardRepository = cardRepository;
            _variationVerifier = variationVerifier;
            _settingsService = settingsService;
            _modelCatalog = modelCatalog;
            _imageUploadService = imageUploadService;
            _logger = logger;
            _environment = environment;
            _pricerService = pricerService;
        }

        // GET: Scan
        public async Task<IActionResult> Index(string? mode)
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
                XimilarMode = ximilarMode,
                SelectedModel = WebModelOption.AutoValue
            };

            try
            {
                var catalog = await _modelCatalog.GetAsync();
                viewModel.FreeModels = catalog.FreeVisionModels;
                viewModel.PaidModels = catalog.PaidVisionModels;
                if (catalog.IsEmpty)
                    viewModel.CatalogError = "Couldn't reach OpenRouter for the live model list. Auto mode will fail until the catalog loads.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Catalog fetch failed on Scan/Index GET");
                viewModel.CatalogError = $"Model catalog failed to load: {ex.Message}";
            }

            return View(viewModel);
        }

        // POST: Scan/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(
            IFormFile? frontImage,
            IFormFile? backImage,
            string? selectedModel,
            string? ximilarMode,
            string? frontImagePath,
            string? backImagePath)
        {
            // Either a multipart upload OR a server-side path from the webcam
            // capture flow (POST /api/cards/upload-image) is acceptable for
            // each slot. The path-based variant skips the second copy because
            // the file is already in wwwroot/uploads.
            var hasFrontFile = frontImage is { Length: > 0 };
            var hasFrontPath = !string.IsNullOrEmpty(frontImagePath) && System.IO.File.Exists(frontImagePath);

            if (!hasFrontFile && !hasFrontPath)
            {
                TempData["ErrorMessage"] = "Please upload or capture a front image of the card.";
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

                // Save uploaded images to temp directory (only when a fresh file
                // arrived — webcam captures land in wwwroot/uploads via the
                // ImageUploadController already and pass us a path).
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
                Directory.CreateDirectory(uploadsPath);

                if (hasFrontFile)
                {
                    frontImagePath = Path.Combine(uploadsPath, $"{Guid.NewGuid()}_{frontImage!.FileName}");
                    using var stream = new FileStream(frontImagePath, FileMode.Create);
                    await frontImage.CopyToAsync(stream);
                }

                var hasBackFile = backImage is { Length: > 0 };
                var hasBackPath = !string.IsNullOrEmpty(backImagePath) && System.IO.File.Exists(backImagePath);
                if (hasBackFile)
                {
                    backImagePath = Path.Combine(uploadsPath, $"{Guid.NewGuid()}_{backImage!.FileName}");
                    using var stream = new FileStream(backImagePath, FileMode.Create);
                    await backImage.CopyToAsync(stream);
                }
                else if (!hasBackPath)
                {
                    backImagePath = null;
                }

                // Promote to non-null for the rest of the method. The early-return
                // above guarantees one of the two front-image paths set this.
                if (string.IsNullOrEmpty(frontImagePath))
                    throw new InvalidOperationException("Front image path was not set after validation — this should be unreachable.");

                // Resolve scan strategy from the form value:
                //   • "auto" or null → server-side free-model rotation (no paid fallback on Web).
                //   • Specific model id → single-attempt explicit pick.
                var settings = _settingsService.Load();
                var modelChoice = selectedModel ?? settings.DefaultModel ?? WebModelOption.AutoValue;

                ScanResult? scanResult = null;
                Exception? lastScanError = null;
                if (modelChoice == WebModelOption.AutoValue)
                {
                    var catalog = await _modelCatalog.GetAsync();
                    if (catalog.IsEmpty)
                    {
                        TempData["ErrorMessage"] = "Couldn't load the OpenRouter model list. Pick a specific model or try again.";
                        CleanupTempFiles(frontImagePath, backImagePath);
                        return RedirectToAction(nameof(Index));
                    }

                    foreach (var freeModel in catalog.FreeVisionModels)
                    {
                        try
                        {
                            _logger.LogInformation("Auto-rotation: trying free model {Model} (Ximilar: {XimilarMode})",
                                freeModel.Id, parsedXimilarMode);
                            scanResult = await _scannerService.ScanCardAsync(
                                frontImagePath, backImagePath, freeModel.Id, parsedXimilarMode);
                            if (scanResult != null) break;
                        }
                        catch (Exception ex)
                        {
                            lastScanError = ex;
                            _logger.LogWarning(ex, "Free model {Model} failed; trying next.", freeModel.Id);
                        }
                    }

                    if (scanResult == null)
                    {
                        // Web side does not show the paid-consent dialog (server-side flow).
                        // Surface a clear deflection so the user knows their options.
                        TempData["ErrorMessage"] =
                            $"All {catalog.FreeVisionModels.Count} free OpenRouter vision models failed. " +
                            "To allow paid-model fallback, pick a specific paid model from the list, or run the scan from the Desktop app.";
                        CleanupTempFiles(frontImagePath, backImagePath);
                        return RedirectToAction(nameof(Index));
                    }
                }
                else
                {
                    _logger.LogInformation("Scanning with explicit model {Model}, Ximilar: {XimilarMode}",
                        modelChoice, parsedXimilarMode);
                    scanResult = await _scannerService.ScanCardAsync(
                        frontImagePath, backImagePath, modelChoice, parsedXimilarMode);
                }

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

                card.CreatedAt = DateTime.UtcNow;
                card.UpdatedAt = DateTime.UtcNow;

                // Store image paths
                card.ImagePathFront = scanViewModel.FrontImagePath;
                card.ImagePathBack = scanViewModel.BackImagePath;

                // Auto-upload any local images to ImgBB and auto-evaluate status —
                // a card with both images and a price saves as Ready; otherwise Draft.
                await TryUploadMissingUrlsAsync(card);
                card.Status = CardStatusEvaluator.Evaluate(card);

                // Save to database
                await _cardRepository.InsertCardAsync(card);

                _logger.LogInformation("Card saved as {Status}: {PlayerName} - {Year} {Brand}",
                    card.Status, card.PlayerName, card.Year, card.Brand);

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

                card.CreatedAt = DateTime.UtcNow;
                card.UpdatedAt = DateTime.UtcNow;
                card.ImagePathFront = scanViewModel.FrontImagePath;
                card.ImagePathBack = scanViewModel.BackImagePath;

                // Auto-fill Whatnot category/subcategory from Sport when blank,
                // then auto-upload + auto-status (Ready if images + price, else Draft).
                WhatnotCategoryDefaulter.ApplyDefaults(card);
                await TryUploadMissingUrlsAsync(card);
                card.Status = CardStatusEvaluator.Evaluate(card);

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

        /// <summary>
        /// Uploads any local image paths that don't yet have a corresponding hosted URL
        /// to ImgBB and populates <c>ImageUrl{N}</c> on the card. Network errors are
        /// swallowed; the card still saves with whatever URLs were obtained.
        /// </summary>
        private async Task TryUploadMissingUrlsAsync(Card card)
        {
            var paths = new[] { card.ImagePathFront, card.ImagePathBack,
                                card.ImagePath3, card.ImagePath4, card.ImagePath5,
                                card.ImagePath6, card.ImagePath7, card.ImagePath8 };
            var urls  = new[] { card.ImageUrl1, card.ImageUrl2,
                                card.ImageUrl3, card.ImageUrl4, card.ImageUrl5,
                                card.ImageUrl6, card.ImageUrl7, card.ImageUrl8 };

            var pathsToUpload = new List<string?>(8);
            for (int i = 0; i < 8; i++)
                pathsToUpload.Add(string.IsNullOrEmpty(urls[i]) ? paths[i] : null);

            if (!pathsToUpload.Any(p => !string.IsNullOrEmpty(p))) return;

            try
            {
                var newUrls = await _imageUploadService.UploadCardImagesAsync(pathsToUpload);
                if (newUrls[0] != null) card.ImageUrl1 = newUrls[0];
                if (newUrls[1] != null) card.ImageUrl2 = newUrls[1];
                if (newUrls[2] != null) card.ImageUrl3 = newUrls[2];
                if (newUrls[3] != null) card.ImageUrl4 = newUrls[3];
                if (newUrls[4] != null) card.ImageUrl5 = newUrls[4];
                if (newUrls[5] != null) card.ImageUrl6 = newUrls[5];
                if (newUrls[6] != null) card.ImageUrl7 = newUrls[6];
                if (newUrls[7] != null) card.ImageUrl8 = newUrls[7];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImgBB upload during save failed for {Player}.", card.PlayerName);
            }
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
