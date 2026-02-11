using System;

namespace FlipKit.Core.Models
{
    public class MissingChecklist
    {
        public int Id { get; set; }
        public string Manufacturer { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public int Year { get; set; }
        public string? Sport { get; set; }
        public int HitCount { get; set; } = 1;
        public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    }
}
