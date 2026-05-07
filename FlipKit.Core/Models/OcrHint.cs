using System.Collections.Generic;

namespace FlipKit.Core.Models
{
    public class OcrHint
    {
        public string? PlayerName { get; set; }
        public int? Year { get; set; }
        public string? CardNumber { get; set; }
        public string? Manufacturer { get; set; }
        public string? Brand { get; set; }
        public string? SetName { get; set; }
        public List<string> AllVisibleText { get; set; } = new();
    }
}
