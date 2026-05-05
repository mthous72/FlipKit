using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Second pass of the eBay listings title-parse pipeline. The deterministic
    /// rule pass (<see cref="FlipKit.Core.Helpers.EbayTitleParser"/>) extracts
    /// what regex can confidently identify; this interface fills the soft
    /// fields — PlayerName, Brand, SetName, ParallelName, Team — using an LLM.
    /// Implementations are free to batch internally (single big prompt vs. one
    /// call per title) so callers shouldn't assume any latency profile.
    /// </summary>
    public interface IEbayTitleEnricher
    {
        Task<IReadOnlyList<EbayTitleEnrichment>> EnrichAsync(
            IReadOnlyList<string> titles,
            CancellationToken ct = default);
    }

    /// <summary>
    /// LLM-derived soft fields for one listing title. Any field may be null if
    /// the model couldn't extract it. Original index in the request batch is
    /// preserved by position in the result list (1:1 with the input list).
    /// </summary>
    public sealed record EbayTitleEnrichment(
        string? PlayerName,
        string? Brand,
        string? SetName,
        string? ParallelName,
        string? Team);
}
