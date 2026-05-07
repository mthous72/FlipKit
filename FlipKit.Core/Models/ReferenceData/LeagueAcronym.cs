namespace FlipKit.Core.Models.ReferenceData
{
    /// <summary>
    /// Reference row mapping a league acronym to its sport. Seeded from
    /// <c>league_acronyms.json</c>. Used by OCR scanning and eBay title
    /// parsing to populate <c>Card.Sport</c> when the league code appears
    /// (e.g. "NFL" anywhere in OCR text → Sport.Football).
    /// </summary>
    public class LeagueAcronym
    {
        public int Id { get; set; }

        /// <summary>Short code as it appears on cards / titles ("NFL", "F1").</summary>
        public string Acronym { get; set; } = string.Empty;

        /// <summary>The sport this acronym maps to.</summary>
        public string Sport { get; set; } = string.Empty;

        /// <summary>Full league name ("National Football League") for UI / docs.</summary>
        public string FullName { get; set; } = string.Empty;
    }
}
