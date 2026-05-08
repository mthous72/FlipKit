namespace FlipKit.Core.Models.Enums
{
    // Outcome of a single scan attempt against an LLM. Quota / billing failures
    // (402, 429, user-cancelled) are deliberately absent — those don't say anything
    // about the model's accuracy and shouldn't penalize its score.
    public enum ScanOutcome
    {
        Success,
        ParseFailure,
        ModelError,
        Cancelled,
    }
}
