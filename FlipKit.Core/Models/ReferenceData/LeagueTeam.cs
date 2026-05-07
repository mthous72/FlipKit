using System.Collections.Generic;

namespace FlipKit.Core.Models.ReferenceData
{
    /// <summary>
    /// Reference row for a sports team. Seeded at first run from the bundled
    /// <c>leagues_teams.json</c> resource and read by the OCR pipeline to
    /// (a) reject team names from player-name candidates, and (b) infer the
    /// card's <c>Sport</c> when a team match is found. Data, not logic — new
    /// leagues / relocations are JSON edits, never code changes.
    /// </summary>
    public class LeagueTeam
    {
        public int Id { get; set; }

        /// <summary>The sport this team plays — drives <c>Card.Sport</c> when matched.</summary>
        public string Sport { get; set; } = string.Empty;

        /// <summary>The full canonical name as printed on cards ("Atlanta Falcons").</summary>
        public string TeamName { get; set; } = string.Empty;

        /// <summary>City / region word(s) for partial matching ("Atlanta").</summary>
        public string City { get; set; } = string.Empty;

        /// <summary>The team-only word ("Falcons") — usually the last token of TeamName.</summary>
        public string Mascot { get; set; } = string.Empty;

        /// <summary>
        /// Common alternative names: nicknames ("Bucs"), historical names
        /// ("Football Team", "Redskins"), market abbreviations. Used when the
        /// OCR'd line doesn't match the canonical TeamName / City / Mascot.
        /// JSON-serialized list to keep the schema flat.
        /// </summary>
        public List<string> Aliases { get; set; } = new();
    }
}
