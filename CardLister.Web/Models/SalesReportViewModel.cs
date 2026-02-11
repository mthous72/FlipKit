using FlipKit.Core.Models;

namespace FlipKit.Web.Models
{
    /// <summary>
    /// View model for the sales report showing sold cards within a date range.
    /// </summary>
    public class SalesReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<Card> SoldCards { get; set; } = new();
        public int TotalSales { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal AverageProfit { get; set; }
    }
}
