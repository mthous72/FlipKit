using System.Collections.Generic;

namespace FlipKit.Core.Models.ReferenceData
{
    /// <summary>
    /// Reference row for a card manufacturer. Seeded from
    /// <c>manufacturers.json</c>. Distinct from <c>SetChecklist.Manufacturer</c>
    /// (which is per-set context) — this is the bootstrap list of who prints
    /// cards at all, used by the OCR pipeline before any checklists are
    /// imported.
    /// </summary>
    public class KnownManufacturer
    {
        public int Id { get; set; }

        /// <summary>Canonical manufacturer name as printed on cards ("Panini").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Sports this manufacturer prints in (e.g. ["Football", "Basketball"]).
        /// Empty when the manufacturer prints across all major sports.
        /// </summary>
        public List<string> SportsActive { get; set; } = new();

        /// <summary>Common alternate spellings / OCR-failure patterns ("Pannini").</summary>
        public List<string> Aliases { get; set; } = new();
    }
}
