using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlipKit.Core.Services.Export
{
    /// <summary>
    /// Loads the embedded eBay Sports Trading Cards "Create new listings" template
    /// (header rows only — no data rows). Exposes the raw header bytes for verbatim
    /// prepending and a parsed column-name → index map for data-row construction.
    ///
    /// The shipped template uses CR-only line endings and starts with a UTF-8 BOM;
    /// both are preserved so eBay's parser sees exactly what they shipped.
    /// </summary>
    public class EbayTemplateProvider
    {
        private const string ResourceName = "FlipKit.Core.Resources.Export.ebay_template_header.csv";

        private readonly Lazy<TemplateData> _data = new(Load);

        /// <summary>Raw bytes of the entire header (rows 0..N-1) including BOM and CR separators.</summary>
        public byte[] HeaderBytes => _data.Value.HeaderBytes;

        /// <summary>The parsed column header line (row 1), in order, as it appears in the template.</summary>
        public IReadOnlyList<string> Columns => _data.Value.Columns;

        /// <summary>Returns the column index for the given exact name, or -1 if not present.</summary>
        public int IndexOf(string columnName) =>
            _data.Value.ColumnIndex.TryGetValue(columnName, out var idx) ? idx : -1;

        /// <summary>
        /// Returns the literal column name beginning with the given prefix, or null if none match.
        /// Use for finding the verbose <c>*Action(SiteID=...)</c> header without hard-coding the
        /// full string (eBay may revise the parenthesized parameters between template versions).
        /// </summary>
        public string? FindColumnStartingWith(string prefix) =>
            _data.Value.Columns.FirstOrDefault(c => c.StartsWith(prefix, StringComparison.Ordinal));

        private static TemplateData Load()
        {
            var asm = typeof(EbayTemplateProvider).Assembly;
            using var stream = asm.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded resource not found: {ResourceName}. " +
                    $"Available resources: {string.Join(", ", asm.GetManifestResourceNames())}");

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();

            // The shipped template uses CR-only line endings. Decode as UTF-8 (BOM auto-handled
            // by the StreamReader pattern) and split on CR to get rows. We only need the second
            // row (the column header) for parsing; the rest is opaque-passthrough.
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            // Strip BOM if present so the column-header row doesn't get a stray BOM prefix
            // when we parse it. The raw bytes still keep the BOM — that's only for write-out.
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);

            var rows = SplitOnCrOrCrlf(text);
            if (rows.Count < 2)
                throw new InvalidOperationException($"eBay template too short: {rows.Count} rows.");

            var columns = ParseCsvRow(rows[1]);
            var columnIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < columns.Count; i++)
            {
                // Skip duplicates (shouldn't happen, but be defensive — first occurrence wins)
                if (!columnIndex.ContainsKey(columns[i]))
                    columnIndex[columns[i]] = i;
            }

            return new TemplateData(bytes, columns, columnIndex);
        }

        // Splits on CR or CRLF (treats \r\n as a single separator). Avoids the standard
        // string.Split which would split CRLF into two rows.
        private static List<string> SplitOnCrOrCrlf(string text)
        {
            var rows = new List<string>();
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    rows.Add(text.Substring(start, i - start));
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    start = i + 1;
                }
                else if (text[i] == '\n')
                {
                    rows.Add(text.Substring(start, i - start));
                    start = i + 1;
                }
            }
            if (start < text.Length) rows.Add(text.Substring(start));
            return rows;
        }

        // Minimal RFC 4180 CSV row parser — handles quoted fields with internal commas.
        // The eBay column-header row contains `*Action(SiteID=US|Country=US|...)` (no commas
        // inside parens) and other names with `:`/`-`/digits, but no embedded commas, so
        // even a naive split would work. The parser handles quoting anyway in case eBay
        // ever adds a column name with a comma.
        private static List<string> ParseCsvRow(string row)
        {
            var fields = new List<string>();
            var sb = new System.Text.StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < row.Length; i++)
            {
                char c = row[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < row.Length && row[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                    else if (c == '"' && sb.Length == 0) inQuotes = true;
                    else sb.Append(c);
                }
            }
            fields.Add(sb.ToString());
            return fields;
        }

        private sealed class TemplateData
        {
            public byte[] HeaderBytes { get; }
            public IReadOnlyList<string> Columns { get; }
            public IReadOnlyDictionary<string, int> ColumnIndex { get; }

            public TemplateData(byte[] headerBytes, IReadOnlyList<string> columns, IReadOnlyDictionary<string, int> columnIndex)
            {
                HeaderBytes = headerBytes;
                Columns = columns;
                ColumnIndex = columnIndex;
            }
        }
    }
}
