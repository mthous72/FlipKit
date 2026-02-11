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
        public string? ImagePathFront { get; set; }
        public string? ImagePathBack { get; set; }
        public string? ImageUrl1 { get; set; }
        public string? ImageUrl2 { get; set; }

        // === WHATNOT-SPECIFIC ===
        public string WhatnotCategory { get; set; } = "Sports Cards";
        public string? WhatnotSubcategory { get; set; }

        // === STATUS / METADATA ===
        public CardStatus Status { get; set; } = CardStatus.Draft;
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // === NAVIGATION ===
        public ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();
    }
}
