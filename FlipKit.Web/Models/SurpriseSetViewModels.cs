using System;
using System.Collections.Generic;
using FlipKit.Core.Models;
using FlipKit.Core.Services;

namespace FlipKit.Web.Models
{
    public class SurpriseSetIndexViewModel
    {
        public List<SurpriseSet> Sets { get; set; } = new();
        public string? StatusMessage { get; set; }
    }

    public class SurpriseSetDetailViewModel
    {
        public SurpriseSet Set { get; set; } = null!;
        public List<Card> Cards { get; set; } = new();
        public List<SurpriseSetIssue> Issues { get; set; } = new();
        public bool HasErrors { get; set; }
        public string? StatusMessage { get; set; }
    }

    public class SurpriseSetCreateViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string? ShowName { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal SpotPrice { get; set; }
        public string SharedCondition { get; set; } = "Near Mint";
        public string SharedShippingProfile { get; set; } = string.Empty;
        public string SharedWhatnotCategory { get; set; } = "Sports Trading Cards";
        public string? Notes { get; set; }
        public string? Error { get; set; }
    }

    public class SurpriseSetCompleteFormModel
    {
        public int SpotsSold { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal TotalFees { get; set; }
        public decimal TotalShipping { get; set; }
    }

    public class SurpriseSetBulkScanViewModel
    {
        public SurpriseSet Set { get; set; } = null!;
        public string? JobId { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Stored in IMemoryCache keyed by JobId during bulk scan.
    /// </summary>
    public class BulkScanJob
    {
        public int SetId { get; set; }
        public string[] ImagePaths { get; set; } = Array.Empty<string>();
        public List<BulkScanJobResult> Results { get; set; } = new();
    }

    public class BulkScanJobResult
    {
        public int Index { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? PlayerName { get; set; }
        public string? CardNumber { get; set; }
        public int? Year { get; set; }
        public string? Manufacturer { get; set; }
        public string? Brand { get; set; }
        public string? SetName { get; set; }
        public string? Team { get; set; }
        public string? Sport { get; set; }
        public string? VariationType { get; set; }
        public string? ParallelName { get; set; }
        public string? SerialNumbered { get; set; }
        public bool IsRookie { get; set; }
        public bool IsAuto { get; set; }
        public bool IsRelic { get; set; }
        public bool IsGraded { get; set; }
        public string? GradeCompany { get; set; }
        public string? GradeValue { get; set; }
        public string Condition { get; set; } = "Near Mint";
        public decimal? CostBasis { get; set; }
        public string? Notes { get; set; }
        public string? ImagePath { get; set; }
    }
}
