using System.Collections.Generic;
using System.Linq;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services.Implementations.SurpriseSets.Rules
{
    internal sealed class InconsistentConditionRule : ISurpriseSetRule
    {
        public IEnumerable<SurpriseSetIssue> Check(Models.SurpriseSet set, IList<Card> cards)
        {
            // Defense-in-depth: stamping at save time should prevent this,
            // but guard against cards added directly via AddCardAsync without stamping.
            var mismatched = cards
                .Where(c => !string.IsNullOrEmpty(c.Condition)
                         && c.Condition != set.SharedCondition)
                .ToList();

            foreach (var card in mismatched)
                yield return new SurpriseSetIssue(
                    "INCONSISTENT_CONDITION",
                    $"Card '{card.PlayerName}' has condition '{card.Condition}' but set requires '{set.SharedCondition}'.",
                    IssueSeverity.Error,
                    CardId: card.Id,
                    Field: nameof(card.Condition));
        }
    }
}
