using System.Collections.Generic;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Services.Implementations.SurpriseSets.Rules
{
    internal sealed class CompletionDataRule : ISurpriseSetRule
    {
        public IEnumerable<SurpriseSetIssue> Check(Models.SurpriseSet set, IList<Card> cards)
        {
            // Only meaningful once the set is being completed.
            if (set.State != SurpriseSetState.Completed &&
                set.State != SurpriseSetState.Exported &&
                set.State != SurpriseSetState.Live)
                yield break;

            if (!set.GrossRevenue.HasValue)
                yield return new SurpriseSetIssue(
                    "COMPLETION_DATA",
                    "Gross revenue is required to complete the set.",
                    IssueSeverity.Error,
                    Field: nameof(set.GrossRevenue));

            if (!set.SpotsSold.HasValue)
                yield return new SurpriseSetIssue(
                    "COMPLETION_DATA",
                    "Spots sold count is required to complete the set.",
                    IssueSeverity.Error,
                    Field: nameof(set.SpotsSold));
        }
    }
}
