using System;

namespace FlipKit.Core.Models
{
    public class PriceHistory
    {
        public int Id { get; set; }
        public int CardId { get; set; }
        public decimal? EstimatedValue { get; set; }
        public decimal? ListingPrice { get; set; }
        public string? PriceSource { get; set; }
        public string? Notes { get; set; }
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Card Card { get; set; } = null!;
    }
}
