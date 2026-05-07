using System.Collections.Generic;
using System.Linq;
using FlipKit.Core.Models;
using FlipKit.Core.Services.Implementations.SurpriseSets.Rules;

namespace FlipKit.Core.Services.Implementations.SurpriseSets
{
    public sealed class SurpriseSetValidator : ISurpriseSetValidator
    {
        private readonly IReadOnlyList<ISurpriseSetRule> _rules =
        [
            new MinCardsRule(),
            new MaxCardsRule(),
            new MixedSportRule(),
            new MixedProductTypeRule(),
            new InconsistentConditionRule(),
            new MissingGalleryRule(),
            new ProhibitedValueLanguageRule(),
            new ProhibitedPrizeLanguageRule(),
            new CompletionDataRule(),
            new ManualAllocationReconciliationRule(),
        ];

        public IList<SurpriseSetIssue> Validate(Models.SurpriseSet set, IList<Card> cards)
            => _rules.SelectMany(r => r.Check(set, cards)).ToList();
    }
}
