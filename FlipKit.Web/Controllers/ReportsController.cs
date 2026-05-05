using Microsoft.AspNetCore.Mvc;
using FlipKit.Core.Services;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Web.Models;

namespace FlipKit.Web.Controllers
{
    public class ReportsController : Controller
    {
        private readonly ICardRepository _cardRepository;
        private readonly IExportService _exportService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(
            ICardRepository cardRepository,
            IExportService exportService,
            ILogger<ReportsController> logger)
        {
            _cardRepository = cardRepository;
            _exportService = exportService;
            _logger = logger;
        }

        // GET: Reports
        [HttpGet]
        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate)
        {
            var end = endDate?.Date ?? DateTime.Today;
            var start = startDate?.Date ?? end.AddDays(-89);
            return View(await BuildViewModelAsync(start, end));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Index")]
        public async Task<IActionResult> IndexPost(DateTime? startDate, DateTime? endDate)
        {
            var end = endDate?.Date ?? DateTime.Today;
            var start = startDate?.Date ?? end.AddDays(-89);
            return View(await BuildViewModelAsync(start, end));
        }

        // POST: Reports/ExportTaxCsv
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportTaxCsv(DateTime? startDate, DateTime? endDate)
        {
            var end = endDate?.Date ?? DateTime.Today;
            var start = startDate?.Date ?? end.AddDays(-89);

            try
            {
                var sold = await _cardRepository.GetAllCardsAsync(CardStatus.Sold);
                var inRange = FilterByDateRange(sold, start, end);

                if (inRange.Count == 0)
                {
                    TempData["ErrorMessage"] = "No sold cards in the selected date range.";
                    return RedirectToAction(nameof(Index), new { startDate = start, endDate = end });
                }

                var tempPath = Path.Combine(Path.GetTempPath(), $"flipkit-tax-{Guid.NewGuid():N}.csv");
                try
                {
                    await _exportService.ExportTaxCsvAsync(inRange, tempPath);
                    var bytes = await System.IO.File.ReadAllBytesAsync(tempPath);
                    var filename = $"FlipKit-Tax-{start:yyyyMMdd}-{end:yyyyMMdd}.csv";
                    return File(bytes, "text/csv", filename);
                }
                finally
                {
                    if (System.IO.File.Exists(tempPath))
                        System.IO.File.Delete(tempPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting tax CSV");
                TempData["ErrorMessage"] = "Tax CSV export failed. Please try again.";
                return RedirectToAction(nameof(Index), new { startDate = start, endDate = end });
            }
        }

        private async Task<ReportsViewModel> BuildViewModelAsync(DateTime start, DateTime end)
        {
            try
            {
                var allCards = await _cardRepository.GetAllCardsAsync();
                var sold = await _cardRepository.GetAllCardsAsync(CardStatus.Sold);
                var inRange = FilterByDateRange(sold, start, end);

                var monthly = inRange
                    .GroupBy(c => new { Year = (c.SaleDate ?? c.UpdatedAt).Year, Month = (c.SaleDate ?? c.UpdatedAt).Month })
                    .Select(g => new MonthlyBreakdown
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        Count = g.Count(),
                        Revenue = g.Sum(c => c.SalePrice ?? 0m),
                        CostBasis = g.Sum(c => c.CostBasis ?? 0m),
                        Profit = g.Sum(c => c.NetProfit ?? ((c.SalePrice ?? 0m) - (c.CostBasis ?? 0m))),
                    })
                    .OrderBy(m => m.Year).ThenBy(m => m.Month)
                    .ToList();

                var topSellers = inRange
                    .OrderByDescending(c => c.NetProfit ?? ((c.SalePrice ?? 0m) - (c.CostBasis ?? 0m)))
                    .Take(10)
                    .ToList();

                var totalRevenue = inRange.Sum(c => c.SalePrice ?? 0m);
                var totalCost = inRange.Sum(c => c.CostBasis ?? 0m);
                var totalProfit = inRange.Sum(c => c.NetProfit ?? ((c.SalePrice ?? 0m) - (c.CostBasis ?? 0m)));

                return new ReportsViewModel
                {
                    StartDate = start,
                    EndDate = end,
                    TotalCards = allCards.Count,
                    DraftCards = allCards.Count(c => c.Status == CardStatus.Draft),
                    PricedCards = allCards.Count(c => c.Status == CardStatus.Priced),
                    ReadyCards = allCards.Count(c => c.Status == CardStatus.Ready),
                    ListedCards = allCards.Count(c => c.Status == CardStatus.Listed),
                    SoldCards = sold.Count,
                    TotalInventoryValue = allCards
                        .Where(c => c.Status != CardStatus.Sold)
                        .Sum(c => c.ListingPrice ?? 0m),
                    SoldInRange = inRange,
                    TotalRevenue = totalRevenue,
                    TotalCostBasis = totalCost,
                    TotalProfit = totalProfit,
                    AverageProfit = inRange.Count > 0 ? totalProfit / inRange.Count : 0m,
                    MonthlyBreakdowns = monthly,
                    TopSellers = topSellers,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building reports view model");
                TempData["ErrorMessage"] = "Error loading report data. Please try again.";
                return new ReportsViewModel { StartDate = start, EndDate = end };
            }
        }

        private static List<Card> FilterByDateRange(List<Card> cards, DateTime start, DateTime end)
        {
            var endInclusive = end.AddDays(1);
            return cards.Where(c =>
            {
                var date = (c.SaleDate ?? c.UpdatedAt).Date;
                return date >= start && date < endInclusive;
            }).ToList();
        }
    }
}
