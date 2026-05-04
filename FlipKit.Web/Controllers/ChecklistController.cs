using System;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Services;
using FlipKit.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FlipKit.Web.Controllers
{
    /// <summary>
    /// Handles the user-driven Checklist Insider .xlsx import flow on Web. Phase 1 surface
    /// A: a single-page upload form that runs parse + commit in one round-trip and surfaces
    /// the result. Mobile clients see iOS-aware help copy.
    /// </summary>
    public class ChecklistController : Controller
    {
        private const long MaxUploadBytes = 10 * 1024 * 1024; // 10 MB — xlsx files are tiny

        private readonly IChecklistImportService _importService;
        private readonly IChecklistLearningService _learningService;
        private readonly ILogger<ChecklistController> _logger;

        public ChecklistController(
            IChecklistImportService importService,
            IChecklistLearningService learningService,
            ILogger<ChecklistController> logger)
        {
            _importService = importService;
            _learningService = learningService;
            _logger = logger;
        }

        public IActionResult Index() => RedirectToAction(nameof(Import));

        [HttpGet]
        public IActionResult Import()
        {
            return View(new ChecklistImportViewModel
            {
                IsIosClient = IsIos(),
            });
        }

        [HttpPost]
        [RequestSizeLimit(MaxUploadBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
        public async Task<IActionResult> Import(ChecklistImportViewModel model)
        {
            model.IsIosClient = IsIos();

            if (model.UploadedFile == null || model.UploadedFile.Length == 0)
            {
                model.StatusMessage = "Please choose a .xlsx file before importing.";
                return View(model);
            }

            if (!model.UploadedFile.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                model.StatusMessage = "Only .xlsx files are supported.";
                return View(model);
            }

            try
            {
                using var stream = model.UploadedFile.OpenReadStream();
                var preview = _importService.Parse(stream, model.UploadedFile.FileName);

                // User-supplied metadata overrides whatever the parser auto-detected.
                if (model.Year.HasValue) preview.Metadata.Year = model.Year;
                if (!string.IsNullOrWhiteSpace(model.Sport)) preview.Metadata.Sport = model.Sport.Trim();
                if (!string.IsNullOrWhiteSpace(model.Manufacturer)) preview.Metadata.Manufacturer = model.Manufacturer.Trim();
                if (!string.IsNullOrWhiteSpace(model.Brand)) preview.Metadata.Brand = model.Brand.Trim();
                if (!string.IsNullOrWhiteSpace(model.SetName)) preview.Metadata.SetName = model.SetName.Trim();

                model.Preview = preview;

                if (!preview.IsValid)
                {
                    model.StatusMessage = "We couldn't determine the year or brand from this file. Fill in the metadata fields and try again.";
                    return View(model);
                }

                var commitResult = await _importService.CommitAsync(preview);
                model.CommitResult = commitResult;
                model.StatusMessage = commitResult.Success
                    ? (commitResult.ReplacedExisting
                        ? $"Replaced existing checklist — {commitResult.CardsImported} cards across {commitResult.SubsetCount} subsets."
                        : $"Imported {commitResult.CardsImported} cards across {commitResult.SubsetCount} subsets.")
                    : (commitResult.ErrorMessage ?? "Import failed.");

                if (commitResult.Success)
                    TempData["StatusMessage"] = model.StatusMessage;

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excel checklist import failed for {File}", model.UploadedFile?.FileName);
                model.StatusMessage = $"Import failed: {ex.Message}";
                return View(model);
            }
        }

        public async Task<IActionResult> List()
        {
            var all = await _learningService.GetAllChecklistsAsync();
            return View(all.OrderByDescending(c => c.Year).ThenBy(c => c.Brand).ToList());
        }

        private bool IsIos()
        {
            var ua = Request.Headers.UserAgent.ToString();
            return ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
                   || ua.Contains("iPad", StringComparison.OrdinalIgnoreCase)
                   || ua.Contains("iPod", StringComparison.OrdinalIgnoreCase);
        }
    }
}
