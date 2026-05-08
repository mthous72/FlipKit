using System.Collections.Generic;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Returns the list of plausible parallel names a card could have, given what we
    /// know so far (manufacturer / brand / year / sport). This list is used in two
    /// places that must stay aligned:
    ///   1. the LLM prompt preamble — anchors the model so it picks from a real
    ///      list instead of inventing names ("Sparkly Glittery Diamond")
    ///   2. the response_format json_schema enum — hard-stops the model from
    ///      emitting an off-list name at all
    /// Returning the same list to both callsites is the whole point.
    /// </summary>
    public interface IParallelCandidateProvider
    {
        /// <summary>
        /// Candidate parallel names ordered by specificity:
        /// known-set parallels (from <c>ParallelFamilyCatalog.json</c>) first,
        /// then manufacturer-wide entries from <c>parallels.json</c>,
        /// then universal color/finish names.
        /// Returns an empty list only when nothing is known about the card AT ALL —
        /// callers can use this to skip the enum constraint.
        /// </summary>
        /// <param name="manufacturer">Card manufacturer (Panini / Topps / etc.). When
        /// null but <paramref name="brand"/> is set, the provider resolves the
        /// brand via <see cref="FlipKit.Core.Helpers.BrandManufacturerMap"/>.</param>
        /// <param name="brand">Card brand (Prizm / Mosaic / Bowman / etc.). Used
        /// alongside <paramref name="year"/> to look up the richest known list.</param>
        /// <param name="year">Release year — required for the per-set lookup.</param>
        /// <param name="sport">Sport — narrows the per-set lookup when multiple
        /// brand/year entries exist (e.g. Mosaic Football vs Mosaic Basketball).</param>
        IReadOnlyList<string> GetCandidates(string? manufacturer, string? brand, int? year, string? sport);
    }
}
