using Microsoft.AspNetCore.Mvc;

namespace FlipKit.Web.Controllers
{
    /// <summary>
    /// Receives webcam-captured (or arbitrary) image blobs from the browser, writes
    /// them to <c>wwwroot/uploads/</c>, and returns the saved path so the caller can
    /// stuff it into a hidden form field for the next round-trip. Mirrors the
    /// Desktop-side <see cref="FlipKit.Desktop.Services.WebcamCaptureDialogService"/>
    /// behaviour but for the browser flow (Roadmap #2 / Docs/27-WEBCAM-CAPTURE-PLAN.md).
    /// </summary>
    [ApiController]
    [Route("api/cards")]
    public class ImageUploadController : ControllerBase
    {
        private const long MaxBlobBytes = 10 * 1024 * 1024; // 10 MB — same order as a 4K JPEG
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImageUploadController> _logger;

        public ImageUploadController(IWebHostEnvironment environment, ILogger<ImageUploadController> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public sealed record UploadResponse(string Path, string Url);

        [HttpPost("upload-image")]
        [RequestSizeLimit(MaxBlobBytes)]
        public async Task<IActionResult> UploadImage([FromForm] IFormFile? blob, CancellationToken ct)
        {
            if (blob is null || blob.Length == 0)
                return BadRequest(new { error = "No image blob received." });

            if (blob.Length > MaxBlobBytes)
                return BadRequest(new { error = $"Image too large ({blob.Length / 1024} KB > {MaxBlobBytes / 1024} KB)." });

            // Decide extension from the supplied filename or fall back to .jpg.
            // FormData blobs from canvas.toBlob default to filename "blob" so we
            // can't trust it; sniff the extension and otherwise default to JPEG.
            var ext = Path.GetExtension(blob.FileName);
            if (string.IsNullOrEmpty(ext) || Array.IndexOf(AllowedExtensions, ext.ToLowerInvariant()) < 0)
                ext = ".jpg";

            try
            {
                var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads");
                Directory.CreateDirectory(uploadsDir);

                var filename = $"webcam-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(uploadsDir, filename);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                    await blob.CopyToAsync(stream, ct);

                var url = $"/uploads/{filename}";
                _logger.LogInformation("Webcam upload: {SizeKB} KB → {Path}", blob.Length / 1024, fullPath);

                return Ok(new UploadResponse(fullPath, url));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save webcam upload");
                return StatusCode(500, new { error = $"Save failed: {ex.Message}" });
            }
        }
    }
}
