using System.Collections.Generic;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Allocates net revenue across the sold cards in a Surprise Set.
    /// Pure math — no database access, fully deterministic.
    /// </summary>
    public interface IRevenueAllocationService
    {
        /// <param name="method">The allocation strategy chosen for the set.</param>
        /// <param name="cards">All cards in the set, ordered by SurpriseSetSlot ascending.</param>
        /// <param name="spotsSold">Number of spots that sold. First N cards (by slot) are sold.</param>
        /// <param name="grossRevenue">Total buyer payments received before deductions.</param>
        /// <param name="totalFees">Platform and payment fees.</param>
        /// <param name="totalShipping">Shipping costs paid by the seller.</param>
        IList<CardAllocation> Allocate(
            RevenueAllocationMethod method,
            IList<Card> cards,
            int spotsSold,
            decimal grossRevenue,
            decimal totalFees,
            decimal totalShipping);
    }

    /// <summary>
    /// Per-card outcome from the allocation calculation.
    /// </summary>
    public sealed record CardAllocation(
        int CardId,
        bool IsSold,
        decimal? AllocatedRevenue);
}
