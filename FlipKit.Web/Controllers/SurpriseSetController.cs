using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Scanning;
using FlipKit.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FlipKit.Web.Controllers
{
    public class SurpriseSetController : Controller
    {
        private readonly ISurpriseSetRepository _repository;
        private readonly ISurpriseSetValidator _validator;
        private readonly ISurpriseSetCsvExporter _csvExporter;
        private readonly ISurpriseSetCompletionService _completionService;
        private readonly IScannerService _scanner;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SurpriseSetController> _logger;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public SurpriseSetController(
            ISurpriseSetRepository repository,
            ISurpriseSetValidator validator,
            ISurpriseSetCsvExporter csvExporter,
            ISurpriseSetCompletionService completionService,
            IScannerService scanner,
            IMemoryCache cache,
            ILogger<SurpriseSetController> logger)
        {
            _repository = repository;
            _validator = validator;
            _csvExporter = csvExporter;
            _completionService = completionService;
            _scanner = scanner;
            _cache = cache;
            _logger = logger;
        }

        // GET /SurpriseSet/
        public async Task<IActionResult> Index()
        {
            var sets = await _repository.GetAllAsync();
            return View(new SurpriseSetIndexViewModel
            {
                Sets = sets,
                StatusMessage = TempData["StatusMessage"] as string,
            });
        }

        // GET /SurpriseSet/Detail/5
        public async Task<IActionResult> Detail(int id)
        {
            var set = await _repository.GetByIdWithCardsAsync(id);
            if (set == null) return NotFound();

            var cards = set.Cards.OrderBy(c => c.SurpriseSetSlot ?? int.MaxValue).ToList();
            var issues = _validator.Validate(set, cards);

            return View(new SurpriseSetDetailViewModel
            {
                Set = set,
                Cards = cards,
                Issues = issues.ToList(),
                HasErrors = issues.Any(i => i.Severity == IssueSeverity.Error),
                StatusMessage = TempData["StatusMessage"] as string,
            });
        }

        // GET /SurpriseSet/Create
        public IActionResult Create() => View(new SurpriseSetCreateViewModel());

        // POST /SurpriseSet/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SurpriseSetCreateViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                model.Error = "Set name is required.";
                return View(model);
            }

            var set = new SurpriseSet
            {
                Name = model.Name.Trim(),
                ShowName = string.IsNullOrWhiteSpace(model.ShowName) ? null : model.ShowName.Trim(),
                Title = string.IsNullOrWhiteSpace(model.Title) ? model.Name.Trim() : model.Title.Trim(),
                SpotPrice = model.SpotPrice,
                SharedCondition = model.SharedCondition,
                SharedShippingProfile = model.SharedShippingProfile ?? string.Empty,
                SharedWhatnotCategory = string.IsNullOrWhiteSpace(model.SharedWhatnotCategory)
                    ? "Sports Trading Cards" : model.SharedWhatnotCategory.Trim(),
                SharedListingType = "Buy it Now",
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                State = SurpriseSetState.Draft,
            };

            try
            {
                await _repository.InsertAsync(set);
                TempData["StatusMessage"] = $"Created \"{set.Name}\".";
                return RedirectToAction(nameof(Detail), new { id = set.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create surprise set");
                model.Error = "Could not create set. Please try again.";
                return View(model);
            }
        }

        // POST /SurpriseSet/Export/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Export(int id)
        {
            var set = await _repository.GetByIdAsync(id);
            if (set == null) return NotFound();

            var tempPath = Path.Combine(Path.GetTempPath(), $"flipkit-set-{id}-{Guid.NewGuid():N}.csv");
            try
            {
                var result = await _csvExporter.ExportAsync(id, tempPath);
                if (!result.Success)
                {
                    TempData["StatusMessage"] = "Export blocked: " +
                        string.Join("; ", result.BlockingIssues.Select(i => i.Message));
                    return RedirectToAction(nameof(Detail), new { id });
                }

                var bytes = await System.IO.File.ReadAllBytesAsync(tempPath);
                var fileName = $"{set.Name.Replace(" ", "-")}-surprise-set.csv";
                return File(bytes, "text/csv", fileName);
            }
            finally
            {
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
        }

        // POST /SurpriseSet/RemoveCard/5?cardId=42
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveCard(int id, int cardId)
        {
            try
            {
                await _repository.RemoveCardAsync(id, cardId);
                TempData["StatusMessage"] = "Card removed from set.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove card {CardId} from set {SetId}", cardId, id);
                TempData["StatusMessage"] = $"Could not remove card: {ex.Message}";
            }
            return RedirectToAction(nameof(Detail), new { id });
        }

        // POST /SurpriseSet/Complete/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id, SurpriseSetCompleteFormModel form)
        {
            var result = await _completionService.CompleteAsync(id, new CompleteSetRequest
            {
                SpotsSold = form.SpotsSold,
                GrossRevenue = form.GrossRevenue,
                TotalFees = form.TotalFees,
                TotalShipping = form.TotalShipping,
            });

            TempData["StatusMessage"] = result.Success
                ? $"Set marked Completed — {result.Allocations.Count(a => a.IsSold)} cards sold."
                : $"Error: {result.ErrorMessage}";

            return RedirectToAction(nameof(Detail), new { id });
        }

        // GET /SurpriseSet/BulkScan/5
        public async Task<IActionResult> BulkScan(int id)
        {
            var set = await _repository.GetByIdAsync(id);
            if (set == null) return NotFound();

            if (set.State is SurpriseSetState.Live or SurpriseSetState.Completed or SurpriseSetState.Cancelled)
            {
                TempData["StatusMessage"] = $"Cannot scan into set in state {set.State}.";
                return RedirectToAction(nameof(Detail), new { id });
            }

            return View(new SurpriseSetBulkScanViewModel { Set = set });
        }

        // POST /SurpriseSet/BulkScanUpload/5  — stores images, returns jobId
        [HttpPost]
        public async Task<IActionResult> BulkScanUpload(int id, IList<Microsoft.AspNetCore.Http.IFormFile> images)
        {
            if (images == null || images.Count == 0)
                return BadRequest(new { error = "No images uploaded." });

            var set = await _repository.GetByIdAsync(id);
            if (set == null) return NotFound(new { error = "Set not found." });

            var tempDir = Path.Combine(Path.GetTempPath(), "flipkit-bulkscan", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var paths = new List<string>();
            foreach (var file in images)
            {
                var ext = Path.GetExtension(file.FileName);
                var dest = Path.Combine(tempDir, $"{paths.Count:D3}{ext}");
                await using var fs = System.IO.File.Create(dest);
                await file.CopyToAsync(fs);
                paths.Add(dest);
            }

            var jobId = Guid.NewGuid().ToString("N");
            var job = new BulkScanJob { SetId = id, ImagePaths = paths.ToArray() };
            _cache.Set($"bulkscan:{jobId}", job, TimeSpan.FromMinutes(30));

            return Ok(new { jobId });
        }

        // GET /SurpriseSet/BulkScanStream/{jobId}  — SSE endpoint
        [HttpGet]
        public async Task BulkScanStream(string jobId, CancellationToken ct)
        {
            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            if (!_cache.TryGetValue($"bulkscan:{jobId}", out BulkScanJob? job) || job == null)
            {
                await WriteEvent(Response, "error", new { message = "Job not found." }, ct);
                return;
            }

            var total = job.ImagePaths.Length;
            var results = new List<BulkScanJobResult>();

            for (int i = 0; i < total && !ct.IsCancellationRequested; i++)
            {
                var imagePath = job.ImagePaths[i];

                await WriteEvent(Response, "progress", new { index = i, total, status = "scanning" }, ct);

                BulkScanJobResult result;
                try
                {
                    var scanResult = await _scanner.ScanCardAsync(
                        imagePath,
                        scanDepth: ScanDepth.Quick);

                    result = new BulkScanJobResult
                    {
                        Index = i,
                        Success = true,
                        PlayerName = scanResult.Card?.PlayerName,
                        CardNumber = scanResult.Card?.CardNumber,
                        Year = scanResult.Card?.Year,
                        Manufacturer = scanResult.Card?.Manufacturer,
                        Brand = scanResult.Card?.Brand,
                        SetName = scanResult.Card?.SetName,
                        Team = scanResult.Card?.Team,
                        Sport = scanResult.Card?.Sport?.ToString(),
                        VariationType = scanResult.Card?.VariationType,
                        ParallelName = scanResult.Card?.ParallelName,
                        SerialNumbered = scanResult.Card?.SerialNumbered,
                        IsRookie = scanResult.Card?.IsRookie ?? false,
                        IsAuto = scanResult.Card?.IsAuto ?? false,
                        IsRelic = scanResult.Card?.IsRelic ?? false,
                        IsGraded = scanResult.Card?.IsGraded ?? false,
                        GradeCompany = scanResult.Card?.GradeCompany,
                        GradeValue = scanResult.Card?.GradeValue,
                        Condition = scanResult.Card?.Condition ?? "Near Mint",
                        ImagePath = imagePath,
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Scan failed for image {Index}", i);
                    result = new BulkScanJobResult
                    {
                        Index = i,
                        Success = false,
                        Error = ex.Message,
                        ImagePath = imagePath,
                    };
                }

                results.Add(result);
                await WriteEvent(Response, "result", result, ct);
            }

            job.Results = results;
            _cache.Set($"bulkscan:{jobId}", job, TimeSpan.FromMinutes(15));

            await WriteEvent(Response, "complete", new { total, scanned = results.Count }, ct);
        }

        // POST /SurpriseSet/BulkScanSave/5  — saves all scanned results into the set
        [HttpPost]
        public async Task<IActionResult> BulkScanSave(int id, [FromBody] BulkScanSaveRequest request)
        {
            if (request?.JobId == null)
                return BadRequest(new { error = "Missing jobId." });

            if (!_cache.TryGetValue($"bulkscan:{request.JobId}", out BulkScanJob? job) || job == null)
                return BadRequest(new { error = "Job expired or not found." });

            var saved = 0;
            foreach (var result in job.Results.Where(r => r.Success))
            {
                try
                {
                    var card = BuildCard(result);
                    await _repository.AddCardAsync(id, card);
                    saved++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save scanned card at index {Index}", result.Index);
                }
            }

            _cache.Remove($"bulkscan:{request.JobId}");

            return Ok(new { saved, redirectUrl = Url.Action(nameof(Detail), new { id }) });
        }

        // === helpers ===

        private static Card BuildCard(BulkScanJobResult r) => new()
        {
            PlayerName = r.PlayerName ?? string.Empty,
            CardNumber = r.CardNumber,
            Year = r.Year,
            Manufacturer = r.Manufacturer,
            Brand = r.Brand,
            SetName = r.SetName,
            Team = r.Team,
            Sport = Enum.TryParse<Sport>(r.Sport, out var s) ? s : null,
            VariationType = r.VariationType ?? "Base",
            ParallelName = r.ParallelName,
            SerialNumbered = r.SerialNumbered,
            IsRookie = r.IsRookie,
            IsAuto = r.IsAuto,
            IsRelic = r.IsRelic,
            IsGraded = r.IsGraded,
            GradeCompany = r.GradeCompany,
            GradeValue = r.GradeValue,
            Condition = r.Condition,
            CostBasis = r.CostBasis,
            Notes = r.Notes,
            Status = CardStatus.ReservedForSet,
            ImagePathFront = r.ImagePath,
        };

        private static async Task WriteEvent(
            Microsoft.AspNetCore.Http.HttpResponse response,
            string eventType,
            object data,
            CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(data, _json);
            var line = $"event: {eventType}\ndata: {json}\n\n";
            await response.WriteAsync(line, ct);
            await response.Body.FlushAsync(ct);
        }
    }

    public class BulkScanSaveRequest
    {
        public string? JobId { get; set; }
    }
}
