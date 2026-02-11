using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Data;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace FlipKit.Core.Services
{
    public class CardRepository : ICardRepository
    {
        private readonly FlipKitDbContext _db;

        public CardRepository(FlipKitDbContext db)
        {
            _db = db;
        }

        public async Task<int> InsertCardAsync(Card card)
        {
            card.CreatedAt = DateTime.UtcNow;
            card.UpdatedAt = DateTime.UtcNow;
            _db.Cards.Add(card);
            await _db.SaveChangesAsync();
            return card.Id;
        }

        public async Task UpdateCardAsync(Card card)
        {
            card.UpdatedAt = DateTime.UtcNow;

            // If entity is already tracked, detach it first to avoid tracking conflicts
            var existingEntry = _db.ChangeTracker.Entries<Card>()
                .FirstOrDefault(e => e.Entity.Id == card.Id);

            if (existingEntry != null)
            {
                existingEntry.State = EntityState.Detached;
            }

            _db.Cards.Update(card);
            await _db.SaveChangesAsync();
        }

        public async Task<Card?> GetCardAsync(int id)
        {
            return await _db.Cards
                .Include(c => c.PriceHistories)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<List<Card>> GetAllCardsAsync(CardStatus? status = null, Sport? sport = null)
        {
            var query = _db.Cards.AsQueryable();

            if (status.HasValue)
                query = query.Where(c => c.Status == status.Value);

            if (sport.HasValue)
                query = query.Where(c => c.Sport == sport.Value);

            return await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        }

        public async Task DeleteCardAsync(int id)
        {
            var card = await _db.Cards.FindAsync(id);
            if (card != null)
            {
                _db.Cards.Remove(card);
                await _db.SaveChangesAsync();
            }
        }

        public async Task<List<Card>> SearchCardsAsync(string query)
        {
            var lowerQuery = query.ToLower();
            return await _db.Cards
                .Where(c =>
                    c.PlayerName.ToLower().Contains(lowerQuery) ||
                    (c.Brand != null && c.Brand.ToLower().Contains(lowerQuery)) ||
                    (c.Team != null && c.Team.ToLower().Contains(lowerQuery)) ||
                    (c.Manufacturer != null && c.Manufacturer.ToLower().Contains(lowerQuery)))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Card>> GetStaleCardsAsync(int thresholdDays)
        {
            var threshold = DateTime.UtcNow.AddDays(-thresholdDays);
            return await _db.Cards
                .Where(c =>
                    c.Status != CardStatus.Sold &&
                    c.Status != CardStatus.Draft &&
                    c.PriceDate.HasValue &&
                    c.PriceDate.Value < threshold)
                .OrderBy(c => c.PriceDate)
                .ToListAsync();
        }

        public async Task AddPriceHistoryAsync(PriceHistory history)
        {
            history.RecordedAt = DateTime.UtcNow;
            _db.PriceHistories.Add(history);
            await _db.SaveChangesAsync();
        }

        public async Task<int> GetCardCountAsync()
        {
            return await _db.Cards.CountAsync();
        }
    }
}
