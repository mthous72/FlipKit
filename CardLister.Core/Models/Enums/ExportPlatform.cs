namespace FlipKit.Core.Models.Enums
{
    /// <summary>
    /// Represents different listing platforms with distinct SEO requirements
    /// and title formatting conventions.
    /// </summary>
    public enum ExportPlatform
    {
        /// <summary>
        /// Generic/default platform with balanced title format
        /// </summary>
        Generic,

        /// <summary>
        /// Whatnot - Optimized for brand + player searches
        /// Buyers typically search: "Prizm CJ Stroud" or "Player Name Parallel"
        /// </summary>
        Whatnot,

        /// <summary>
        /// eBay - Optimized for manufacturer + detailed SEO
        /// Search algorithm heavily weights manufacturer and comprehensive details
        /// </summary>
        eBay,

        /// <summary>
        /// Check Out My Cards (COMC) - Consignment platform
        /// Requires specific categorization and format
        /// </summary>
        COMC
    }
}
