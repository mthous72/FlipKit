using System;
using System.Collections.Generic;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Models
{
    public class SurpriseSet
    {
        public int Id { get; set; }

        // === IDENTITY ===
        public string Name { get; set; } = string.Empty;
        public string? ShowName { get; set; }
        public string? Notes { get; set; }

        // === LIFECYCLE ===
        public SurpriseSetState State { get; set; } = SurpriseSetState.Draft;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? ExportedAt { get; set; }
        public DateTime? LiveAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? CancelledAt { get; set; }

        // === SHARED LISTING FIELDS ===
        // These are stamped onto every CSV row at export time.
        // Whatnot's consistency rule is enforced by construction — not post-hoc validation.
        public string Title { get; set; } = string.Empty;
        public string SharedListingType { get; set; } = "Buy it Now";
        public decimal SpotPrice { get; set; }
        public string SharedCondition { get; set; } = string.Empty;
        public string SharedShippingProfile { get; set; } = string.Empty;
        public string SharedWhatnotCategory { get; set; } = "Sports Trading Cards";
        public string? SharedWhatnotSubcategory { get; set; }
        public bool Offerable { get; set; } = false;
        // Gallery images for the listing (not per-card photos — buyer doesn't see
        // individual cards until after shipping).
        public string? SharedImageUrl1 { get; set; }
        public string? SharedImageUrl2 { get; set; }
        public string? SharedImageUrl3 { get; set; }
        public string? SharedImageUrl4 { get; set; }
        public string? SharedImageUrl5 { get; set; }
        public string? SharedImageUrl6 { get; set; }
        public string? SharedImageUrl7 { get; set; }
        public string? SharedImageUrl8 { get; set; }

        // === ECONOMICS ===
        public RevenueAllocationMethod AllocationMethod { get; set; } = RevenueAllocationMethod.EqualSplit;
        // Optional total cost paid for all cards as a lot. Auto-split evenly across cards
        // (CostSource = LotSplit). Per-card overrides are preserved on re-balance.
        public decimal? LotCostBasis { get; set; }
        // Completion fields — populated when marking the set as Completed.
        public int? SpotsSold { get; set; }
        public decimal? GrossRevenue { get; set; }
        public decimal? TotalFees { get; set; }
        public decimal? TotalShipping { get; set; }

        // === NAVIGATION ===
        public ICollection<Card> Cards { get; set; } = new List<Card>();
    }
}
