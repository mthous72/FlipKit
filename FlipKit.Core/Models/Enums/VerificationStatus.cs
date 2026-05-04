namespace FlipKit.Core.Models.Enums
{
    /// <summary>
    /// Records the tier outcome the user accepted when saving a Card. Set by the
    /// post-scan checklist verifier (Phase 2 of Roadmap 1) and surfaced in the
    /// editor as a coloured badge. Stored as a string in the database via
    /// <c>HasConversion&lt;string&gt;</c>.
    /// </summary>
    public enum VerificationStatus
    {
        /// <summary>
        /// Never run against a checklist. Either no checklist exists for the set or
        /// the card was created before the verifier ran. Default for legacy rows.
        /// </summary>
        NotChecked = 0,

        /// <summary>Tier 1: matcher returned an exact match, all field confidences ≥ 0.85, user saved as-is.</summary>
        Verified,

        /// <summary>Tier 2: card # + player matched but at least one field was uncertain; user reviewed and saved.</summary>
        BestGuess,

        /// <summary>User picked a different ChecklistCard from the picker than the matcher's top candidate.</summary>
        UserCorrected,

        /// <summary>Tier 3: no candidate accepted; saved with the AI's guess only.</summary>
        NoMatchFound,
    }
}
