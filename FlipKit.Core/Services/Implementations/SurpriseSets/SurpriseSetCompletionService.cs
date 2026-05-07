using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Services.Implementations.SurpriseSets
{
    public class SurpriseSetCompletionService : ISurpriseSetCompletionService
    {
        private readonly ISurpriseSetRepository _repository;
        private readonly IRevenueAllocationService _allocator;

        public SurpriseSetCompletionService(
            ISurpriseSetRepository repository,
            IRevenueAllocationService allocator)
        {
            _repository = repository;
            _allocator = allocator;
        }

        public async Task<CompleteSetResult> CompleteAsync(int setId, CompleteSetRequest request)
        {
            var set = await _repository.GetByIdWithCardsAsync(setId);
            if (set == null)
                return Fail($"Surprise set {setId} not found.");

            if (set.State is SurpriseSetState.Completed or SurpriseSetState.Cancelled)
                return Fail($"Set is already {set.State} and cannot be completed again.");

            var cards = set.Cards.OrderBy(c => c.SurpriseSetSlot ?? int.MaxValue).ToList();

            if (request.SpotsSold < 0 || request.SpotsSold > cards.Count)
                return Fail($"SpotsSold ({request.SpotsSold}) must be between 0 and {cards.Count}.");

            if (request.GrossRevenue < 0)
                return Fail("GrossRevenue cannot be negative.");

            IList<CardAllocation> allocations;
            try
            {
                allocations = _allocator.Allocate(
                    set.AllocationMethod,
                    cards,
                    request.SpotsSold,
                    request.GrossRevenue,
                    request.TotalFees,
                    request.TotalShipping);
            }
            catch (Exception ex)
            {
                return Fail($"Allocation failed: {ex.Message}");
            }

            var completedAt = DateTime.UtcNow;
            set.State = SurpriseSetState.Completed;
            set.CompletedAt = completedAt;
            set.UpdatedAt = completedAt;
            set.SpotsSold = request.SpotsSold;
            set.GrossRevenue = request.GrossRevenue;
            set.TotalFees = request.TotalFees;
            set.TotalShipping = request.TotalShipping;

            await _repository.CompleteSetAsync(set, allocations, completedAt);

            return new CompleteSetResult
            {
                Success = true,
                Allocations = allocations,
            };
        }

        private static CompleteSetResult Fail(string message) =>
            new() { Success = false, ErrorMessage = message };
    }
}
