using System.Collections.Generic;
using System.Linq;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Services.Implementations.SurpriseSets.Rules
{
    internal sealed class ManualAllocationReconciliationRule : ISurpriseSetRule
    {
        private const decimal Tolerance = 0.01m;

        public IEnumerable<SurpriseSetIssue> Check(Models.SurpriseSet set, IList<Card> cards)
        {
            if (set.AllocationMethod != RevenueAllocationMethod.Manual)
                yield break;

            if (!set.GrossRevenue.HasValue)
                yield break;

            // Only validate once some per-card amounts have been entered.
            var entered = cards.Where(c => c.SalePrice.HasValue).ToList();
            if (entered.Count == 0)
                yield break;

            decimal netGross = set.GrossRevenue.Value
                - (set.TotalFees ?? 0m)
                - (set.TotalShipping ?? 0m);

            decimal allocated = entered.Sum(c => c.SalePrice!.Value);
            decimal diff = System.Math.Abs(allocated - netGross);

            if (diff > Tolerance)
                yield return new SurpriseSetIssue(
                    "MANUAL_ALLOC_MISMATCH",
                    $"Manual allocation total ({allocated:C}) does not match net gross ({netGross:C}). Difference: {diff:C}.",
                    IssueSeverity.Error);
        }
    }
}
