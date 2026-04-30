namespace FlipKit.Core.Models.Enums
{
    /// <summary>
    /// Controls how Ximilar card recognition is used during scanning.
    /// </summary>
    public enum XimilarScanMode
    {
        /// <summary>
        /// Standard Ximilar recognition (default). Uses the card database for fast lookups.
        /// </summary>
        Standard,

        /// <summary>
        /// Ximilar with Magic AI enabled. Uses extra tokens for newer cards and short prints
        /// that may not be in the standard database.
        /// </summary>
        Magic,

        /// <summary>
        /// Skip Ximilar entirely and use OpenRouter LLM directly.
        /// </summary>
        Disabled
    }
}
