using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Serilog;

namespace FlipKit.Desktop.Converters
{
    /// <summary>
    /// Converts file paths or URLs to full-resolution bitmaps (no thumbnail scaling).
    /// Used in edit views where users need to see card details clearly.
    /// Supports both local file paths and ImgBB URLs.
    /// </summary>
    public class FilePathToFullSizeBitmapConverter : IValueConverter
    {
        public static readonly FilePathToFullSizeBitmapConverter Instance = new();
        private static readonly HttpClient _httpClient = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                // Check if it's a URL (ImgBB or other hosted image)
                if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    // Load from URL
                    var response = _httpClient.GetAsync(path).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        var stream = response.Content.ReadAsStreamAsync().Result;
                        return new Bitmap(stream);
                    }
                    return null;
                }

                // File doesn't exist
                if (!File.Exists(path))
                    return null;

                // Load from local file path (full-resolution, no DecodeToWidth)
                using var fileStream = File.OpenRead(path);
                return new Bitmap(fileStream);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load full-size image from {Path}", path);
                return null;
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
