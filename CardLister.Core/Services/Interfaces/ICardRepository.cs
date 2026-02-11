using System.Collections.Generic;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Services
{
    public interface ICardRepository
    {
        Task<int> InsertCardAsync(Card card);
        Task UpdateCardAsync(Card card);
        Task<Card?> GetCardAsync(int id);
        Task<List<Card>> GetAllCardsAsync(CardStatus? status = null, Sport? sport = null);
        Task DeleteCardAsync(int id);
        Task<List<Card>> SearchCardsAsync(string query);
        Task<List<Card>> GetStaleCardsAsync(int thresholdDays);
        Task AddPriceHistoryAsync(PriceHistory history);
        Task<int> GetCardCountAsync();
    }
}
