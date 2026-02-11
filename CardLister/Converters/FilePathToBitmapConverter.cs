using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Serilog;

namespace FlipKit.Desktop.Converters
{
    /// <summary>
    /// Converts file paths to thumbnail bitmaps with caching for improved DataGrid scrolling performance.
    /// </summary>
    public class FilePathToBitmapConverter : IValueConverter
    {
        public static readonly FilePathToBitmapConverter Instance = new();

        // Cache bitmaps to avoid reloading on scroll (max 200 thumbnails ~6MB)
        private static readonly Dictionary<string, Bitmap?> _cache = new();
        private const int MaxCacheSize = 200;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrWhiteSpace(path))
                return null;

            // Check cache first (fast path)
            if (_cache.TryGetValue(path, out var cachedBitmap))
                return cachedBitmap;

            // File doesn't exist
            if (!File.Exists(path))
            {
                _cache[path] = null; // Cache miss to avoid repeated File.Exists checks
                return null;
            }

            try
            {
                // Load image as thumbnail to save memory (28x28 in UI, decode at 56x56 for quality)
                using var stream = File.OpenRead(path);
                var bitmap = Bitmap.DecodeToWidth(stream, 56);

                // Add to cache (with simple LRU eviction)
                if (_cache.Count >= MaxCacheSize)
                {
                    // Remove first cached item (simple but effective)
                    var firstKey = string.Empty;
                    foreach (var key in _cache.Keys)
                    {
                        firstKey = key;
                        break;
                    }
                    if (!string.IsNullOrEmpty(firstKey))
                    {
                        _cache[firstKey]?.Dispose();
                        _cache.Remove(firstKey);
                    }
                }

                _cache[path] = bitmap;
                return bitmap;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load thumbnail from {Path}", path);
                _cache[path] = null; // Cache failure to avoid retry spam
                return null;
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Clears the bitmap cache (call when memory pressure is high).
        /// </summary>
        public static void ClearCache()
        {
            foreach (var bitmap in _cache.Values)
            {
                bitmap?.Dispose();
            }
            _cache.Clear();
        }
    }
}
