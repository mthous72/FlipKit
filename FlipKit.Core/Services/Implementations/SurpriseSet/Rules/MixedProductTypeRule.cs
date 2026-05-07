using System.Collections.Generic;
using System.Linq;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services.Implementations.SurpriseSets.Rules
{
    internal sealed class MixedProductTypeRule : ISurpriseSetRule
    {
        public IEnumerable<SurpriseSetIssue> Check(Models.SurpriseSet set, IList<Card> cards)
        {
            bool hasGraded = cards.Any(c => c.IsGraded);
            bool hasRaw    = cards.Any(c => !c.IsGraded);

            if (hasGraded && hasRaw)
                yield return new SurpriseSetIssue(
                    "MIXED_PRODUCT",
                    "Set mixes graded and raw (ungraded) cards. Whatnot requires a consistent product type within a Surprise Set.",
                    IssueSeverity.Error);
        }
    }
}
