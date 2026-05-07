using System.Collections.Generic;
using System.Linq;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services.Implementations.SurpriseSets.Rules
{
    internal sealed class MixedSportRule : ISurpriseSetRule
    {
        public IEnumerable<SurpriseSetIssue> Check(Models.SurpriseSet set, IList<Card> cards)
        {
            var sports = cards
                .Where(c => c.Sport != null)
                .Select(c => c.Sport)
                .Distinct()
                .ToList();

            if (sports.Count > 1)
                yield return new SurpriseSetIssue(
                    "MIXED_SPORT",
                    $"Set contains cards from multiple sports ({string.Join(", ", sports)}). Consider single-sport sets for better buyer clarity.",
                    IssueSeverity.Warning);
        }
    }
}
