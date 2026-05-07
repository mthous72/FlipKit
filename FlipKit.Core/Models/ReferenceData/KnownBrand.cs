using System.Collections.Generic;

namespace FlipKit.Core.Models.ReferenceData
{
    /// <summary>
    /// Reference row for a card brand line ("Mosaic", "Prizm", "Topps Chrome").
    /// Seeded from <c>brands.json</c>. Brands change more often than teams or
    /// manufacturers (Topps drops several new brands per year), so this list
    /// drifts faster than the others — but it's still small enough to ship.
    /// </summary>
    public class KnownBrand
    {
        public int Id { get; set; }

        /// <summary>Canonical brand name ("Prizm", "Topps Chrome").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>The manufacturer that prints this brand ("Panini", "Topps").</summary>
        public string Manufacturer { get; set; } = string.Empty;

        /// <summary>Sports this brand prints in (empty = all major sports).</summary>
        public List<string> Sports { get; set; } = new();
    }
}
