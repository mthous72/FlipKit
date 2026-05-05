using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services.Export;

namespace FlipKit.Web.Models
{
    public class ExportListViewModel
    {
        public List<Card> Cards { get; set; } = new();

        // Filters
        public string? SearchQuery { get; set; }
        public string SelectedSport { get; set; } = "All";
        public string SelectedStatus { get; set; } = "Ready";

        // Export options
        public string SelectedPlatform { get; set; } = "Whatnot";

        // Validation results from last export attempt
        public IReadOnlyList<ExportRowError> ValidationErrors { get; set; } = Array.Empty<ExportRowError>();
        public IReadOnlyList<ExportRowError> ValidationWarnings { get; set; } = Array.Empty<ExportRowError>();
    }
}
