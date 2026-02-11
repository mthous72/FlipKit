using FlipKit.Core.Models;

namespace FlipKit.Web.Models
{
    /// <summary>
    /// View model for the pricing list page showing cards that need pricing.
    /// </summary>
    public class PricingListViewModel
    {
        public List<Card> Cards { get; set; } = new();
    }
}
