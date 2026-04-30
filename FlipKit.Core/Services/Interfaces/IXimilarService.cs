using System.Threading.Tasks;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    public interface IXimilarService
    {
        /// <summary>
        /// Recognize a card using Ximilar Collectibles Recognition API.
        /// </summary>
        /// <param name="imagePath">Path to the card image</param>
        /// <param name="useMagicAi">If true, uses extra tokens for newer/short print cards</param>
        Task<XimilarResult?> RecognizeCardAsync(string imagePath, bool useMagicAi = false);
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
