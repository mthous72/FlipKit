using System.Collections.Generic;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Models
{
    public class VerificationResult
    {
        public VerificationConfidence OverallConfidence { get; set; }
        public List<FieldConfidence> FieldConfidences { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Suggestions { get; set; } = new();
        public string? SuggestedVariation { get; set; }
        public string? SuggestedPlayerName { get; set; }
        public bool ChecklistMatch { get; set; }
        public bool PlayerVerified { get; set; }
        public bool VariationVerified { get; set; }
        public bool CardNumberVerified { get; set; }
    }
}
