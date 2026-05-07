using System.Threading.Tasks;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    public interface IOcrService
    {
        bool IsAvailable { get; }
        Task<ScanResult> ScanCardAsync(string imagePath, string? backImagePath = null);
    }
}
