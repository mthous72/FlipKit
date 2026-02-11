using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Models
{
    public class FieldConfidence
    {
        public string FieldName { get; set; } = string.Empty;
        public string? Value { get; set; }
        public VerificationConfidence Confidence { get; set; }
        public string? Reason { get; set; }
    }
}
