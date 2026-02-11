using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// API-based implementation of ICardRepository for remote access via Tailscale.
    /// </summary>
    public class ApiCardRepository : ICardRepository
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly ILogger<ApiCardRepository>? _logger;

        public ApiCardRepository(HttpClient httpClient, string baseUrl, ILogger<ApiCardRepository>? logger = null)
        {
            _httpClient = httpClient;
            _baseUrl = baseUrl.TrimEnd('/');
            _logger = logger;
        }

        public async Task<int> InsertCardAsync(Card card)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/cards", card);
                response.EnsureSuccessStatusCode();

                var createdCard = await response.Content.ReadFromJsonAsync<Card>();
                return createdCard?.Id ?? 0;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to insert card via API");
                throw;
            }
        }

        public async Task UpdateCardAsync(Card card)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{_baseUrl}/api/cards/{card.Id}", card);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to update card {CardId} via API", card.Id);
                throw;
            }
        }

        public async Task<Card?> GetCardAsync(int id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<Card>($"{_baseUrl}/api/cards/{id}");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get card {CardId} via API", id);
                throw;
            }
        }

        public async Task<List<Card>> GetAllCardsAsync(CardStatus? status = null, Sport? sport = null)
        {
            try
            {
                var queryParams = new List<string>();
                if (status.HasValue)
                    queryParams.Add($"status={status.Value}");
                if (sport.HasValue)
                    queryParams.Add($"sport={sport.Value}");

                var query = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
                var url = $"{_baseUrl}/api/cards{query}";

                var cards = await _httpClient.GetFromJsonAsync<List<Card>>(url);
                return cards ?? new List<Card>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get all cards via API");
                throw;
            }
        }

        public async Task DeleteCardAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{_baseUrl}/api/cards/{id}");
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to delete card {CardId} via API", id);
                throw;
            }
        }

        public async Task<List<Card>> GetUnpricedCardsAsync()
        {
            try
            {
                var cards = await _httpClient.GetFromJsonAsync<List<Card>>($"{_baseUrl}/api/cards/unpriced");
                return cards ?? new List<Card>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get unpriced cards via API");
                throw;
            }
        }

        public async Task<List<Card>> GetStaleCardsAsync(int thresholdDays)
        {
            try
            {
                var cards = await _httpClient.GetFromJsonAsync<List<Card>>($"{_baseUrl}/api/cards/stale");
                return cards ?? new List<Card>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get stale cards via API");
                throw;
            }
        }

        public async Task<List<Card>> GetSoldCardsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var queryParams = new List<string>();
                if (startDate.HasValue)
                    queryParams.Add($"startDate={startDate.Value:O}");
                if (endDate.HasValue)
                    queryParams.Add($"endDate={endDate.Value:O}");

                var query = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
                var url = $"{_baseUrl}/api/reports/sold{query}";

                var response = await _httpClient.GetFromJsonAsync<SoldCardsResponse>(url);
                return response?.Cards ?? new List<Card>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get sold cards via API");
                throw;
            }
        }

        public async Task<List<Card>> SearchCardsAsync(string query)
        {
            try
            {
                var cards = await _httpClient.GetFromJsonAsync<List<Card>>($"{_baseUrl}/api/cards?search={Uri.EscapeDataString(query)}");
                return cards ?? new List<Card>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to search cards via API");
                throw;
            }
        }

        public async Task AddPriceHistoryAsync(PriceHistory history)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/cards/{history.CardId}/price-history", history);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to add price history via API");
                throw;
            }
        }

        public async Task<int> GetCardCountAsync()
        {
            try
            {
                var stats = await _httpClient.GetFromJsonAsync<CardStatsResponse>($"{_baseUrl}/api/cards/stats");
                return stats?.TotalCards ?? 0;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get card count via API");
                throw;
            }
        }

        private class SoldCardsResponse
        {
            public List<Card> Cards { get; set; } = new();
            public object? Summary { get; set; }
        }

        private class CardStatsResponse
        {
            public int TotalCards { get; set; }
        }
    }
}
