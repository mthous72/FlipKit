using System;
using System.Collections.Generic;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Models
{
    public class Card
    {
        public int Id { get; set; }

        // === CARD IDENTITY ===
        public string PlayerName { get; set; } = string.Empty;
        public string? CardNumber { get; set; }
        public int? Year { get; set; }
        public Sport? Sport { get; set; }

        // === MANUFACTURER / SET ===
        public string? Manufacturer { get; set; }
        public string? Brand { get; set; }
        public string? SetName { get; set; }
        public string? Team { get; set; }

        // === VARIATION / PARALLEL ===
        public string VariationType { get; set; } = "Base";
        public string? ParallelName { get; set; }
        public string? SerialNumbered { get; set; }
        public bool IsShortPrint { get; set; }
        public bool IsSSP { get; set; }

        // === SPECIAL ATTRIBUTES ===
        public bool IsRookie { get; set; }
        public bool IsAuto { get; set; }
        public bool IsRelic { get; set; }

        // === CONDITION / GRADING ===
        public string Condition { get; set; } = "Near Mint";
        public bool IsGraded { get; set; }
        public string? GradeCompany { get; set; }
        public string? GradeValue { get; set; }
        public string? CertNumber { get; set; }
        public string? AutoGrade { get; set; }

        // === ACQUISITION / COST BASIS ===
        public decimal? CostBasis { get; set; }
        public CostSource? CostSource { get; set; }
        public DateTime? CostDate { get; set; }
        public string? CostNotes { get; set; }

        // === PRICING ===
        public decimal? EstimatedValue { get; set; }
        public string? PriceSource { get; set; }
        public DateTime? PriceDate { get; set; }
        public decimal? ListingPrice { get; set; }
        public int PriceCheckCount { get; set; }

        // === SALE INFORMATION ===
        public decimal? SalePrice { get; set; }
        public DateTime? SaleDate { get; set; }
        public string? SalePlatform { get; set; }
        public decimal? FeesPaid { get; set; }
        public decimal? ShippingCost { get; set; }
        public decimal? NetProfit { get; set; }

        // === LISTING SETTINGS ===
        public int Quantity { get; set; } = 1;
        public string ListingType { get; set; } = "Buy It Now";
        public bool Offerable { get; set; } = true;
        public string ShippingProfile { get; set; } = "4 oz";

        // === IMAGES ===
        // Slots 1 (front) and 2 (back) are captured at scan time and sent to the AI for
        // identification. Slots 3-8 are user-attached extras (condition shots, edge shots,
        // slab close-ups) — uploaded to ImgBB but never sent to the LLM.
        public string? ImagePathFront { get; set; }
        public string? ImagePathBack { get; set; }
        public string? ImagePath3 { get; set; }
        public string? ImagePath4 { get; set; }
        public string? ImagePath5 { get; set; }
        public string? ImagePath6 { get; set; }
        public string? ImagePath7 { get; set; }
        public string? ImagePath8 { get; set; }
        public string? ImageUrl1 { get; set; }
        public string? ImageUrl2 { get; set; }
        public string? ImageUrl3 { get; set; }
        public string? ImageUrl4 { get; set; }
        public string? ImageUrl5 { get; set; }
        public string? ImageUrl6 { get; set; }
        public string? ImageUrl7 { get; set; }
        public string? ImageUrl8 { get; set; }

        // === EXPORT IDENTIFIERS ===
        public string? Sku { get; set; }

        // === MARKETPLACE LINKAGE ===
        // eBay listing ID for cards imported from a Seller Hub CSV. Used as the
        // upsert key on re-import so a fresh export of the same listing updates
        // the existing row instead of creating a duplicate.
        public string? EbayItemId { get; set; }
        public DateTime? ListedAt { get; set; }

        // === WHATNOT-SPECIFIC ===
        public string WhatnotCategory { get; set; } = "Sports Cards";
        public string? WhatnotSubcategory { get; set; }

        // === CHECKLIST VERIFICATION (Phase 2 — Checklist Insider) ===
        // Tier outcome the user accepted on save. Drives the badge in the editor and
        // can be re-evaluated by the missing-checklists audit when a new checklist
        // is later imported.
        public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.NotChecked;
        // Composite re-find key: "{setChecklistId}:{normalizedCardNumber}:{subsetLower}".
        // ChecklistCard rows live inside a JSON blob on SetChecklist, so a true FK isn't
        // possible without a relational migration. This string is enough to look the
        // matched row back up from the editor or re-verify pass.
        public string? MatchedChecklistKey { get; set; }

        // === SURPRISE SET ===
        public int? SurpriseSetId { get; set; }
        public int? SurpriseSetSlot { get; set; }  // 1-based position in the set's checklist
        public SurpriseSet? SurpriseSet { get; set; }

        // === STATUS / METADATA ===
        public CardStatus Status { get; set; } = CardStatus.Draft;
        public CardDataSource DataSource { get; set; } = CardDataSource.None;
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // === NAVIGATION ===
        public ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();
    }
}
