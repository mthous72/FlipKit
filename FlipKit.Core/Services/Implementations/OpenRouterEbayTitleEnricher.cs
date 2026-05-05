using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Services.ApiModels;
using FlipKit.Core.Services.Scanning;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services.Implementations
{
    /// <summary>
    /// Text-only OpenRouter implementation of <see cref="IEbayTitleEnricher"/>.
    /// Batches up to <see cref="BatchSize"/> titles per request to keep total
    /// LLM cost down on a typical 200-listing import. Uses the configured
    /// default vision model (which all support text-only too) so users don't
    /// need a separate text model setting.
    /// </summary>
    public class OpenRouterEbayTitleEnricher : IEbayTitleEnricher
    {
        private const string ApiUrl = "https://openrouter.ai/api/v1/chat/completions";
        private const int BatchSize = 10;
        private const int MaxTokensPerBatch = 2048;

        private readonly HttpClient _httpClient;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<OpenRouterEbayTitleEnricher> _logger;

        public OpenRouterEbayTitleEnricher(
            HttpClient httpClient,
            ISettingsService settingsService,
            ILogger<OpenRouterEbayTitleEnricher> logger)
        {
            _httpClient = httpClient;
            _settingsService = settingsService;
            _logger = logger;
        }

        public async Task<IReadOnlyList<EbayTitleEnrichment>> EnrichAsync(
            IReadOnlyList<string> titles,
            CancellationToken ct = default)
        {
            if (titles is null || titles.Count == 0)
                return Array.Empty<EbayTitleEnrichment>();

            var settings = _settingsService.Load();
            if (string.IsNullOrWhiteSpace(settings.OpenRouterApiKey))
                throw new InvalidOperationException(
                    "OpenRouter API key is not configured. Go to Settings to enter your key before importing eBay listings.");

            var model = ResolveModel(settings.DefaultModel);

            // Pre-fill an output buffer so partial-batch failures still return
            // the right shape (caller relies on positional 1:1 with input).
            var results = new EbayTitleEnrichment?[titles.Count];

            for (int start = 0; start < titles.Count; start += BatchSize)
            {
                ct.ThrowIfCancellationRequested();
                var batchTitles = titles.Skip(start).Take(BatchSize).ToList();

                IReadOnlyList<EbayTitleEnrichment>? batch;
                try
                {
                    batch = await EnrichBatchAsync(batchTitles, model, settings.OpenRouterApiKey!, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Enrichment batch starting at index {Start} failed; filling with empty enrichments so the import can continue.",
                        start);
                    batch = batchTitles.Select(_ => new EbayTitleEnrichment(null, null, null, null, null)).ToList();
                }

                for (int i = 0; i < batch.Count && start + i < results.Length; i++)
                    results[start + i] = batch[i];
            }

            // Replace any leftover nulls (defensive — every slot should have been set above).
            for (int i = 0; i < results.Length; i++)
                results[i] ??= new EbayTitleEnrichment(null, null, null, null, null);

            return results!;
        }

        private async Task<IReadOnlyList<EbayTitleEnrichment>> EnrichBatchAsync(
            List<string> titles,
            string model,
            string apiKey,
            CancellationToken ct)
        {
            var prompt = BuildPrompt(titles);

            var request = new OpenRouterRequest
            {
                Model = model,
                MaxTokens = MaxTokensPerBatch,
                Messages = new List<OpenRouterMessage>
                {
                    new() { Role = "user", Content = prompt },
                },
            };

            var json = JsonSerializer.Serialize(request);
            using var http = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            http.Headers.Add("Authorization", $"Bearer {apiKey}");
            http.Headers.Add("X-Title", "FlipKit");

            var resp = await _httpClient.SendAsync(http, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"OpenRouter API error ({(int)resp.StatusCode} {resp.StatusCode}): {body}");

            var apiResponse = JsonSerializer.Deserialize<OpenRouterResponse>(body);
            var content = apiResponse?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("OpenRouter returned no enrichment content.");

            return ParseResponse(content, titles.Count);
        }

        public static string BuildPrompt(IReadOnlyList<string> titles)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are a sports-card listing parser. For each numbered eBay listing title below, extract these fields:");
            sb.AppendLine("- playerName: athlete on the card (omit team city/nickname)");
            sb.AppendLine("- brand: card line / brand name (e.g. \"Prizm\", \"Optic\", \"Mosaic\", \"Chronicles\"). NOT the manufacturer.");
            sb.AppendLine("- setName: full set or subset name when distinct from brand (e.g. \"Premier Level\", \"Rookies & Stars\")");
            sb.AppendLine("- parallelName: parallel/insert variant (e.g. \"Silver\", \"Zebra\", \"Red Mojo\"). null if Base.");
            sb.AppendLine("- team: team name (e.g. \"Colts\", \"Lakers\")");
            sb.AppendLine();
            sb.AppendLine("Reply ONLY with a JSON array, one object per title, in the same order. Use null for fields you cannot determine. No prose, no markdown.");
            sb.AppendLine();
            sb.AppendLine("Titles:");
            for (int i = 0; i < titles.Count; i++)
                sb.AppendLine($"{i + 1}. {titles[i]}");
            sb.AppendLine();
            sb.AppendLine("Example response shape:");
            sb.AppendLine("[{\"playerName\":\"Patrick Mahomes\",\"brand\":\"Prizm\",\"setName\":null,\"parallelName\":\"Silver\",\"team\":\"Chiefs\"}]");
            return sb.ToString();
        }

        public static IReadOnlyList<EbayTitleEnrichment> ParseResponse(string content, int expectedCount)
        {
            var trimmed = StripCodeFence(content).Trim();

            // Some models prefix prose despite the instruction. Locate the first '[' / '{'.
            var firstArray = trimmed.IndexOf('[');
            var firstObject = trimmed.IndexOf('{');
            int start;
            if (firstArray >= 0 && (firstObject < 0 || firstArray < firstObject))
                start = firstArray;
            else if (firstObject >= 0)
                start = firstObject;
            else
                throw new InvalidOperationException("Enrichment response did not contain JSON.");

            var jsonBody = trimmed[start..];

            // Single-object response — wrap into array.
            if (jsonBody.StartsWith("{"))
                jsonBody = "[" + jsonBody + "]";

            using var doc = JsonDocument.Parse(jsonBody);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Enrichment response root was not an array.");

            var list = new List<EbayTitleEnrichment>(expectedCount);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                list.Add(new EbayTitleEnrichment(
                    PlayerName: ReadString(el, "playerName"),
                    Brand: ReadString(el, "brand"),
                    SetName: ReadString(el, "setName"),
                    ParallelName: ReadString(el, "parallelName"),
                    Team: ReadString(el, "team")));
            }

            // Pad if model returned fewer than requested so callers can rely on 1:1 indexing.
            while (list.Count < expectedCount)
                list.Add(new EbayTitleEnrichment(null, null, null, null, null));

            return list;
        }

        private static string? ReadString(JsonElement el, string field)
        {
            if (!el.TryGetProperty(field, out var v) || v.ValueKind == JsonValueKind.Null)
                return null;
            if (v.ValueKind != JsonValueKind.String) return null;
            var s = v.GetString();
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        private static string StripCodeFence(string content)
        {
            content = content.Trim();
            if (content.StartsWith("```"))
            {
                // Remove leading ```json or ``` and the trailing ```
                var firstNewline = content.IndexOf('\n');
                if (firstNewline > 0) content = content[(firstNewline + 1)..];
                if (content.EndsWith("```"))
                    content = content[..^3];
            }
            return content.Trim();
        }

        private static string ResolveModel(string? defaultModel)
        {
            if (string.IsNullOrWhiteSpace(defaultModel) || defaultModel == OpenRouterModelDefaults.AutoModelValue)
                return OpenRouterModelDefaults.DefaultFreeModelId;
            return defaultModel;
        }
    }
}
