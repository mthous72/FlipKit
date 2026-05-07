using System.Collections.Generic;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services.Implementations.SurpriseSets.Rules
{
    internal sealed class MaxCardsRule : ISurpriseSetRule
    {
        private const int WhatnotMaxSpots = 500;

        public IEnumerable<SurpriseSetIssue> Check(Models.SurpriseSet set, IList<Card> cards)
        {
            if (cards.Count > WhatnotMaxSpots)
                yield return new SurpriseSetIssue(
                    "MAX_CARDS",
                    $"Whatnot Surprise Sets are limited to {WhatnotMaxSpots} spots. This set has {cards.Count}.",
                    IssueSeverity.Error);
        }
    }
}
