using Microsoft.AspNetCore.Mvc;
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
        private readonly ICardRepository _cardRepository;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(
            ICardRepository cardRepository,
            IWebHostEnvironment env,
            ILogger<InventoryController> logger)
        {
            _cardRepository = cardRepository;
            _env = env;
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
            var files = new[] { viewModel.ImageFile3, viewModel.ImageFile4, viewModel.ImageFile5,
                                viewModel.ImageFile6, viewModel.ImageFile7, viewModel.ImageFile8 };
            var removals = new[] { viewModel.RemoveImage3, viewModel.RemoveImage4, viewModel.RemoveImage5,
                                   viewModel.RemoveImage6, viewModel.RemoveImage7, viewModel.RemoveImage8 };

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
            }

            viewModel.ImagePath3 = paths[0]; viewModel.ImagePath4 = paths[1]; viewModel.ImagePath5 = paths[2];
            viewModel.ImagePath6 = paths[3]; viewModel.ImagePath7 = paths[4]; viewModel.ImagePath8 = paths[5];
            viewModel.ImageUrl3 = urls[0]; viewModel.ImageUrl4 = urls[1]; viewModel.ImageUrl5 = urls[2];
            viewModel.ImageUrl6 = urls[3]; viewModel.ImageUrl7 = urls[4]; viewModel.ImageUrl8 = urls[5];
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
    }
}
