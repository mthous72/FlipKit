using System.Collections.Generic;
using System.Text.RegularExpressions;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services.Implementations.SurpriseSets.Rules
{
    internal sealed class ProhibitedPrizeLanguageRule : ISurpriseSetRule
    {
        // Matches prize/hype language prohibited by Whatnot's Surprise Set policy.
        private static readonly Regex Pattern = new(
            @"\b(guaranteed\s+hit|big\s+hit|chase\s+card|chase|holy\s+grail|grail\s+card|whale\s+hit|prize\s+card)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public IEnumerable<SurpriseSetIssue> Check(Models.SurpriseSet set, IList<Card> cards)
        {
            var text = $"{set.Title} {set.Notes}";
            var match = Pattern.Match(text);
            if (match.Success)
                yield return new SurpriseSetIssue(
                    "PROHIBITED_PRIZE_LANG",
                    $"Title or notes contain prohibited prize language ('{match.Value}'). Whatnot forbids guaranteed-hit, chase, and grail language in Surprise Set listings.",
                    IssueSeverity.Error,
                    Field: "Title/Notes");
        }
    }
}
