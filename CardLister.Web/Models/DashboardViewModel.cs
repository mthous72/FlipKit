namespace FlipKit.Web.Models
{
    /// <summary>
    /// View model for the home dashboard displaying inventory summary statistics.
    /// </summary>
    public class DashboardViewModel
    {
        public int TotalCards { get; set; }
        public int DraftCards { get; set; }
        public int PricedCards { get; set; }
        public int ReadyCards { get; set; }
        public int ListedCards { get; set; }
        public int SoldCards { get; set; }
        public decimal TotalValue { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
