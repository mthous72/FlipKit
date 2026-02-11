using FlipKit.Core.Models;

namespace FlipKit.Web.Models
{
    /// <summary>
    /// View model for displaying scan results with verification.
    /// </summary>
    public class ScanResultViewModel
    {
        public Card? ScannedCard { get; set; }
        public string? FrontImagePath { get; set; }
        public string? BackImagePath { get; set; }
        public VerificationResult? VerificationResult { get; set; }
    }
}
