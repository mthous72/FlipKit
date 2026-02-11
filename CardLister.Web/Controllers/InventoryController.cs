using Microsoft.AspNetCore.Mvc;
using FlipKit.Core.Services;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Web.Models;
using System.Linq;

namespace FlipKit.Web.Controllers
{
    /// <summary>
    /// Inventory management controller for viewing, editing, and deleting cards.
    /// </summary>
    public class InventoryController : Controller
    {
        private readonly ICardRepository _cardRepository;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(ICardRepository cardRepository, ILogger<InventoryController> logger)
        {
            _cardRepository = cardRepository;
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

                return View(card);
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

                return View(card);
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
        public async Task<IActionResult> Edit(int id, Card card)
        {
            if (id != card.Id)
            {
                TempData["ErrorMessage"] = "Invalid card ID.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Update timestamp
                card.UpdatedAt = DateTime.UtcNow;

                await _cardRepository.UpdateCardAsync(card);

                _logger.LogInformation("Card {CardId} updated successfully", id);
                TempData["SuccessMessage"] = $"Card '{card.PlayerName}' updated successfully.";
                return RedirectToAction(nameof(Details), new { id = card.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating card {CardId}", id);
                TempData["ErrorMessage"] = "Error saving changes. Please try again.";
                return View(card);
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
    }
}
