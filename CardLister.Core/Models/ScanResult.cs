using System.Collections.Generic;

namespace FlipKit.Core.Models
{
    public class ScanResult
    {
        public Card Card { get; set; } = new();
        public VisualCues? VisualCues { get; set; }
        public List<string> AllVisibleText { get; set; } = new();
        public List<FieldConfidence> Confidences { get; set; } = new();
    }
}
