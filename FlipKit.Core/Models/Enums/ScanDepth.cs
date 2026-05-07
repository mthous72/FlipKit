namespace FlipKit.Core.Models.Enums
{
    public enum ScanDepth
    {
        /// <summary>
        /// Minimal identification: player name, year, set name, graded status.
        /// Faster and cheaper — intended for Surprise Set lot scanning where full
        /// card detail can be filled in later.
        /// </summary>
        Quick,

        /// <summary>
        /// Full card identification including parallels, serial numbers, visual cues,
        /// and confidence scores. Default for single-card and standard bulk scans.
        /// </summary>
        Standard,
    }
}
