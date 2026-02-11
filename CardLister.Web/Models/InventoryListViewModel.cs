using FlipKit.Core.Models;

namespace FlipKit.Web.Models
{
    /// <summary>
    /// View model for the inventory list page with filtering and pagination.
    /// </summary>
    public class InventoryListViewModel
    {
        public List<Card> Cards { get; set; } = new();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; }
        public int PageSize { get; set; } = 20;
        public string? SearchQuery { get; set; }
        public string SelectedSport { get; set; } = "All";
        public string SelectedStatus { get; set; } = "All";

        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }
}
