using System;

namespace FlipKit.Core.Models;

/// <summary>
/// Represents a sold card transaction scraped from 130point.com or other sources.
/// Used for automated price lookups and market analysis.
/// </summary>
public class SoldPriceRecord
{
    public int Id { get; set; }

    // === CARD IDENTIFICATION ===
    public string PlayerName { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string? Manufacturer { get; set; }
    public string? Brand { get; set; }
    public string? CardNumber { get; set; }
    public string? ParallelName { get; set; }

    // === CONDITION/GRADING ===
    public string? Condition { get; set; }
    public bool IsGraded { get; set; }
    public string? GradeCompany { get; set; }  // PSA, BGS, CGC, etc.
    public string? GradeValue { get; set; }    // "10", "9.5", etc.

    // === SALE DETAILS ===
    public decimal SoldPrice { get; set; }
    public DateTime SoldDate { get; set; }
    public string Platform { get; set; } = "eBay";  // eBay, Whatnot, COMC
    public string? SaleType { get; set; }           // "Auction", "Buy It Now", "Best Offer"

    // === SALE CONTEXT ===
    public decimal? ShippingCost { get; set; }
    public int? BidCount { get; set; }
    public string? ListingTitle { get; set; }
    public string? SourceUrl { get; set; }  // Link back to 130point listing

    // === METADATA ===
    public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;
    public string? Sport { get; set; }  // Football, Baseball, Basketball
}
