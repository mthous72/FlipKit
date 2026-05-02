using System.Collections.Generic;
using System.Threading.Tasks;

namespace FlipKit.Core.Services
{
    public interface IImageUploadService
    {
        Task<string> UploadImageAsync(string imagePath, string? name = null);
        Task<(string? url1, string? url2)> UploadCardImagesAsync(string frontPath, string? backPath = null);

        /// <summary>
        /// Uploads each non-empty existing path in <paramref name="localPaths"/> in order.
        /// Returns a list of the same length where each entry is the resulting URL or null
        /// if the corresponding input was empty/missing/failed.
        /// </summary>
        Task<List<string?>> UploadCardImagesAsync(IList<string?> localPaths);
    }
}
