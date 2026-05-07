using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using Microsoft.Extensions.Logging;

namespace FlipKit.Desktop.Services
{
    public class WindowsOcrService : IOcrService
    {
        private readonly ILogger<WindowsOcrService>? _logger;

        public WindowsOcrService(ILogger<WindowsOcrService>? logger = null)
        {
            _logger = logger;
        }

        public bool IsAvailable
        {
            get
            {
                if (!OperatingSystem.IsWindowsVersionAtLeast(10))
                    return false;
                try
                {
                    var lang = new Windows.Globalization.Language("en");
                    return Windows.Media.Ocr.OcrEngine.IsLanguageSupported(lang);
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task<ScanResult> ScanCardAsync(string imagePath, string? backImagePath = null)
        {
            var allText = new List<string>();

            allText.AddRange(await RunOcrOnImageAsync(imagePath));

            if (!string.IsNullOrEmpty(backImagePath) && File.Exists(backImagePath))
                allText.AddRange(await RunOcrOnImageAsync(backImagePath));

            var (card, confidences) = OcrTextParser.Parse(allText);
            card.ImagePathFront = imagePath;
            card.ImagePathBack = backImagePath;
            card.DataSource = CardDataSource.Ocr;
            card.Status = CardStatus.Draft;

            return new ScanResult
            {
                Card = card,
                AllVisibleText = allText,
                Confidences = confidences,
                VisualCues = null,
            };
        }

        private async Task<List<string>> RunOcrOnImageAsync(string imagePath)
        {
            var lines = new List<string>();
            string? preprocessedPath = null;

            try
            {
                preprocessedPath = OcrImagePreprocessor.Preprocess(imagePath);
                var pathToLoad = preprocessedPath != imagePath ? preprocessedPath : imagePath;

                var imageBytes = await File.ReadAllBytesAsync(pathToLoad);

                using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                await stream.WriteAsync(imageBytes.AsBuffer());
                stream.Seek(0);

                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                var bitmap = await decoder.GetSoftwareBitmapAsync(
                    Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                    Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);

                var lang = new Windows.Globalization.Language("en");
                var engine = Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(lang);
                if (engine == null)
                    return lines;

                var result = await engine.RecognizeAsync(bitmap);
                foreach (var line in result.Lines)
                    lines.Add(line.Text);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "OCR failed for image {Path}", imagePath);
            }
            finally
            {
                if (preprocessedPath != null && preprocessedPath != imagePath
                    && File.Exists(preprocessedPath))
                {
                    try { File.Delete(preprocessedPath); } catch { /* best effort */ }
                }
            }

            return lines;
        }
    }
}
