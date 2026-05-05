using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using FlipKit.Core.Helpers;
using FlipKit.Core.Services;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Web.Models;
using System.IO;
using System.Linq;

namespace FlipKit.Web.Controllers
{
    /// <summary>
    /// Inventory management controller for viewing, editing, and deleting cards.
    /// </summary>
    public class InventoryController : Controller
    {
        private const string EbayPreviewCachePrefix = "ebay-import:";
        private static readonly TimeSpan EbayPreviewTtl = TimeSpan.FromMinutes(30);

        private readonly ICardRepository _cardRepository;
        private readonly IWebHostEnvironment _env;
        private readonly IImageUploadService _imageUploadService;
        private readonly IEbayListingImportService _ebayImportService;
        private readonly IMemoryCache _previewCache;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(
            ICardRepository cardRepository,
            IWebHostEnvironment env,
            IImageUploadService imageUploadService,
            IEbayListingImportService ebayImportService,
            IMemoryCache previewCache,
            ILogger<InventoryController> logger)
        {
            _cardRepository = cardRepository;
            _env = env;
            _imageUploadService = imageUploadService;
            _ebayImportService = ebayImportService;
            _previewCache = previewCache;
            _logger = logger;
        }

        // GET: Inventory
        public async Task<IActionResult> Index(
            string? search,
            string sport = "All",
            string status = "All",
            int page = 1,
            int pageSize = 20)
        {
            try
            {
                // Get all cards
                var allCards = await _cardRepository.GetAllCardsAsync();

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchLower = search.ToLower();
                    allCards = allCards.Where(c =>
                        (c.PlayerName?.ToLower().Contains(searchLower) ?? false) ||
                        (c.Brand?.ToLower().Contains(searchLower) ?? false) ||
                        (c.Team?.ToLower().Contains(searchLower) ?? false) ||
                        (c.SetName?.ToLower().Contains(searchLower) ?? false) ||
                        (c.ParallelName?.ToLower().Contains(searchLower) ?? false) ||
                        (c.CardNumber?.ToLower().Contains(searchLower) ?? false)
                    ).ToList();
                }

                // Apply sport filter
                if (sport != "All" && Enum.TryParse<Sport>(sport, out var sportEnum))
                {
                    allCards = allCards.Where(c => c.Sport == sportEnum).ToList();
                }

                // Apply status filter
                if (status != "All" && Enum.TryParse<CardStatus>(status, out var statusEnum))
                {
                    allCards = allCards.Where(c => c.Status == statusEnum).ToList();
                }

                // Calculate pagination
                var totalCount = allCards.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                page = Math.Max(1, Math.Min(page, totalPages == 0 ? 1 : totalPages));

                // Get page of results
                var cards = allCards
                    .OrderByDescending(c => c.UpdatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var viewModel = new InventoryListViewModel
                {
                    Cards = cards,
                    CurrentPage = page,
                    TotalPages = totalPages,
                    TotalCount = totalCount,
                    PageSize = pageSize,
                    SearchQuery = search,
                    SelectedSport = sport,
                    SelectedStatus = status
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading inventory");
                TempData["ErrorMessage"] = "Error loading inventory. Please try again.";
                return View(new InventoryListViewModel());
            }
        }

        // GET: Inventory/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var card = await _cardRepository.GetCardAsync(id);
                if (card == null)
                {
                    TempData["ErrorMessage"] = "Card not found.";
                    return RedirectToAction(nameof(Index));
                }

                var viewModel = MapCardToViewModel(card);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading card details for ID {CardId}", id);
                TempData["ErrorMessage"] = "Error loading card details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Inventory/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var card = await _cardRepository.GetCardAsync(id);
                if (card == null)
                {
                    TempData["ErrorMessage"] = "Card not found.";
                    return RedirectToAction(nameof(Index));
                }

                var viewModel = MapCardToViewModel(card);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading card for editing ID {CardId}", id);
                TempData["ErrorMessage"] = "Error loading card. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Inventory/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CardDetailsViewModel viewModel)
        {
            if (id != viewModel.Id)
            {
                TempData["ErrorMessage"] = "Invalid card ID.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Get existing card to preserve fields not in the view model
                var existingCard = await _cardRepository.GetCardAsync(id);
                if (existingCard == null)
                {
                    TempData["ErrorMessage"] = "Card not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Process additional-photo uploads and removals BEFORE the main mapping
                // so the resulting paths are merged into the view model and the standard
                // mapping pass picks them up.
                await ProcessAdditionalPhotosAsync(viewModel, existingCard);

                // Map view model back to card
                MapViewModelToCard(viewModel, existingCard);
                existingCard.UpdatedAt = DateTime.UtcNow;

                // Auto-fill Whatnot category/subcategory from Sport when blank,
                // then auto-upload any local images, then auto-evaluate status
                // (Ready when both images and price are present; Draft otherwise).
                WhatnotCategoryDefaulter.ApplyDefaults(existingCard);
                await TryUploadMissingUrlsAsync(existingCard);
                existingCard.Status = CardStatusEvaluator.Evaluate(existingCard);

                await _cardRepository.UpdateCardAsync(existingCard);

                _logger.LogInformation("Card {CardId} updated successfully", id);
                TempData["SuccessMessage"] = $"Card '{existingCard.PlayerName}' updated successfully.";
                return RedirectToAction(nameof(Details), new { id = existingCard.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating card {CardId}", id);
                TempData["ErrorMessage"] = "Error saving changes. Please try again.";
                return View(viewModel);
            }
        }

        private static CardDetailsViewModel MapCardToViewModel(Card card)
        {
            return new CardDetailsViewModel
            {
                Id = card.Id,
                PlayerName = card.PlayerName,
                Sport = card.Sport,
                Brand = card.Brand,
                Manufacturer = card.Manufacturer,
                Year = card.Year,
                CardNumber = card.CardNumber,
                Team = card.Team,
                SetName = card.SetName,
                VariationType = card.VariationType,
                ParallelName = card.ParallelName,
                SerialNumbered = card.SerialNumbered,
                IsShortPrint = card.IsShortPrint,
                IsSSP = card.IsSSP,
                IsRookie = card.IsRookie,
                IsAuto = card.IsAuto,
                IsRelic = card.IsRelic,
                Condition = card.Condition,
                IsGraded = card.IsGraded,
                GradeCompany = card.GradeCompany,
                GradeValue = card.GradeValue,
                CertNumber = card.CertNumber,
                AutoGrade = card.AutoGrade,
                CostBasis = card.CostBasis,
                CostSource = card.CostSource,
                CostDate = card.CostDate,
                CostNotes = card.CostNotes,
                Quantity = card.Quantity,
                EstimatedValue = card.EstimatedValue,
                ListingPrice = card.ListingPrice,
                ListingType = card.ListingType,
                Offerable = card.Offerable,
                ShippingProfile = card.ShippingProfile,
                WhatnotCategory = card.WhatnotCategory,
                WhatnotSubcategory = card.WhatnotSubcategory,
                Notes = card.Notes,
                Status = card.Status,
                ImagePathFront = card.ImagePathFront,
                ImagePathBack = card.ImagePathBack,
                ImageUrl1 = card.ImageUrl1,
                ImageUrl2 = card.ImageUrl2,
                ImagePath3 = card.ImagePath3,
                ImagePath4 = card.ImagePath4,
                ImagePath5 = card.ImagePath5,
                ImagePath6 = card.ImagePath6,
                ImagePath7 = card.ImagePath7,
                ImagePath8 = card.ImagePath8,
                ImageUrl3 = card.ImageUrl3,
                ImageUrl4 = card.ImageUrl4,
                ImageUrl5 = card.ImageUrl5,
                ImageUrl6 = card.ImageUrl6,
                ImageUrl7 = card.ImageUrl7,
                ImageUrl8 = card.ImageUrl8,
                CreatedAt = card.CreatedAt,
                UpdatedAt = card.UpdatedAt
            };
        }

        private static void MapViewModelToCard(CardDetailsViewModel viewModel, Card card)
        {
            card.PlayerName = viewModel.PlayerName ?? "";
            card.Sport = viewModel.Sport;
            card.Brand = viewModel.Brand;
            card.Manufacturer = viewModel.Manufacturer;
            card.Year = viewModel.Year;
            card.CardNumber = viewModel.CardNumber;
            card.Team = viewModel.Team;
            card.SetName = viewModel.SetName;
            card.VariationType = viewModel.VariationType;
            card.ParallelName = viewModel.ParallelName;
            card.SerialNumbered = viewModel.SerialNumbered;
            card.IsShortPrint = viewModel.IsShortPrint;
            card.IsSSP = viewModel.IsSSP;
            card.IsRookie = viewModel.IsRookie;
            card.IsAuto = viewModel.IsAuto;
            card.IsRelic = viewModel.IsRelic;
            card.Condition = viewModel.Condition;
            card.IsGraded = viewModel.IsGraded;
            card.GradeCompany = viewModel.GradeCompany;
            card.GradeValue = viewModel.GradeValue;
            card.CertNumber = viewModel.CertNumber;
            card.AutoGrade = viewModel.AutoGrade;
            card.CostBasis = viewModel.CostBasis;
            card.CostSource = viewModel.CostSource;
            card.CostDate = viewModel.CostDate;
            card.CostNotes = viewModel.CostNotes;
            card.Quantity = viewModel.Quantity;
            card.EstimatedValue = viewModel.EstimatedValue;
            card.ListingPrice = viewModel.ListingPrice;
            card.ListingType = viewModel.ListingType;
            card.Offerable = viewModel.Offerable;
            card.ShippingProfile = viewModel.ShippingProfile;
            card.WhatnotCategory = viewModel.WhatnotCategory;
            card.WhatnotSubcategory = viewModel.WhatnotSubcategory;
            card.Notes = viewModel.Notes;
            card.Status = viewModel.Status;
            // Preserve image paths from existing card if not provided
            if (!string.IsNullOrEmpty(viewModel.ImagePathFront))
                card.ImagePathFront = viewModel.ImagePathFront;
            if (!string.IsNullOrEmpty(viewModel.ImagePathBack))
                card.ImagePathBack = viewModel.ImagePathBack;
            if (!string.IsNullOrEmpty(viewModel.ImageUrl1))
                card.ImageUrl1 = viewModel.ImageUrl1;
            if (!string.IsNullOrEmpty(viewModel.ImageUrl2))
                card.ImageUrl2 = viewModel.ImageUrl2;

            // Slots 3-8 — overwrite with whatever the view model carries (set by
            // ProcessAdditionalPhotosAsync to the post-upload / post-remove state).
            card.ImagePath3 = viewModel.ImagePath3;
            card.ImagePath4 = viewModel.ImagePath4;
            card.ImagePath5 = viewModel.ImagePath5;
            card.ImagePath6 = viewModel.ImagePath6;
            card.ImagePath7 = viewModel.ImagePath7;
            card.ImagePath8 = viewModel.ImagePath8;
            card.ImageUrl3 = viewModel.ImageUrl3;
            card.ImageUrl4 = viewModel.ImageUrl4;
            card.ImageUrl5 = viewModel.ImageUrl5;
            card.ImageUrl6 = viewModel.ImageUrl6;
            card.ImageUrl7 = viewModel.ImageUrl7;
            card.ImageUrl8 = viewModel.ImageUrl8;
        }

        /// <summary>
        /// Handles the multipart-form file uploads and remove-checkboxes for slots 3-8.
        /// Saves new uploads to wwwroot/uploads/, clears slots flagged for removal, and
        /// updates the view model so the standard MapViewModelToCard pass picks up the
        /// resulting state.
        /// </summary>
        private async Task ProcessAdditionalPhotosAsync(CardDetailsViewModel viewModel, Card existingCard)
        {
            // Pull current state from the existing card — view model fields may be null
            // for hidden fields the form omitted.
            var paths = new string?[6] { existingCard.ImagePath3, existingCard.ImagePath4, existingCard.ImagePath5,
                                          existingCard.ImagePath6, existingCard.ImagePath7, existingCard.ImagePath8 };
            var urls  = new string?[6] { existingCard.ImageUrl3, existingCard.ImageUrl4, existingCard.ImageUrl5,
                                          existingCard.ImageUrl6, existingCard.ImageUrl7, existingCard.ImageUrl8 };
            // Webcam-captured paths come back via the same hidden ImagePath{slot}
            // inputs the controller already binds. We compare against existingCard
            // to detect "the form sent a fresher path than the DB has."
            var formPaths = new string?[6] { viewModel.ImagePath3, viewModel.ImagePath4, viewModel.ImagePath5,
                                              viewModel.ImagePath6, viewModel.ImagePath7, viewModel.ImagePath8 };
            var files = new[] { viewModel.ImageFile3, viewModel.ImageFile4, viewModel.ImageFile5,
                                viewModel.ImageFile6, viewModel.ImageFile7, viewModel.ImageFile8 };
            var removals = new[] { viewModel.RemoveImage3, viewModel.RemoveImage4, viewModel.RemoveImage5,
                                   viewModel.RemoveImage6, viewModel.RemoveImage7, viewModel.RemoveImage8 };

            // Restrict webcam-captured paths to the wwwroot/uploads dir. Without this
            // a malicious form submission could point ImagePath{n} at any file the
            // server can read (e.g. /etc/passwd) and have the export upload it later.
            var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
            string? uploadsAbsolute = null;
            try { uploadsAbsolute = Path.GetFullPath(uploadsDir); } catch { uploadsAbsolute = null; }

            for (int i = 0; i < 6; i++)
            {
                // Removal wins over upload — if both are set on the same slot, the user
                // wanted to clear it.
                if (removals[i])
                {
                    paths[i] = null;
                    urls[i] = null;
                    continue;
                }

                if (files[i] != null && files[i]!.Length > 0)
                {
                    var savedPath = await SaveUploadedFileAsync(files[i]!);
                    paths[i] = savedPath;
                    // New file → previous hosted URL is now stale, so it gets cleared.
                    // The Export "Upload Images" step will re-upload the new file.
                    urls[i] = null;
                }
                else if (IsFreshWebcamPath(formPaths[i], paths[i], uploadsAbsolute))
                {
                    // Webcam captured a still and stuffed the path into the hidden
                    // ImagePath{slot} input. Treat it the same as a file upload —
                    // adopt the new path, drop the old hosted URL.
                    paths[i] = formPaths[i];
                    urls[i] = null;
                }
            }

            viewModel.ImagePath3 = paths[0]; viewModel.ImagePath4 = paths[1]; viewModel.ImagePath5 = paths[2];
            viewModel.ImagePath6 = paths[3]; viewModel.ImagePath7 = paths[4]; viewModel.ImagePath8 = paths[5];
            viewModel.ImageUrl3 = urls[0]; viewModel.ImageUrl4 = urls[1]; viewModel.ImageUrl5 = urls[2];
            viewModel.ImageUrl6 = urls[3]; viewModel.ImageUrl7 = urls[4]; viewModel.ImageUrl8 = urls[5];
        }

        /// <summary>
        /// Uploads any local image paths that don't yet have a corresponding hosted URL
        /// to ImgBB. Network errors are swallowed — partial uploads are fine; the save
        /// still proceeds.
        /// </summary>
        private async Task TryUploadMissingUrlsAsync(Card card)
        {
            var paths = new[] { card.ImagePathFront, card.ImagePathBack,
                                card.ImagePath3, card.ImagePath4, card.ImagePath5,
                                card.ImagePath6, card.ImagePath7, card.ImagePath8 };
            var urls  = new[] { card.ImageUrl1, card.ImageUrl2,
                                card.ImageUrl3, card.ImageUrl4, card.ImageUrl5,
                                card.ImageUrl6, card.ImageUrl7, card.ImageUrl8 };

            var pathsToUpload = new System.Collections.Generic.List<string?>(8);
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
            catch (System.Exception ex)
            {
                _logger.LogWarning(ex, "ImgBB upload during save failed for card {Id}.", card.Id);
            }
        }

        /// <summary>
        /// True when <paramref name="formPath"/> is a webcam-captured upload path
        /// the user just got back from <c>/api/cards/upload-image</c>: non-empty,
        /// different from the existing DB-stored path, and physically inside
        /// <c>wwwroot/uploads</c>. The path-rooted check guards against a
        /// malicious form pointing the server at an arbitrary file.
        /// </summary>
        private static bool IsFreshWebcamPath(string? formPath, string? existingPath, string? uploadsAbsolute)
        {
            if (string.IsNullOrWhiteSpace(formPath)) return false;
            if (string.Equals(formPath, existingPath, System.StringComparison.OrdinalIgnoreCase)) return false;
            if (string.IsNullOrEmpty(uploadsAbsolute)) return false;

            string fullForm;
            try { fullForm = Path.GetFullPath(formPath); }
            catch { return false; }

            // Path.GetFullPath collapses ".." traversal, so a startsWith check on
            // the canonicalised path is sufficient. Add the directory separator so
            // "uploads2" doesn't match "uploads".
            var prefix = uploadsAbsolute.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullForm.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)
                && System.IO.File.Exists(fullForm);
        }

        private async Task<string> SaveUploadedFileAsync(Microsoft.AspNetCore.Http.IFormFile file)
        {
            var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
            Directory.CreateDirectory(uploadsDir);

            var safeName = Path.GetFileName(file.FileName);
            var fileName = $"{Guid.NewGuid()}_{safeName}";
            var fullPath = Path.Combine(uploadsDir, fileName);
            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return fullPath;
        }

        // POST: Inventory/Reprice/5 — clears pricing and sends card back to Draft
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reprice(int id)
        {
            try
            {
                var card = await _cardRepository.GetCardAsync(id);
                if (card == null)
                {
                    TempData["ErrorMessage"] = "Card not found.";
                    return RedirectToAction(nameof(Index));
                }

                card.EstimatedValue = null;
                card.ListingPrice = null;
                card.PriceSource = null;
                card.PriceDate = null;
                card.Status = CardStatus.Draft;
                card.UpdatedAt = DateTime.UtcNow;

                await _cardRepository.UpdateCardAsync(card);

                TempData["SuccessMessage"] = $"'{card.PlayerName}' sent back to Draft for repricing.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error repricing card {CardId}", id);
                TempData["ErrorMessage"] = "Error repricing card. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Inventory/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var card = await _cardRepository.GetCardAsync(id);
                if (card == null)
                {
                    TempData["ErrorMessage"] = "Card not found.";
                    return RedirectToAction(nameof(Index));
                }

                var playerName = card.PlayerName;
                await _cardRepository.DeleteCardAsync(id);

                _logger.LogInformation("Card {CardId} deleted successfully", id);
                TempData["SuccessMessage"] = $"Card '{playerName}' deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting card {CardId}", id);
                TempData["ErrorMessage"] = "Error deleting card. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Inventory/ImportEbay — landing page with the file picker form.
        [HttpGet]
        public IActionResult ImportEbay() => View();

        // POST: Inventory/ImportEbay — parse the CSV + run the LLM enrichment,
        // stash the resulting preview in IMemoryCache keyed by a fresh GUID
        // token, then redirect to the review page. The user picks which rows
        // to import on the review page and posts the token back to
        // ImportEbayCommit, which reads from the cache (no second LLM call).
        // Cache TTL is 30 minutes sliding — long enough for a careful review
        // without hoarding RAM forever.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(20 * 1024 * 1024)] // 20 MB cap; 200 rows × 1 KB each leaves plenty of headroom
        public async Task<IActionResult> ImportEbay(IFormFile? csvFile)
        {
            if (csvFile is null || csvFile.Length == 0)
            {
                TempData["ErrorMessage"] = "No file uploaded. Pick the eBay Seller Hub CSV export.";
                return RedirectToAction(nameof(ImportEbay));
            }

            try
            {
                EbayListingImportPreview preview;
                using (var stream = csvFile.OpenReadStream())
                {
                    preview = await _ebayImportService.ParseAsync(stream, csvFile.FileName);
                }

                if (preview.Rows.Count == 0)
                {
                    var warnings = string.Join(" ", preview.Warnings);
                    TempData["ErrorMessage"] = $"No importable rows found in {csvFile.FileName}. {warnings}".Trim();
                    return RedirectToAction(nameof(ImportEbay));
                }

                var token = Guid.NewGuid().ToString("N");
                _previewCache.Set(EbayPreviewCachePrefix + token, preview, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = EbayPreviewTtl,
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2),
                });

                return RedirectToAction(nameof(ImportEbayReview), new { token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "eBay listings import failed for {File}", csvFile.FileName);
                TempData["ErrorMessage"] = $"Import failed: {ex.Message}";
                return RedirectToAction(nameof(ImportEbay));
            }
        }

        // GET: Inventory/ImportEbayReview?token=... — renders the cached preview
        // with Skip checkboxes per row. Token expiry sends the user back to the
        // upload page with a clear message.
        [HttpGet]
        public IActionResult ImportEbayReview(string? token)
        {
            if (string.IsNullOrEmpty(token) ||
                !_previewCache.TryGetValue<EbayListingImportPreview>(EbayPreviewCachePrefix + token, out var preview)
                || preview is null)
            {
                TempData["ErrorMessage"] = "Preview expired or not found — please re-upload the CSV.";
                return RedirectToAction(nameof(ImportEbay));
            }

            ViewData["PreviewToken"] = token;
            return View(preview);
        }

        // POST: Inventory/ImportEbayCommit — load preview from cache by token,
        // mark rows the user unticked as Skip, then commit. The form posts an
        // array of ebay item-ids to keep ("commitItemIds[]"); rows whose item
        // id isn't in that list are skipped.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportEbayCommit(string token, string[]? commitItemIds)
        {
            if (string.IsNullOrEmpty(token) ||
                !_previewCache.TryGetValue<EbayListingImportPreview>(EbayPreviewCachePrefix + token, out var preview)
                || preview is null)
            {
                TempData["ErrorMessage"] = "Preview expired or not found — please re-upload the CSV.";
                return RedirectToAction(nameof(ImportEbay));
            }

            var keep = new HashSet<string>(commitItemIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var row in preview.Rows)
            {
                row.Skip = string.IsNullOrEmpty(row.CsvRow.EbayItemId) || !keep.Contains(row.CsvRow.EbayItemId);
            }

            try
            {
                var result = await _ebayImportService.CommitAsync(preview);
                _previewCache.Remove(EbayPreviewCachePrefix + token);

                var summary = $"Imported {result.Inserted} new + {result.Updated} updated, {result.Skipped} skipped (from {preview.SourceFileName}).";
                if (result.Errors.Count > 0)
                {
                    TempData["ErrorMessage"] = $"{summary} ({result.Errors.Count} errors — first: {result.Errors[0]})";
                }
                else
                {
                    TempData["SuccessMessage"] = summary;
                }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "eBay listings commit failed for token {Token}", token);
                TempData["ErrorMessage"] = $"Commit failed: {ex.Message}";
                return RedirectToAction(nameof(ImportEbayReview), new { token });
            }
        }
    }
}
