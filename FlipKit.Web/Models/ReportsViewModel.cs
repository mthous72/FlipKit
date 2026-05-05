using FlipKit.Core.Models;

namespace FlipKit.Web.Models
{
    public class ReportsViewModel
    {
        // Date range (defaults: 90 days ago → today)
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Inventory snapshot (all cards, status-independent)
        public int TotalCards { get; set; }
        public int DraftCards { get; set; }
        public int PricedCards { get; set; }
        public int ReadyCards { get; set; }
        public int ListedCards { get; set; }
        public int SoldCards { get; set; }
        public decimal TotalInventoryValue { get; set; }

        // Sales within the selected date range
        public List<Card> SoldInRange { get; set; } = new();
        public decimal TotalRevenue { get; set; }
        public decimal TotalCostBasis { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal AverageProfit { get; set; }

        // Monthly breakdown for the selected range
        public List<MonthlyBreakdown> MonthlyBreakdowns { get; set; } = new();

        // Top 10 sellers by net profit within range
        public List<Card> TopSellers { get; set; } = new();
    }

    public class MonthlyBreakdown
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthLabel => new DateTime(Year, Month, 1).ToString("MMM yyyy");
        public int Count { get; set; }
        public decimal Revenue { get; set; }
        public decimal CostBasis { get; set; }
        public decimal Profit { get; set; }
    }
}
