using System;
using System.Collections.Generic;
using System.Linq;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Services.Implementations.SurpriseSets
{
    public class RevenueAllocationService : IRevenueAllocationService
    {
        public IList<CardAllocation> Allocate(
            RevenueAllocationMethod method,
            IList<Card> cards,
            int spotsSold,
            decimal grossRevenue,
            decimal totalFees,
            decimal totalShipping)
        {
            if (spotsSold < 0 || spotsSold > cards.Count)
                throw new ArgumentOutOfRangeException(nameof(spotsSold),
                    $"spotsSold ({spotsSold}) must be between 0 and cards.Count ({cards.Count}).");

            var ordered = cards.OrderBy(c => c.SurpriseSetSlot ?? int.MaxValue).ToList();
            var soldCards = ordered.Take(spotsSold).ToList();
            var unsoldCards = ordered.Skip(spotsSold).ToList();

            var result = new List<CardAllocation>();

            switch (method)
            {
                case RevenueAllocationMethod.EqualSplit:
                    result.AddRange(AllocateEqual(soldCards, grossRevenue, totalFees, totalShipping));
                    break;

                case RevenueAllocationMethod.CostWeighted:
                    result.AddRange(AllocateCostWeighted(soldCards, grossRevenue, totalFees, totalShipping));
                    break;

                case RevenueAllocationMethod.Manual:
                    result.AddRange(AllocateManual(soldCards));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(method), method, null);
            }

            foreach (var c in unsoldCards)
                result.Add(new CardAllocation(c.Id, IsSold: false, AllocatedRevenue: null));

            return result;
        }

        private static IEnumerable<CardAllocation> AllocateEqual(
            List<Card> soldCards,
            decimal grossRevenue,
            decimal totalFees,
            decimal totalShipping)
        {
            if (soldCards.Count == 0) yield break;

            decimal net = grossRevenue - totalFees - totalShipping;
            decimal perCard = Math.Round(net / soldCards.Count, 2);
            decimal distributed = perCard * (soldCards.Count - 1);

            for (int i = 0; i < soldCards.Count; i++)
            {
                // Last card absorbs the rounding remainder.
                decimal alloc = i == soldCards.Count - 1
                    ? net - distributed
                    : perCard;
                yield return new CardAllocation(soldCards[i].Id, IsSold: true, AllocatedRevenue: alloc);
            }
        }

        private static IEnumerable<CardAllocation> AllocateCostWeighted(
            List<Card> soldCards,
            decimal grossRevenue,
            decimal totalFees,
            decimal totalShipping)
        {
            if (soldCards.Count == 0) yield break;

            // Fall back to equal split if any sold card is missing a cost basis.
            bool anyCostMissing = soldCards.Any(c => c.CostBasis is null or 0);
            if (anyCostMissing)
            {
                foreach (var a in AllocateEqual(soldCards, grossRevenue, totalFees, totalShipping))
                    yield return a;
                yield break;
            }

            decimal net = grossRevenue - totalFees - totalShipping;
            decimal totalCost = soldCards.Sum(c => c.CostBasis!.Value);

            decimal distributed = 0m;
            for (int i = 0; i < soldCards.Count; i++)
            {
                if (i == soldCards.Count - 1)
                {
                    yield return new CardAllocation(soldCards[i].Id, IsSold: true,
                        AllocatedRevenue: net - distributed);
                }
                else
                {
                    decimal alloc = Math.Round(net * (soldCards[i].CostBasis!.Value / totalCost), 2);
                    distributed += alloc;
                    yield return new CardAllocation(soldCards[i].Id, IsSold: true,
                        AllocatedRevenue: alloc);
                }
            }
        }

        private static IEnumerable<CardAllocation> AllocateManual(List<Card> soldCards)
        {
            foreach (var c in soldCards)
                yield return new CardAllocation(c.Id, IsSold: true,
                    AllocatedRevenue: c.SalePrice);
        }
    }
}
