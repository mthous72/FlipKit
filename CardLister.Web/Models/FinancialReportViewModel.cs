namespace FlipKit.Web.Models
{
    /// <summary>
    /// View model for the financial report showing profitability analysis.
    /// </summary>
    public class FinancialReportViewModel
    {
        // Overall Financial Stats
        public decimal TotalInventoryValue { get; set; }
        public decimal TotalInventoryCost { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal OverallProfitMargin { get; set; }

        // Inventory Metrics
        public int ActiveCards { get; set; }
        public int SoldCards { get; set; }
        public decimal InventoryTurnoverRate { get; set; }

        // Breakdown by Sport
        public List<SportProfitability> ProfitBySport { get; set; } = new();
    }

    /// <summary>
    /// Profitability metrics for a specific sport.
    /// </summary>
    public class SportProfitability
    {
        public string Sport { get; set; } = string.Empty;
        public int TotalSales { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal AverageProfit { get; set; }
        public decimal ProfitMargin { get; set; }
    }
}
