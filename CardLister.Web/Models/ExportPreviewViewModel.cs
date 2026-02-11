using FlipKit.Core.Models;

namespace FlipKit.Web.Models
{
    /// <summary>
    /// View model for previewing export data for a single card.
    /// </summary>
    public class ExportPreviewViewModel
    {
        public Card Card { get; set; } = new();
        public string GeneratedTitle { get; set; } = string.Empty;
        public string GeneratedDescription { get; set; } = string.Empty;
        public List<string> ValidationErrors { get; set; } = new();
    }
}
