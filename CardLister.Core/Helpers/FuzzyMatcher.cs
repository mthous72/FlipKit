using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FlipKit.Core.Helpers
{
    public static class FuzzyMatcher
    {
        private static readonly Dictionary<string, string> ParallelAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "refractors", "refractor" },
            { "xfractor", "x-fractor" },
            { "holo", "holographic" },
            { "rr", "rated rookie" },
            { "sp", "short print" },
            { "ssp", "super short print" },
            { "rwb", "red white blue" },
            { "red white & blue", "red white blue" },
            { "red, white & blue", "red white blue" },
            { "gold vinyl", "gold vinyl 1/1" },
            { "black finite", "black finite 1/1" },
            { "press proof", "press proof silver" },
            { "neon green", "neon green" },
            { "disco", "disco prizm" },
            { "shimmer", "shimmer" },
            { "mojo", "mojo refractor" },
            { "wave", "wave refractor" },
            { "aqua", "aqua" },
            { "camo", "camo" },
        };

        public static double Match(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return 0.0;

            var normA = Normalize(a);
            var normB = Normalize(b);

            if (normA == normB)
                return 1.0;

            var distance = LevenshteinDistance(normA, normB);
            var maxLen = Math.Max(normA.Length, normB.Length);

            if (maxLen == 0)
                return 1.0;

            return 1.0 - ((double)distance / maxLen);
        }

        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var result = input.ToLowerInvariant();
            result = Regex.Replace(result, @"[^\w\s/]", "");
            result = Regex.Replace(result, @"\s+", " ");
            return result.Trim();
        }

        public static string NormalizeCardNumber(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var result = input.Trim();
            result = result.TrimStart('#');
            result = result.TrimStart('0');

            if (string.IsNullOrEmpty(result))
                result = "0";

            return result;
        }

        public static string NormalizeParallelName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var normalized = Normalize(input);

            if (ParallelAliases.TryGetValue(normalized, out var alias))
                return Normalize(alias);

            // Also check original input before normalization
            if (ParallelAliases.TryGetValue(input.Trim(), out var alias2))
                return Normalize(alias2);

            return normalized;
        }

        private static int LevenshteinDistance(string s, string t)
        {
            var n = s.Length;
            var m = t.Length;

            if (n == 0) return m;
            if (m == 0) return n;

            var d = new int[n + 1, m + 1];

            for (var i = 0; i <= n; i++)
                d[i, 0] = i;

            for (var j = 0; j <= m; j++)
                d[0, j] = j;

            for (var i = 1; i <= n; i++)
            {
                for (var j = 1; j <= m; j++)
                {
                    var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }
    }
}
