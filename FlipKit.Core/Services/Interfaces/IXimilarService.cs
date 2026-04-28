using System.Threading.Tasks;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    public interface IXimilarService
    {
        Task<XimilarResult?> RecognizeCardAsync(string imagePath);
        Task<bool> TestConnectionAsync(string apiKey);
        bool IsConfigured { get; }
    }

    public class XimilarResult
    {
        public bool Success { get; set; }
        public Card? Card { get; set; }
        public double Confidence { get; set; }
        public string? RawResponse { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
