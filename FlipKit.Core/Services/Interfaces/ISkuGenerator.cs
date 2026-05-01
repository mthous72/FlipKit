using System.Threading.Tasks;

namespace FlipKit.Core.Services
{
    public interface ISkuGenerator
    {
        /// <summary>
        /// Generates the next available auto-incremented SKU using the configured prefix and pad width.
        /// Uses MAX(numeric_suffix) + 1 across existing SKUs, so deleted SKUs are never reused.
        /// </summary>
        Task<string> GenerateNextSkuAsync();

        /// <summary>
        /// Returns true if the given SKU is not already used by another card.
        /// Pass excludeCardId to ignore a specific card's existing SKU (used when a user is editing one).
        /// </summary>
        Task<bool> IsSkuAvailableAsync(string sku, int? excludeCardId = null);
    }
}
