using Microsoft.AspNetCore.Mvc;
using FlipKit.Core.Services;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services.Export;
using FlipKit.Web.Models;

namespace FlipKit.Web.Controllers
{
    public class ExportController : Controller
    {
        private readonly ICardRepository _cardRepository;
        private readonly IExportService _exportService;
        private readonly ILogger<ExportController> _logger;

        public ExportController(
            ICardRepository cardRepository,
            IExportService exportService,
            ILogger<ExportController> logger)
        {
            _cardRepository = cardRepository;
            _exportService = exportService;
            _logger = logger;
        }

        // GET: Export
        public async Task<IActionResult> Index(
            string? search,
            string sport = "All",
            string status = "Ready",
            string platform = "Whatnot")
        {
            try
            {
                var cards = await LoadFilteredAsync(search, sport, status);
                var vm = new ExportListViewModel
                {
                    Cards = cards,
                    SearchQuery = search,
                    SelectedSport = sport,
                    SelectedStatus = status,
                    SelectedPlatform = platform,
                };
                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading export list");
                TempData["ErrorMessage"] = "Error loading cards. Please try again.";
                return View(new ExportListViewModel());
            }
        }

        // POST: Export/ExportCsv
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportCsv(
            List<int>? selectedIds,
            string platform = "Whatnot",
            string sport = "All",
            string status = "Ready",
            string? search = null)
        {
            if (selectedIds == null || selectedIds.Count == 0)
            {
                TempData["ErrorMessage"] = "No cards selected for export.";
                return RedirectToAction(nameof(Index), new { search, sport, status, platform });
            }

            if (!Enum.TryParse<ExportPlatform>(platform, out var exportPlatform))
                exportPlatform = ExportPlatform.Whatnot;

            try
            {
                // Load selected cards
                var allCards = await LoadFilteredAsync(search, sport, status);
                var selected = allCards.Where(c => selectedIds.Contains(c.Id)).ToList();

                if (selected.Count == 0)
                {
                    TempData["ErrorMessage"] = "Selected cards could not be found.";
                    return RedirectToAction(nameof(Index), new { search, sport, status, platform });
                }

                // Validate — block on errors, show warnings but proceed
                var allErrors = _exportService.ValidateBatch(selected, exportPlatform);
                var errors = allErrors.Where(e => e.Severity == ExportErrorSeverity.Error).ToList();

                if (errors.Count > 0)
                {
                    var cards = await LoadFilteredAsync(search, sport, status);
                    var vm = new ExportListViewModel
                    {
                        Cards = cards,
                        SearchQuery = search,
                        SelectedSport = sport,
                        SelectedStatus = status,
                        SelectedPlatform = platform,
                        ValidationErrors = errors,
                        ValidationWarnings = allErrors.Where(e => e.Severity == ExportErrorSeverity.Warning).ToList(),
                    };
                    TempData["ErrorMessage"] = $"{errors.Count} card(s) have errors that must be fixed before export.";
                    return View(nameof(Index), vm);
                }

                // Write to temp, stream to browser, delete temp
                var tempPath = Path.Combine(Path.GetTempPath(), $"flipkit-export-{Guid.NewGuid():N}.csv");
                try
                {
                    await _exportService.ExportCsvAsync(selected, tempPath, exportPlatform);
                    var bytes = await System.IO.File.ReadAllBytesAsync(tempPath);
                    var filename = $"FlipKit-{exportPlatform}-{DateTime.Now:yyyyMMdd-HHmmss}.csv";

                    // Promote exported cards to Listed
                    foreach (var card in selected.Where(c => c.Status != CardStatus.Sold))
                    {
                        card.Status = CardStatus.Listed;
                        card.UpdatedAt = DateTime.UtcNow;
                        await _cardRepository.UpdateCardAsync(card);
                    }

                    var warnings = allErrors.Where(e => e.Severity == ExportErrorSeverity.Warning).ToList();
                    if (warnings.Count > 0)
                        TempData["WarningMessage"] = $"Export complete with {warnings.Count} warning(s). Review the exported file.";
                    else
                        TempData["SuccessMessage"] = $"Exported {selected.Count} card(s) to {exportPlatform}. Status promoted to Listed.";

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
                _logger.LogError(ex, "Error during CSV export");
                TempData["ErrorMessage"] = "Export failed. Please try again.";
                return RedirectToAction(nameof(Index), new { search, sport, status, platform });
            }
        }

        private async Task<List<Card>> LoadFilteredAsync(string? search, string sport, string status)
        {
            CardStatus? statusFilter = status != "All" && Enum.TryParse<CardStatus>(status, out var s) ? s : null;
            Sport? sportFilter = sport != "All" && Enum.TryParse<Sport>(sport, out var sp) ? sp : null;

            var cards = await _cardRepository.GetAllCardsAsync(statusFilter, sportFilter);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                cards = cards.Where(c =>
                    (c.PlayerName?.ToLower().Contains(lower) ?? false) ||
                    (c.Brand?.ToLower().Contains(lower) ?? false) ||
                    (c.Team?.ToLower().Contains(lower) ?? false) ||
                    (c.SetName?.ToLower().Contains(lower) ?? false)
                ).ToList();
            }

            return cards.OrderBy(c => c.PlayerName).ToList();
        }
    }
}
