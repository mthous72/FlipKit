using System.Collections.Generic;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services.Implementations.SurpriseSets
{
    internal interface ISurpriseSetRule
    {
        IEnumerable<SurpriseSetIssue> Check(Models.SurpriseSet set, IList<Card> cards);
    }
}
