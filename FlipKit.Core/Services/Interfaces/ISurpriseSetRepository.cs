using System.Collections.Generic;
using System.Threading.Tasks;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    public interface ISurpriseSetRepository
    {
        Task<SurpriseSet?> GetByIdAsync(int id);
        Task<SurpriseSet?> GetByIdWithCardsAsync(int id);
        Task<List<SurpriseSet>> GetAllAsync();
        Task<List<SurpriseSet>> GetDraftSetsAsync();
        Task<int> InsertAsync(SurpriseSet set);
        Task UpdateAsync(SurpriseSet set);

        /// <summary>
        /// Hard-deletes the set and ALL of its cards. Only valid on Draft sets.
        /// The caller is responsible for confirming with the user before calling.
        /// </summary>
        Task DeleteAsync(int id);

        /// <summary>
        /// Assigns the card to the set (next slot), stamps Status = ReservedForSet,
        /// and rebalances lot cost if LotCostBasis is configured.
        /// Throws if the set is locked (State >= Exported).
        /// </summary>
        Task AddCardAsync(int setId, Card card);

        /// <summary>
        /// Removes the card from the set, re-evaluates its status via CardStatusEvaluator,
        /// and renumbers the remaining slots. Rebalances lot cost if applicable.
        /// Throws if the set is locked.
        /// </summary>
        Task RemoveCardAsync(int setId, int cardId);

        /// <summary>
        /// Returns true when the set is in a state that prevents card add/remove
        /// (Exported, Live, Completed, or Cancelled).
        /// </summary>
        Task<bool> IsLockedAsync(int id);
    }
}
