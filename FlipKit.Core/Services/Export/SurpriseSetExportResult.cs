using System.Collections.Generic;
using FlipKit.Core.Services;

namespace FlipKit.Core.Services.Export
{
    public sealed class SurpriseSetExportResult
    {
        public bool Success { get; init; }
        public int RowsWritten { get; init; }
        public IList<SurpriseSetIssue> BlockingIssues { get; init; } = System.Array.Empty<SurpriseSetIssue>();
    }
}
