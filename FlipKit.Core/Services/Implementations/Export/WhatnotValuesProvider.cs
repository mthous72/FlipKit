using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlipKit.Core.Services.Export
{
    /// <summary>
    /// Loads and caches Whatnot's reference data (categories, sub-categories, conditions,
    /// shipping profiles, hazmat values) from the embedded whatnot_values.json resource.
    /// Singleton-friendly — the data is immutable and ~35 KB.
    /// </summary>
    public class WhatnotValuesProvider
    {
        private const string ResourceName = "FlipKit.Core.Resources.Export.whatnot_values.json";

        private readonly Lazy<WhatnotValues> _values = new(Load);

        public IReadOnlyList<string> Categories => _values.Value.Categories;
        public IReadOnlyList<string> ShippingProfiles => _values.Value.ShippingProfiles;
        public IReadOnlyList<string> Hazmat => _values.Value.Hazmat;
        public IReadOnlyDictionary<string, List<string>> Subcategories => _values.Value.Subcategories;
        public IReadOnlyDictionary<string, List<string>> Conditions => _values.Value.Conditions;

        public bool IsValidCategory(string? category) =>
            !string.IsNullOrEmpty(category) && _values.Value.CategorySet.Contains(category);

        public bool IsValidSubcategory(string category, string? subcategory)
        {
            var hasSubcategoryList = _values.Value.Subcategories.TryGetValue(category, out var subs)
                                     && subs != null && subs.Count > 0;
            if (string.IsNullOrEmpty(subcategory))
            {
                // Blank is OK only when the category has no sub-categories defined.
                // Whatnot rejects rows with "Subcategory not provided" otherwise.
                return !hasSubcategoryList;
            }
            return hasSubcategoryList && subs!.Contains(subcategory);
        }

        /// <summary>
        /// Returns the allowed Condition values for the given category/sub-category combo,
        /// using the spec's fallback chain: sub-category → category → empty list.
        /// </summary>
        public IReadOnlyList<string> ConditionsFor(string? category, string? subcategory)
        {
            var conds = _values.Value.Conditions;
            if (!string.IsNullOrEmpty(subcategory) && conds.TryGetValue(subcategory, out var bySub))
                return bySub;
            if (!string.IsNullOrEmpty(category) && conds.TryGetValue(category, out var byCat))
                return byCat;
            return Array.Empty<string>();
        }

        public bool IsValidShippingProfile(string? profile) =>
            !string.IsNullOrEmpty(profile) && _values.Value.ShippingProfileSet.Contains(profile);

        private static WhatnotValues Load()
        {
            var asm = typeof(WhatnotValuesProvider).Assembly;
            using var stream = asm.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded resource not found: {ResourceName}. " +
                    $"Available resources: {string.Join(", ", asm.GetManifestResourceNames())}");

            var raw = JsonSerializer.Deserialize<RawWhatnotValues>(stream)
                ?? throw new InvalidOperationException("whatnot_values.json deserialized to null.");

            return new WhatnotValues(
                raw.Categories ?? new(),
                raw.ShippingProfiles ?? new(),
                raw.Hazmat ?? new(),
                raw.Subcategories ?? new(),
                raw.Conditions ?? new());
        }

        // Cached parsed view, plus hash sets for fast membership tests.
        private sealed class WhatnotValues
        {
            public IReadOnlyList<string> Categories { get; }
            public IReadOnlyList<string> ShippingProfiles { get; }
            public IReadOnlyList<string> Hazmat { get; }
            public IReadOnlyDictionary<string, List<string>> Subcategories { get; }
            public IReadOnlyDictionary<string, List<string>> Conditions { get; }
            public HashSet<string> CategorySet { get; }
            public HashSet<string> ShippingProfileSet { get; }

            public WhatnotValues(
                List<string> categories,
                List<string> shippingProfiles,
                List<string> hazmat,
                Dictionary<string, List<string>> subcategories,
                Dictionary<string, List<string>> conditions)
            {
                Categories = categories;
                ShippingProfiles = shippingProfiles;
                Hazmat = hazmat;
                Subcategories = subcategories;
                Conditions = conditions;
                CategorySet = new HashSet<string>(categories, StringComparer.Ordinal);
                ShippingProfileSet = new HashSet<string>(shippingProfiles, StringComparer.Ordinal);
            }
        }

        private sealed class RawWhatnotValues
        {
            [JsonPropertyName("categories")]        public List<string>? Categories { get; set; }
            [JsonPropertyName("shipping_profiles")] public List<string>? ShippingProfiles { get; set; }
            [JsonPropertyName("hazmat")]            public List<string>? Hazmat { get; set; }
            [JsonPropertyName("subcategories")]     public Dictionary<string, List<string>>? Subcategories { get; set; }
            [JsonPropertyName("conditions")]        public Dictionary<string, List<string>>? Conditions { get; set; }
        }
    }
}
