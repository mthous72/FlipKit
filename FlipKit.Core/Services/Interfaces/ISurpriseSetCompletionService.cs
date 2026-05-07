using System.Threading.Tasks;

namespace FlipKit.Core.Services
{
    public sealed class CompleteSetRequest
    {
        public int SpotsSold { get; init; }
        public decimal GrossRevenue { get; init; }
        public decimal TotalFees { get; init; }
        public decimal TotalShipping { get; init; }
    }

    public sealed class CompleteSetResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public IList<CardAllocation> Allocations { get; init; } = Array.Empty<CardAllocation>();
    }

    public interface ISurpriseSetCompletionService
    {
        Task<CompleteSetResult> CompleteAsync(int setId, CompleteSetRequest request);
    }
}
