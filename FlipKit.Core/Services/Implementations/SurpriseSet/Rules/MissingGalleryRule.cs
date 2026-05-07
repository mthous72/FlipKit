using System.Collections.Generic;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services.Implementations.SurpriseSets.Rules
{
    internal sealed class MissingGalleryRule : ISurpriseSetRule
    {
        public IEnumerable<SurpriseSetIssue> Check(Models.SurpriseSet set, IList<Card> cards)
        {
            if (string.IsNullOrWhiteSpace(set.SharedImageUrl1))
                yield return new SurpriseSetIssue(
                    "MISSING_GALLERY",
                    "At least one gallery image (SharedImageUrl1) is required before exporting to Whatnot.",
                    IssueSeverity.Error,
                    Field: nameof(set.SharedImageUrl1));
        }
    }
}
