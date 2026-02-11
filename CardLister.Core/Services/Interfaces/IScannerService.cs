using System.Threading.Tasks;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    public interface IScannerService
    {
        Task<ScanResult> ScanCardAsync(string imagePath, string? backImagePath = null, string model = "nvidia/nemotron-nano-12b-v2-vl:free");
        Task<string> SendCustomPromptAsync(string imagePath, string prompt, string? backImagePath = null, string model = "nvidia/nemotron-nano-12b-v2-vl:free");
    }
}
