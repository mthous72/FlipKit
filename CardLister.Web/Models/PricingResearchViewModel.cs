using FlipKit.Core.Models;

namespace FlipKit.Web.Models
{
    /// <summary>
    /// View model for researching and setting card prices.
    /// </summary>
    public class PricingResearchViewModel
    {
        public Card Card { get; set; } = new();
        public string TerapeakUrl { get; set; } = string.Empty;
        public string EbaySoldUrl { get; set; } = string.Empty;
        public decimal? EstimatedValue { get; set; }
        public decimal? ListingPrice { get; set; }
        public decimal? SuggestedPrice { get; set; }
    }
}
