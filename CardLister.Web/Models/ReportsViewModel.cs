using FlipKit.Core.Models;

namespace FlipKit.Web.Models
{
    /// <summary>
    /// View model for the main reports dashboard.
    /// </summary>
    public class ReportsViewModel
    {
        // Inventory Statistics
        public int TotalCards { get; set; }
        public int DraftCards { get; set; }
        public int PricedCards { get; set; }
        public int ReadyCards { get; set; }
        public int ListedCards { get; set; }
        public int SoldCards { get; set; }

        // Financial Statistics
        public decimal TotalInventoryValue { get; set; }
        public decimal TotalCostBasis { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalProfit { get; set; }

        // Breakdowns
        public Dictionary<string, int> CardsBySport { get; set; } = new();
        public List<Card> RecentSales { get; set; } = new();
    }
}
