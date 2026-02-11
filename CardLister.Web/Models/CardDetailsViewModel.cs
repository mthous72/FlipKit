using System.ComponentModel.DataAnnotations;
using FlipKit.Core.Models.Enums;
using CostSource = FlipKit.Core.Models.Enums.CostSource;

namespace FlipKit.Web.Models
{
    /// <summary>
    /// View model for viewing and editing card details.
    /// </summary>
    public class CardDetailsViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Player Name")]
        public string? PlayerName { get; set; }

        [Display(Name = "Sport")]
        public Sport? Sport { get; set; }

        [Display(Name = "Brand")]
        public string? Brand { get; set; }

        [Display(Name = "Manufacturer")]
        public string? Manufacturer { get; set; }

        [Display(Name = "Year")]
        public int? Year { get; set; }

        [Display(Name = "Card Number")]
        public string? CardNumber { get; set; }

        [Display(Name = "Team")]
        public string? Team { get; set; }

        [Display(Name = "Set Name")]
        public string? SetName { get; set; }

        [Display(Name = "Variation Type")]
        public string? VariationType { get; set; }

        [Display(Name = "Parallel Name")]
        public string? ParallelName { get; set; }

        [Display(Name = "Serial Number")]
        public string? SerialNumbered { get; set; }

        [Display(Name = "Short Print")]
        public bool IsShortPrint { get; set; }

        [Display(Name = "Super Short Print")]
        public bool IsSSP { get; set; }

        [Display(Name = "Rookie Card")]
        public bool IsRookie { get; set; }

        [Display(Name = "Autograph")]
        public bool IsAuto { get; set; }

        [Display(Name = "Relic/Memorabilia")]
        public bool IsRelic { get; set; }

        [Display(Name = "Condition")]
        public string? Condition { get; set; }

        [Display(Name = "Graded")]
        public bool IsGraded { get; set; }

        [Display(Name = "Grading Company")]
        public string? GradeCompany { get; set; }

        [Display(Name = "Grade")]
        public string? GradeValue { get; set; }

        [Display(Name = "Cert Number")]
        public string? CertNumber { get; set; }

        [Display(Name = "Auto Grade")]
        public string? AutoGrade { get; set; }

        [Display(Name = "Cost Basis")]
        [DataType(DataType.Currency)]
        public decimal? CostBasis { get; set; }

        [Display(Name = "Cost Source")]
        public CostSource? CostSource { get; set; }

        [Display(Name = "Cost Date")]
        [DataType(DataType.Date)]
        public DateTime? CostDate { get; set; }

        [Display(Name = "Cost Notes")]
        public string? CostNotes { get; set; }

        [Display(Name = "Quantity")]
        public int Quantity { get; set; } = 1;

        [Display(Name = "Estimated Value")]
        [DataType(DataType.Currency)]
        public decimal? EstimatedValue { get; set; }

        [Display(Name = "Listing Price")]
        [DataType(DataType.Currency)]
        public decimal? ListingPrice { get; set; }

        [Display(Name = "Listing Type")]
        public string? ListingType { get; set; }

        [Display(Name = "Accept Offers")]
        public bool Offerable { get; set; }

        [Display(Name = "Shipping Profile")]
        public string? ShippingProfile { get; set; }

        [Display(Name = "Whatnot Category")]
        public string? WhatnotCategory { get; set; }

        [Display(Name = "Whatnot Subcategory")]
        public string? WhatnotSubcategory { get; set; }

        [Display(Name = "Notes")]
        [DataType(DataType.MultilineText)]
        public string? Notes { get; set; }

        public CardStatus Status { get; set; }

        [Display(Name = "Front Image")]
        public string? ImagePathFront { get; set; }

        [Display(Name = "Back Image")]
        public string? ImagePathBack { get; set; }

        [Display(Name = "Image URL 1")]
        public string? ImageUrl1 { get; set; }

        [Display(Name = "Image URL 2")]
        public string? ImageUrl2 { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
