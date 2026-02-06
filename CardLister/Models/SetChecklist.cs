using System;
using System.Collections.Generic;

namespace CardLister.Models
{
    public class SetChecklist
    {
        public int Id { get; set; }
        public string Manufacturer { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public int Year { get; set; }
        public string? Sport { get; set; }
        public List<ChecklistCard> Cards { get; set; } = new();
        public List<string> KnownVariations { get; set; } = new();
        public int TotalBaseCards { get; set; }
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
        public string DataSource { get; set; } = "seed";
        public DateTime LastEnrichedAt { get; set; } = DateTime.MinValue;
    }
}
