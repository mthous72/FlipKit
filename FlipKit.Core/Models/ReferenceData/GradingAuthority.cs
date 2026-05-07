namespace FlipKit.Core.Models.ReferenceData
{
    /// <summary>
    /// Reference row for a card-grading service (PSA, BGS, CGC, SGC, etc.).
    /// Seeded from <c>grading_authorities.json</c>. Drives OCR grade detection,
    /// the eBay-export grading-company mapping, and any future grade-scale
    /// validation. Stable list — these companies don't change often.
    /// </summary>
    public class GradingAuthority
    {
        public int Id { get; set; }

        /// <summary>Short code as printed on slabs ("PSA", "BGS", "CGC").</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>Full company name ("Professional Sports Authenticator").</summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>Lowest valid numeric grade (always 1 for these scales).</summary>
        public decimal MinGrade { get; set; } = 1m;

        /// <summary>Highest valid numeric grade (always 10).</summary>
        public decimal MaxGrade { get; set; } = 10m;

        /// <summary>Grade increment (0.5 for half-points, 1 for whole-only).</summary>
        public decimal GradeIncrement { get; set; } = 0.5m;

        /// <summary>True when the authority publishes per-attribute subgrades (BGS, CGC).</summary>
        public bool HasSubgrades { get; set; }

        /// <summary>True when this authority is no longer issuing new grades (e.g. CSG, BCCG).</summary>
        public bool IsActive { get; set; } = true;
    }
}
