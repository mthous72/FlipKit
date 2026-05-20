namespace FlipKit.Core.Models.Enums
{
    // Mirrors CardSight's "confidence" string field on identification results.
    // High = 90-100%, Medium = 75-89%, Low = 50-74%.
    public enum CardsightConfidenceTier
    {
        Low = 0,
        Medium = 1,
        High = 2
    }
}
