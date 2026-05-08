using System.Collections.Generic;

namespace FlipKit.Core.Models
{
    public class ScanResult
    {
        public Card Card { get; set; } = new();
        public VisualCues? VisualCues { get; set; }
        public List<string> AllVisibleText { get; set; } = new();
        public List<FieldConfidence> Confidences { get; set; } = new();

        // OpenRouter model id that produced this result (post auto-rotation, the
        // *winning* model for the scan). Populated by the scanner so VMs can
        // record scoreboard entries without re-deriving which model ran.
        public string? UsedModelId { get; set; }

        // Number of verified-fields hints the LLM disobeyed and the scanner had
        // to restore. Counts as drift penalty in the model scoreboard.
        public int DriftEventCount { get; set; }
    }
}
