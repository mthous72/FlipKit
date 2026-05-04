using System.Threading.Tasks;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Runs a freshly-scanned <see cref="Card"/> against the locked or auto-resolved
    /// <see cref="SetChecklist"/> and produces a tier outcome the editor can render.
    /// Tier 1 (Verified) = exact match, all confidences high. Tier 2 (BestGuess) =
    /// card # + player matched but at least one field is uncertain. Tier 3 (NoMatch)
    /// = no candidate; user picks from a fuzzy candidate list.
    /// </summary>
    public interface IChecklistVerificationMatcher
    {
        Task<ChecklistMatchResult> MatchAsync(Card card);
    }
}
