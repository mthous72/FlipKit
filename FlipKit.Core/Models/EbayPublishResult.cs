namespace FlipKit.Core.Models
{
    public class EbayPublishResult
    {
        public bool Success { get; set; }
        public string? ListingId { get; set; }
        public string? ListingUrl { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
