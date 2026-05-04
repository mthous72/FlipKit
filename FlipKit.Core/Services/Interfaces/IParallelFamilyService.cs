using System.Collections.Generic;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Looks up parallel options for a given (Year, Brand, Sport) tuple from the
    /// bundled <c>ParallelFamilyCatalog.json</c>. Returns an empty list when the
    /// set isn't catalogued — UI falls back to free-text Parallel in that case.
    /// </summary>
    public interface IParallelFamilyService
    {
        IReadOnlyList<ParallelOption> GetParallels(int? year, string? brand, string? sport);
    }
}
