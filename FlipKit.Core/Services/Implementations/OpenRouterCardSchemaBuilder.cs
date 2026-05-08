using System.Collections.Generic;
using System.Linq;

namespace FlipKit.Core.Services.Implementations
{
    /// <summary>
    /// Builds the json_schema body the scanner attaches to its
    /// <c>response_format</c>. Mirrors the <see cref="ApiModels.ScannedCardData"/>
    /// shape and adds an <c>enum</c> constraint on <c>parallel_name</c> when the
    /// caller has a candidate list — that's the hard stop that prevents the LLM
    /// from inventing parallel names ("Sparkly Glittery Diamond") regardless of
    /// what the prompt suggests. Caller is expected to pass the same candidate
    /// list that's surfaced in the prompt preamble so the two stay aligned.
    ///
    /// Returns plain anonymous-object trees so System.Text.Json can serialize
    /// them; OpenRouter forwards the schema verbatim to the provider.
    /// </summary>
    public static class OpenRouterCardSchemaBuilder
    {
        /// <summary>
        /// Builds the full <c>response_format</c> envelope with strict mode on.
        /// </summary>
        /// <param name="parallelCandidates">When non-empty, <c>parallel_name</c>
        /// is constrained to one of these strings or <c>null</c>. When empty,
        /// <c>parallel_name</c> stays as a free-form nullable string (used when
        /// we don't know the manufacturer at all and don't want to over-constrain).</param>
        public static object BuildResponseFormat(IReadOnlyList<string> parallelCandidates)
        {
            return new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "card_extract",
                    strict = true,
                    schema = BuildSchema(parallelCandidates),
                },
            };
        }

        /// <summary>
        /// Exposed for tests so they can inspect the generated property set
        /// without round-tripping through <see cref="BuildResponseFormat"/>.
        /// </summary>
        public static object BuildSchema(IReadOnlyList<string> parallelCandidates)
        {
            // Type-arrays of ["string", "null"] let strict mode accept legitimate
            // unknowns (player can't be read, etc.) without rejecting the response.
            var nullableString = new[] { "string", "null" };
            var nullableInt = new[] { "integer", "null" };
            var nullableBool = new[] { "boolean", "null" };

            object parallelNameProp = parallelCandidates.Count > 0
                ? new
                {
                    type = nullableString,
                    @enum = parallelCandidates.Cast<string?>().Append(null).ToArray(),
                }
                : new { type = nullableString };

            // Properties order mirrors ScannedCardData top-to-bottom so a quick
            // diff catches drift if either side gains a field.
            var properties = new Dictionary<string, object>
            {
                ["player_name"] = new { type = nullableString },
                ["card_number"] = new { type = nullableString },
                ["year"] = new { type = nullableInt },
                ["sport"] = new { type = nullableString },
                ["manufacturer"] = new { type = nullableString },
                ["brand"] = new { type = nullableString },
                ["set_name"] = new { type = nullableString },
                ["team"] = new { type = nullableString },
                ["variation_type"] = new
                {
                    type = nullableString,
                    @enum = new string?[] { "Base", "Parallel", "Insert", "Refractor", "Auto", "Relic", null },
                },
                ["parallel_name"] = parallelNameProp,
                ["serial_numbered"] = new { type = nullableString },
                ["is_rookie"] = new { type = nullableBool },
                ["is_auto"] = new { type = nullableBool },
                ["is_relic"] = new { type = nullableBool },
                ["is_short_print"] = new { type = nullableBool },
                ["is_graded"] = new { type = nullableBool },
                ["grade_company"] = new { type = nullableString },
                ["grade_value"] = new { type = nullableString },
                ["auto_grade"] = new { type = nullableString },
                ["cert_number"] = new { type = nullableString },
                ["condition_notes"] = new { type = nullableString },
                ["visual_cues"] = new
                {
                    type = new[] { "object", "null" },
                    additionalProperties = false,
                    properties = new Dictionary<string, object>
                    {
                        ["border_color"] = new { type = nullableString },
                        ["card_finish"] = new { type = nullableString },
                        ["has_foil"] = new { type = nullableBool },
                        ["has_refractor_pattern"] = new { type = nullableBool },
                        ["has_serial_number"] = new { type = nullableBool },
                        ["serial_number_location"] = new { type = nullableString },
                        ["background_pattern"] = new { type = nullableString },
                        ["text_color"] = new { type = nullableString },
                        ["has_rookie_logo"] = new { type = nullableBool },
                        ["has_auto_sticker"] = new { type = nullableBool },
                        ["has_relic_swatch"] = new { type = nullableBool },
                    },
                    required = new[]
                    {
                        "border_color", "card_finish", "has_foil", "has_refractor_pattern",
                        "has_serial_number", "serial_number_location", "background_pattern",
                        "text_color", "has_rookie_logo", "has_auto_sticker", "has_relic_swatch",
                    },
                },
                ["all_visible_text"] = new
                {
                    type = new[] { "array", "null" },
                    items = new { type = "string" },
                },
                ["confidence"] = new
                {
                    type = new[] { "object", "null" },
                    additionalProperties = new
                    {
                        type = "string",
                        @enum = new[] { "high", "medium", "low" },
                    },
                },
            };

            return new
            {
                type = "object",
                additionalProperties = false,
                required = properties.Keys.ToArray(),
                properties,
            };
        }
    }
}
