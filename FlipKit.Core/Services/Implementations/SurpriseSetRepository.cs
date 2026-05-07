using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Data;
using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace FlipKit.Core.Services.Implementations
{
    public class SurpriseSetRepository : ISurpriseSetRepository
    {
        private readonly FlipKitDbContext _db;

        public SurpriseSetRepository(FlipKitDbContext db) => _db = db;

        public async Task<SurpriseSet?> GetByIdAsync(int id) =>
            await _db.SurpriseSets.FirstOrDefaultAsync(s => s.Id == id);

        public async Task<SurpriseSet?> GetByIdWithCardsAsync(int id) =>
            await _db.SurpriseSets
                .Include(s => s.Cards)
                .FirstOrDefaultAsync(s => s.Id == id);

        public async Task<List<SurpriseSet>> GetAllAsync() =>
            await _db.SurpriseSets
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

        public async Task<List<SurpriseSet>> GetDraftSetsAsync() =>
            await _db.SurpriseSets
                .Where(s => s.State == SurpriseSetState.Draft)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

        public async Task<int> InsertAsync(SurpriseSet set)
        {
            set.CreatedAt = DateTime.UtcNow;
            set.UpdatedAt = DateTime.UtcNow;
            _db.SurpriseSets.Add(set);
            await _db.SaveChangesAsync();
            return set.Id;
        }

        public async Task UpdateAsync(SurpriseSet set)
        {
            set.UpdatedAt = DateTime.UtcNow;

            var existing = _db.ChangeTracker.Entries<SurpriseSet>()
                .FirstOrDefault(e => e.Entity.Id == set.Id);
            if (existing != null)
                existing.State = EntityState.Detached;

            _db.SurpriseSets.Update(set);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // Delete cards before the set (FK Restrict requires this order).
                await _db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM cards WHERE SurpriseSetId = {0}", id);
                await _db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM surprise_sets WHERE Id = {0}", id);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task AddCardAsync(int setId, Card card)
        {
            if (await IsLockedAsync(setId))
                throw new InvalidOperationException(
                    $"Surprise set {setId} is locked (State >= Exported) and cannot accept new cards.");

            var set = await GetByIdWithCardsAsync(setId)
                ?? throw new InvalidOperationException($"Surprise set {setId} not found.");

            int nextSlot = set.Cards.Any() ? set.Cards.Max(c => c.SurpriseSetSlot ?? 0) + 1 : 1;
            card.SurpriseSetId = setId;
            card.SurpriseSetSlot = nextSlot;
            card.Status = CardStatus.ReservedForSet;

            var existing = _db.ChangeTracker.Entries<Card>()
                .FirstOrDefault(e => e.Entity.Id == card.Id);
            if (existing != null)
                existing.State = EntityState.Detached;

            _db.Cards.Update(card);
            await _db.SaveChangesAsync();

            if (set.LotCostBasis.HasValue)
                await RebalanceLotCostAsync(setId, set.LotCostBasis.Value);
        }

        public async Task RemoveCardAsync(int setId, int cardId)
        {
            if (await IsLockedAsync(setId))
                throw new InvalidOperationException(
                    $"Surprise set {setId} is locked and cannot have cards removed.");

            var card = await _db.Cards
                .FirstOrDefaultAsync(c => c.Id == cardId && c.SurpriseSetId == setId)
                ?? throw new InvalidOperationException(
                    $"Card {cardId} was not found in surprise set {setId}.");

            card.SurpriseSetId = null;
            card.SurpriseSetSlot = null;
            // Re-evaluate status now that the card is no longer in a set.
            card.Status = CardStatusEvaluator.Evaluate(card);
            _db.Cards.Update(card);
            await _db.SaveChangesAsync();

            // Renumber remaining cards 1..N in their current slot order.
            var remaining = await _db.Cards
                .Where(c => c.SurpriseSetId == setId)
                .OrderBy(c => c.SurpriseSetSlot)
                .ToListAsync();

            for (int i = 0; i < remaining.Count; i++)
                remaining[i].SurpriseSetSlot = i + 1;

            if (remaining.Count > 0)
                await _db.SaveChangesAsync();

            var set = await GetByIdAsync(setId);
            if (set?.LotCostBasis.HasValue == true)
                await RebalanceLotCostAsync(setId, set.LotCostBasis.Value);
        }

        public async Task<bool> IsLockedAsync(int id)
        {
            var state = await _db.SurpriseSets
                .Where(s => s.Id == id)
                .Select(s => (SurpriseSetState?)s.State)
                .FirstOrDefaultAsync();

            return state is SurpriseSetState.Exported
                or SurpriseSetState.Live
                or SurpriseSetState.Completed
                or SurpriseSetState.Cancelled;
        }

        public async Task CompleteSetAsync(
            SurpriseSet set,
            IList<CardAllocation> allocations,
            DateTime completedAt)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var existing = _db.ChangeTracker.Entries<SurpriseSet>()
                    .FirstOrDefault(e => e.Entity.Id == set.Id);
                if (existing != null) existing.State = EntityState.Detached;
                _db.SurpriseSets.Update(set);

                foreach (var alloc in allocations)
                {
                    var card = await _db.Cards.FindAsync(alloc.CardId);
                    if (card == null) continue;

                    if (alloc.IsSold)
                    {
                        card.SalePrice = alloc.AllocatedRevenue;
                        card.SaleDate = completedAt;
                        card.SalePlatform = "Whatnot";
                        card.Status = CardStatus.SoldInSet;
                    }
                    else
                    {
                        card.SurpriseSetId = null;
                        card.SurpriseSetSlot = null;
                        card.Status = CardStatusEvaluator.Evaluate(card);
                    }
                    card.UpdatedAt = completedAt;
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Rebalances per-card cost for all LotSplit cards in the set.
        // N = total cards in set (including manually-costed ones) so the split
        // reflects the actual lot composition, not just the auto-split subset.
        private async Task RebalanceLotCostAsync(int setId, decimal lotCostBasis)
        {
            int totalCards = await _db.Cards.CountAsync(c => c.SurpriseSetId == setId);
            if (totalCards == 0) return;

            decimal perCard = lotCostBasis / totalCards;

            var lotSplitCards = await _db.Cards
                .Where(c => c.SurpriseSetId == setId && c.CostSource == CostSource.LotSplit)
                .ToListAsync();

            foreach (var c in lotSplitCards)
                c.CostBasis = perCard;

            if (lotSplitCards.Count > 0)
                await _db.SaveChangesAsync();
        }
    }
}
