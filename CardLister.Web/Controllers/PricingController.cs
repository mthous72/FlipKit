using Microsoft.AspNetCore.Mvc;
using FlipKit.Core.Services;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Web.Models;

namespace FlipKit.Web.Controllers
{
    public class PricingController : Controller
    {
        private readonly ICardRepository _cardRepository;
        private readonly IPricerService _pricerService;
        private readonly IBrowserService _browserService;
        private readonly ILogger<PricingController> _logger;

        public PricingController(
            ICardRepository cardRepository,
            IPricerService pricerService,
            IBrowserService browserService,
            ILogger<PricingController> logger)
        {
            _cardRepository = cardRepository;
            _pricerService = pricerService;
            _browserService = browserService;
            _logger = logger;
        }

        // GET: Pricing
        public async Task<IActionResult> Index()
        {
            try
            {
                var allCards = await _cardRepository.GetAllCardsAsync();

                // Get cards that need pricing (Draft or no price set)
                var needsPricing = allCards
                    .Where(c => c.Status == CardStatus.Draft || !c.ListingPrice.HasValue)
                    .OrderByDescending(c => c.UpdatedAt)
                    .ToList();

                var viewModel = new PricingListViewModel
                {
                    Cards = needsPricing
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pricing list");
                return View(new PricingListViewModel());
            }
        }

        // GET: Pricing/Research/5
        public async Task<IActionResult> Research(int id)
        {
            try
            {
                var card = await _cardRepository.GetCardAsync(id);
                if (card == null)
                {
                    return NotFound();
                }

                var viewModel = new PricingResearchViewModel
                {
                    Card = card,
                    TerapeakUrl = _pricerService.BuildTerapeakUrl(card),
                    EbaySoldUrl = _pricerService.BuildEbaySoldUrl(card),
                    EstimatedValue = card.EstimatedValue,
                    ListingPrice = card.ListingPrice
                };

                // If estimated value exists, suggest a price
                if (card.EstimatedValue.HasValue)
                {
                    viewModel.SuggestedPrice = _pricerService.SuggestPrice(card.EstimatedValue.Value, card);
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pricing research for card {CardId}", id);
                return NotFound();
            }
        }

        // POST: Pricing/Save
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int cardId, decimal? estimatedValue, decimal? listingPrice)
        {
            try
            {
                var card = await _cardRepository.GetCardAsync(cardId);
                if (card == null)
                {
                    TempData["ErrorMessage"] = "Card not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Update pricing
                card.EstimatedValue = estimatedValue;
                card.ListingPrice = listingPrice;
                card.PriceDate = DateTime.UtcNow;
                card.PriceSource = "Manual Research";
                card.UpdatedAt = DateTime.UtcNow;

                // Update status if price is set
                if (listingPrice.HasValue && listingPrice.Value > 0)
                {
                    card.Status = CardStatus.Priced;
                }

                await _cardRepository.UpdateCardAsync(card);

                _logger.LogInformation("Pricing saved for card {CardId}: ${ListingPrice}",
                    cardId, listingPrice);

                TempData["SuccessMessage"] = $"Pricing saved for '{card.PlayerName}'!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving pricing for card {CardId}", cardId);
                TempData["ErrorMessage"] = $"Failed to save pricing: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Pricing/OpenTerapeak
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OpenTerapeak(int cardId)
        {
            try
            {
                var card = await _cardRepository.GetCardAsync(cardId);
                if (card == null)
                {
                    return NotFound();
                }

                var url = _pricerService.BuildTerapeakUrl(card);
                _browserService.OpenUrl(url);

                // Return JavaScript to open URL
                return Json(new { success = true, url });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening Terapeak for card {CardId}", cardId);
                return Json(new { success = false, error = ex.Message });
            }
        }

        // POST: Pricing/OpenEbay
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OpenEbay(int cardId)
        {
            try
            {
                var card = await _cardRepository.GetCardAsync(cardId);
                if (card == null)
                {
                    return NotFound();
                }

                var url = _pricerService.BuildEbaySoldUrl(card);
                _browserService.OpenUrl(url);

                // Return JavaScript to open URL
                return Json(new { success = true, url });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening eBay for card {CardId}", cardId);
                return Json(new { success = false, error = ex.Message });
            }
        }

        // POST: Pricing/CalculateSuggested
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CalculateSuggested(int cardId, decimal estimatedValue)
        {
            try
            {
                var card = await _cardRepository.GetCardAsync(cardId);
                if (card == null)
                {
                    return Json(new { success = false, error = "Card not found" });
                }

                var suggestedPrice = _pricerService.SuggestPrice(estimatedValue, card);
                var netProfit = _pricerService.CalculateNet(suggestedPrice);

                return Json(new
                {
                    success = true,
                    suggestedPrice,
                    netProfit,
                    feeAmount = suggestedPrice - netProfit
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating suggested price for card {CardId}", cardId);
                return Json(new { success = false, error = ex.Message });
            }
        }
    }
}
