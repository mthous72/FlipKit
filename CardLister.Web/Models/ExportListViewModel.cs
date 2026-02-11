using FlipKit.Core.Models;

namespace FlipKit.Web.Models
{
    /// <summary>
    /// View model for the export list page showing cards ready for export.
    /// </summary>
    public class ExportListViewModel
    {
        public List<Card> ReadyCards { get; set; } = new();
        public List<Card> PricedCards { get; set; } = new();
    }
}
