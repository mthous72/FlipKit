using System.Collections.Generic;

namespace FlipKit.Core.Models.ReferenceData
{
    /// <summary>
    /// Reference row for a parallel finish or insert subset name. Seeded from
    /// <c>parallels.json</c>. Complementary to the per-set
    /// <c>ParallelFamilyCatalog.json</c>: that file lists every parallel for
    /// a specific Year+Brand+Sport, while this table holds universal finish /
    /// insert keywords (Refractor, Silver, Gold, Future Stars, Diamond Kings)
    /// that recur across sets — used by the OCR parser to recognize
    /// ParallelName / Insert tokens regardless of which set the card is from.
    /// </summary>
    public class KnownVariation
    {
        public int Id { get; set; }

        /// <summary>Canonical display name ("Refractor", "Future Stars").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>"Parallel" (color/finish variant of the same card) or "Insert" (themed subset).</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Manufacturer that originated / primarily uses this variation.
        /// Empty when the variation is universal across manufacturers
        /// (e.g. "Silver" / "Gold" appear everywhere).
        /// </summary>
        public string Manufacturer { get; set; } = string.Empty;

        /// <summary>Sports the variation appears in. Empty = all sports.</summary>
        public List<string> Sports { get; set; } = new();
    }
}
