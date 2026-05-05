using System;

namespace FlipKit.Core.Models;

/// <summary>
/// Represents a card listing fetched from an external marketplace (eBay Browse API
/// or future providers). Used for competitive pricing research — these are active
/// asking prices, not confirmed sold prices.
/// </summary>
public class ListingRecord
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

    // === LISTING DETAILS ===
    public decimal SoldPrice { get; set; }     // asking price (not confirmed sold)
    public DateTime SoldDate { get; set; }     // listing date / last-seen date
    public string Platform { get; set; } = "eBay";
    public string? SaleType { get; set; }      // "Auction", "Buy It Now", "Best Offer"

    // === LISTING CONTEXT ===
    public decimal? ShippingCost { get; set; }
    public int? BidCount { get; set; }
    public string? ListingTitle { get; set; }
    public string? SourceUrl { get; set; }

    // === METADATA ===
    public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;
    public string? Sport { get; set; }  // Football, Baseball, Basketball
}
