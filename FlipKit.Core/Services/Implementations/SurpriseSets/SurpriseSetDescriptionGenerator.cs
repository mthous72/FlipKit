using System.Collections.Generic;
using System.Linq;
using System.Text;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services.Implementations.SurpriseSets
{
    // IMPORTANT: This class MUST NOT call any LLM or AI service, ever.
    // Description generation is always template-based so sellers can audit
    // exactly what text will appear on Whatnot. Adding LLM calls here is
    // explicitly prohibited per the Surprise Set design spec (§ 4).
    public sealed class SurpriseSetDescriptionGenerator : ISurpriseSetDescriptionGenerator
    {
        public string Generate(SurpriseSet set, IList<Card> cards)
        {
            var sb = new StringBuilder();

            // Header — prefer show name if the seller has one
            var displayName = !string.IsNullOrWhiteSpace(set.ShowName) ? set.ShowName : set.Name;
            sb.AppendLine($"{displayName} — Surprise Set");
            sb.AppendLine();

            // Spot count and price
            var count = cards.Count;
            sb.AppendLine($"- {count} card{(count != 1 ? "s" : "")} in this set");
            if (set.SpotPrice > 0m)
                sb.AppendLine($"- Spot price: {set.SpotPrice:C}");

            // Condition
            if (!string.IsNullOrWhiteSpace(set.SharedCondition))
                sb.AppendLine($"- Condition: {set.SharedCondition}");

            // Graded vs raw breakdown
            if (count > 0)
            {
                var gradedCount = cards.Count(c => c.IsGraded);
                if (gradedCount == count)
                    sb.AppendLine("- All cards are professionally graded");
                else if (gradedCount > 0)
                    sb.AppendLine($"- {gradedCount} graded, {count - gradedCount} raw (ungraded)");
                else
                    sb.AppendLine("- Raw (ungraded) cards");
            }

            // Sports
            var sports = cards
                .Where(c => c.Sport != null)
                .Select(c => c.Sport!.Value.ToString())
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            if (sports.Count == 1)
                sb.AppendLine($"- Sport: {sports[0]}");
            else if (sports.Count > 1)
                sb.AppendLine($"- Sports: {string.Join(", ", sports)}");

            // Highlights (attributes present in the set)
            var highlights = new List<string>();
            if (cards.Any(c => c.IsAuto)) highlights.Add("autographs");
            if (cards.Any(c => c.IsRookie)) highlights.Add("rookies");
            if (cards.Any(c => c.IsRelic)) highlights.Add("relics");
            if (cards.Any(c => !string.IsNullOrEmpty(c.SerialNumbered))) highlights.Add("serial-numbered cards");

            if (highlights.Count > 0)
                sb.AppendLine($"- Highlights: {string.Join(", ", highlights)}");

            // Shipping
            if (!string.IsNullOrWhiteSpace(set.SharedShippingProfile))
                sb.AppendLine($"- Shipping: {set.SharedShippingProfile}");

            // Seller notes (freeform, added verbatim)
            if (!string.IsNullOrWhiteSpace(set.Notes))
            {
                sb.AppendLine();
                sb.AppendLine(set.Notes.Trim());
            }

            // Standard footer
            sb.AppendLine();
            sb.AppendLine("Cards ship securely packaged. Spots are randomly assigned — every spot has an equal chance of receiving any card in the set.");

            return sb.ToString().TrimEnd();
        }
    }
}
