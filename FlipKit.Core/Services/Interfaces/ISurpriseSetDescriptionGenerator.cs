using System.Collections.Generic;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    public interface ISurpriseSetDescriptionGenerator
    {
        /// <summary>
        /// Generates a Whatnot listing description for a Surprise Set.
        /// Output is always template-driven — no AI or LLM is involved.
        /// </summary>
        string Generate(SurpriseSet set, IList<Card> cards);
    }
}
