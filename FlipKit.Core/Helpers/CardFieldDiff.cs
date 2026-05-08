using System;
using FlipKit.Core.Models;

namespace FlipKit.Core.Helpers
{
    // Counts user corrections between an AI-produced card and its post-edit
    // state. Drives the "user correction" penalty in the model accuracy
    // scoreboard — fields where the user changed the value the model produced
    // are votes against that model's accuracy.
    public static class CardFieldDiff
    {
        // Compares the 18 fields the LLM actually produces (matches the json
        // schema the scanner uses). Cost / sale / pricing / listing fields
        // are excluded — those are user-entered and have nothing to do with
        // model output. A "user correction" is any field where:
        //   - the model produced a non-default value, AND
        //   - the user changed it to a different non-empty value.
        // A field the model left blank that the user filled in still counts:
        // the model failed to produce it. A field the user blanked out (clearing
        // a wrong value) also counts. A field unchanged is not a correction.
        public static int CountUserCorrections(Card before, Card after)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));

            var corrections = 0;

            // String fields — case-insensitive compare with null/empty normalisation.
            corrections += DiffString(before.PlayerName, after.PlayerName);
            corrections += DiffString(before.CardNumber, after.CardNumber);
            corrections += DiffString(before.Manufacturer, after.Manufacturer);
            corrections += DiffString(before.Brand, after.Brand);
            corrections += DiffString(before.SetName, after.SetName);
            corrections += DiffString(before.Team, after.Team);
            corrections += DiffString(before.VariationType, after.VariationType);
            corrections += DiffString(before.ParallelName, after.ParallelName);
            corrections += DiffString(before.SerialNumbered, after.SerialNumbered);
            corrections += DiffString(before.GradeCompany, after.GradeCompany);
            corrections += DiffString(before.GradeValue, after.GradeValue);

            // Year (int?) — null vs 0 vs value all distinct.
            if (before.Year != after.Year) corrections++;

            // Sport (Sport?) — direct compare.
            if (before.Sport != after.Sport) corrections++;

            // Booleans — five toggleable identity flags the LLM produces.
            if (before.IsRookie != after.IsRookie) corrections++;
            if (before.IsAuto != after.IsAuto) corrections++;
            if (before.IsRelic != after.IsRelic) corrections++;
            if (before.IsShortPrint != after.IsShortPrint) corrections++;
            if (before.IsGraded != after.IsGraded) corrections++;

            return corrections;
        }

        private static int DiffString(string? before, string? after)
        {
            // Treat null and empty as equivalent so an unset field doesn't
            // register as different from an empty string.
            var b = string.IsNullOrWhiteSpace(before) ? null : before.Trim();
            var a = string.IsNullOrWhiteSpace(after) ? null : after.Trim();
            return string.Equals(b, a, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        }
    }
}
