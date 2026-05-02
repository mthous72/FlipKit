using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Helpers
{
    /// <summary>
    /// Fills in <see cref="Card.WhatnotCategory"/> and <see cref="Card.WhatnotSubcategory"/>
    /// from other card fields when the user hasn't set them explicitly. Runs at save
    /// time so the values are persisted; the export validator then sees a complete
    /// row and Whatnot's "Subcategory not provided" error stops happening.
    ///
    /// Only fills blanks — never overrides a value the user already chose.
    /// </summary>
    public static class WhatnotCategoryDefaulter
    {
        public static void ApplyDefaults(Card card)
        {
            // Default category — Sports Cards is the legacy default; leave as-is unless empty.
            if (string.IsNullOrEmpty(card.WhatnotCategory))
                card.WhatnotCategory = "Sports Cards";

            if (!string.IsNullOrEmpty(card.WhatnotSubcategory))
                return;

            // Sports Cards: derive from the Sport enum.
            if (card.WhatnotCategory == "Sports Cards" && card.Sport.HasValue)
            {
                card.WhatnotSubcategory = SportToSingles(card.Sport.Value);
            }

            // Trading Card Games: no reliable derivation — the user has to pick
            // (Pokémon Cards / Magic: The Gathering / Yu-Gi-Oh! Cards / Lorcana / etc.).
            // The validator catches it with a clear message naming examples.
        }

        private static string SportToSingles(Sport sport) => sport switch
        {
            Sport.Baseball   => "Baseball Singles",
            Sport.Basketball => "Basketball Singles",
            Sport.Football   => "Football Singles",
            Sport.Hockey     => "Hockey Singles",
            Sport.Soccer     => "Soccer Singles",
            _                => "Other Sports Cards",
        };
    }
}
