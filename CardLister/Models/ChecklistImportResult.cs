namespace CardLister.Models
{
    public class ChecklistImportResult
    {
        public bool Success { get; set; }
        public int CardsAdded { get; set; }
        public int VariationsAdded { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
