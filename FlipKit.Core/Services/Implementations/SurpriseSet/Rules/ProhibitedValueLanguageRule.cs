using System.Collections.Generic;
using System.Text.RegularExpressions;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services.Implementations.SurpriseSets.Rules
{
    internal sealed class ProhibitedValueLanguageRule : ISurpriseSetRule
    {
        // Matches language that implies a minimum, floor, or guaranteed value —
        // all prohibited by Whatnot's Surprise Set policy.
        private static readonly Regex Pattern = new(
            @"\b(floor|ceiling|average\s+value|book\s+value|estimated\s+value|worth\s+at\s+least|valued\s+at|guaranteed\s+value|guaranteed\s+minimum|min\s+value)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public IEnumerable<SurpriseSetIssue> Check(Models.SurpriseSet set, IList<Card> cards)
        {
            var text = $"{set.Title} {set.Notes}";
            var match = Pattern.Match(text);
            if (match.Success)
                yield return new SurpriseSetIssue(
                    "PROHIBITED_VALUE_LANG",
                    $"Title or notes contain prohibited value language ('{match.Value}'). Whatnot forbids floor/ceiling/average-value language in Surprise Set listings.",
                    IssueSeverity.Error,
                    Field: "Title/Notes");
        }
    }
}
