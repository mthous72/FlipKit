using System.Collections.Generic;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services.Implementations.SurpriseSets.Rules
{
    internal sealed class MinCardsRule : ISurpriseSetRule
    {
        public IEnumerable<SurpriseSetIssue> Check(Models.SurpriseSet set, IList<Card> cards)
        {
            if (cards.Count < 1)
                yield return new SurpriseSetIssue(
                    "MIN_CARDS",
                    "A Surprise Set must contain at least 1 card.",
                    IssueSeverity.Error);
        }
    }
}
