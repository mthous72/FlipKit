using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services.ApiModels;
using FlipKit.Core.Services.Implementations;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services
{
    public class OpenRouterScannerService : IScannerService
    {
        private const string ApiUrl = "https://openrouter.ai/api/v1/chat/completions";

        // Phase 5b — model lists moved to OpenRouterModelDefaults so the scanner's
        // local fallback chain and the live-fetch catalog's fallback path read from
        // the same source. Use `OpenRouterModelDefaults.FallbackFreeModelIds` for
        // chain construction, and `OpenRouterModelDefaults.DefaultFreeModelId` for
        // the baseline default.

        private const string ScanPromptBody = @"
Return ONLY a JSON object with these exact fields (use null for unknown values):

{
  ""player_name"": ""Full player name"",
  ""card_number"": ""Card number without # symbol"",
  ""year"": 2024,
  ""sport"": ""Football|Baseball|Basketball|Hockey|Soccer|MMA|Wrestling|Golf|Tennis|Racing"",
  ""manufacturer"": ""Panini|Topps|Upper Deck|Leaf"",
  ""brand"": ""Sub-brand (Prizm, Donruss, Chrome, etc.)"",
  ""set_name"": ""Full set name if visible"",
  ""team"": ""Team name"",
  ""variation_type"": ""Base|Parallel|Insert|Refractor|Auto|Relic"",
  ""parallel_name"": ""Color/pattern name (Silver, Blue, Gold, etc.) or null"",
  ""serial_numbered"": ""Print run as string (/99, /25, 1/1) or null"",
  ""is_rookie"": true or false,
  ""is_auto"": true or false,
  ""is_relic"": true or false,
  ""is_short_print"": true or false,
  ""is_graded"": true or false,
  ""grade_company"": ""PSA|BGS|CGC|CCG|SGC or null"",
  ""grade_value"": ""Numeric grade (10, 9.5, 9, etc.) or Authentic or null"",
  ""auto_grade"": ""Autograph grade if separate from card grade, or null"",
  ""cert_number"": ""Certificate/serial number on the slab or null"",
  ""condition_notes"": ""Any visible condition issues"",
  ""visual_cues"": {
    ""border_color"": ""Color of the card border or null"",
    ""card_finish"": ""matte|glossy|chrome|holographic|prizm or null"",
    ""has_foil"": true or false,
    ""has_refractor_pattern"": true or false,
    ""has_serial_number"": true or false,
    ""serial_number_location"": ""Location of serial number or null"",
    ""background_pattern"": ""Description of background pattern or null"",
    ""text_color"": ""Color of player name text or null"",
    ""has_rookie_logo"": true or false,
    ""has_auto_sticker"": true or false,
    ""has_relic_swatch"": true or false
  },
  ""all_visible_text"": [""Every line of text visible on the card""],
  ""confidence"": {
    ""player_name"": ""high|medium|low"",
    ""card_number"": ""high|medium|low"",
    ""year"": ""high|medium|low"",
    ""manufacturer"": ""high|medium|low"",
    ""brand"": ""high|medium|low"",
    ""variation_type"": ""high|medium|low"",
    ""parallel_name"": ""high|medium|low""
  }
}

Identification tips:
- ""RC"" or ""Rated Rookie"" logo = rookie card
- Serial numbers are usually printed at bottom (e.g., 045/199)
- Panini brands: Prizm, Donruss, Mosaic, Select, Optic, Contenders, Phoenix
- Topps brands: Chrome, Heritage, Stadium Club, Finest, Bowman, Inception
- Look for rainbow/shimmer effects to identify parallels
- Actual ink/sticker signature = auto
- Jersey swatch or memorabilia piece = relic
- Report ALL text you can read on the card in all_visible_text
- For confidence: high = clearly visible/certain, medium = partially visible/likely, low = guessing/unclear
- Graded cards are in hard plastic ""slabs"" with a label showing company, grade, and cert number
- PSA labels are red/white, BGS are silver/black, CGC are green, SGC are gold
- Look for numeric grade prominently displayed on the label (e.g., ""GEM MINT 10"", ""9.5"")
- ""Authentic"" means verified genuine but not numerically graded
- If graded, the grade company and value should be clearly readable on the label

Return ONLY the JSON, no other text or markdown.";

        // Tighter prompt body for the Enhance flow — same JSON response schema as
        // ScanPromptBody so MapToCard stays drop-in, but skips the identification
        // tips and confidence-grading guidance the LLM doesn't need when most
        // fields are already confirmed by the OCR + checklist directory pass.
        // Pairs with BuildLockedHintPreamble: that block tells the LLM which
        // fields are verified, this body tells it where to spend its vision.
        private const string EnhancePromptBody = @"
Return ONLY a JSON object with the same schema as a fresh scan, BUT:
  * Echo every CONFIRMED field listed in the preamble VERBATIM. Do not
    second-guess them based on the image — they have been validated against
    our checklist database and are correct.
  * Spend your full attention on the visual-pattern fields: variation_type,
    parallel_name (refractor / wave / prizm / mojo / sparkle / disco / etc.),
    visual_cues.* (border_color, card_finish, has_foil, has_refractor_pattern,
    background_pattern, text_color), and the grade / auto / relic flags if
    you can see physical evidence in the image.
  * Use the all_visible_text we already extracted (preamble) — append any
    additional text you can read but don't re-derive what's already there.

{
  ""player_name"": ""Full player name"",
  ""card_number"": ""Card number without # symbol"",
  ""year"": 2024,
  ""sport"": ""Football|Baseball|Basketball|Hockey|Soccer|MMA|Wrestling|Golf|Tennis|Racing"",
  ""manufacturer"": ""Panini|Topps|Upper Deck|Leaf"",
  ""brand"": ""Sub-brand (Prizm, Donruss, Chrome, etc.)"",
  ""set_name"": ""Full set name if visible"",
  ""team"": ""Team name"",
  ""variation_type"": ""Base|Parallel|Insert|Refractor|Auto|Relic"",
  ""parallel_name"": ""Color/pattern name (Silver, Blue, Gold, etc.) or null"",
  ""serial_numbered"": ""Print run as string (/99, /25, 1/1) or null"",
  ""is_rookie"": true or false,
  ""is_auto"": true or false,
  ""is_relic"": true or false,
  ""is_short_print"": true or false,
  ""is_graded"": true or false,
  ""grade_company"": ""PSA|BGS|CGC|CCG|SGC or null"",
  ""grade_value"": ""Numeric grade (10, 9.5, 9, etc.) or Authentic or null"",
  ""auto_grade"": ""Autograph grade if separate from card grade, or null"",
  ""cert_number"": ""Certificate/serial number on the slab or null"",
  ""condition_notes"": ""Any visible condition issues"",
  ""visual_cues"": {
    ""border_color"": ""Color of the card border or null"",
    ""card_finish"": ""matte|glossy|chrome|holographic|prizm or null"",
    ""has_foil"": true or false,
    ""has_refractor_pattern"": true or false,
    ""has_serial_number"": true or false,
    ""serial_number_location"": ""Location of serial number or null"",
    ""background_pattern"": ""Description of background pattern or null"",
    ""text_color"": ""Color of player name text or null"",
    ""has_rookie_logo"": true or false,
    ""has_auto_sticker"": true or false,
    ""has_relic_swatch"": true or false
  },
  ""all_visible_text"": [""Every line of text visible on the card""],
  ""confidence"": {
    ""player_name"": ""high|medium|low"",
    ""card_number"": ""high|medium|low"",
    ""year"": ""high|medium|low"",
    ""manufacturer"": ""high|medium|low"",
    ""brand"": ""high|medium|low"",
    ""variation_type"": ""high|medium|low"",
    ""parallel_name"": ""high|medium|low""
  }
}

Return ONLY the JSON, no other text or markdown.";

        // Lightweight prompt for Surprise Set lot-scanning — just enough to identify
        // and label a card without the full extraction cost.
        private const string QuickScanPromptBody = @"
Return ONLY a JSON object identifying this sports card. Use null for unknown values:

{
  ""player_name"": ""Full player name"",
  ""year"": 2024,
  ""manufacturer"": ""Panini|Topps|Upper Deck|Leaf or null"",
  ""brand"": ""Prizm|Chrome|Donruss etc. or null"",
  ""set_name"": ""Set name if visible or null"",
  ""card_number"": ""Card number without # or null"",
  ""is_graded"": true or false,
  ""grade_company"": ""PSA|BGS|CGC|SGC or null"",
  ""grade_value"": ""Numeric grade or null""
}

Return ONLY the JSON, no other text.";

        private readonly HttpClient _httpClient;
        private readonly ISettingsService _settingsService;
        private readonly IParallelCandidateProvider? _parallelProvider;
        private readonly ILogger<OpenRouterScannerService> _logger;

        public OpenRouterScannerService(
            HttpClient httpClient,
            ISettingsService settingsService,
            ILogger<OpenRouterScannerService> logger,
            IParallelCandidateProvider? parallelProvider = null)
        {
            _httpClient = httpClient;
            _settingsService = settingsService;
            // Optional so existing tests that mock the scanner don't have to wire
            // it up. When unset (or no candidates can be produced for a card) the
            // prompt + schema fall back to "free-form parallel name" behavior.
            _parallelProvider = parallelProvider;
            _logger = logger;
        }

        public async Task<ScanResult> ScanCardAsync(
            string imagePath,
            string? backImagePath = null,
            string model = OpenRouterModelDefaults.DefaultFreeModelId,
            XimilarScanMode ximilarMode = XimilarScanMode.Standard,
            ScanDepth scanDepth = ScanDepth.Standard,
            OcrHint? ocrHint = null,
            CancellationToken ct = default)
        {
            // Defensive: never put the UI sentinel "auto" on the wire. OpenRouter has
            // a real "Auto Router" provider that interprets it as routing to a
            // (typically premium) model — caused a real $2.50/4-card billing surprise
            // when a stale settings value leaked through. Substitute the free default
            // and log loudly so we can find any remaining leak callsites.
            var resolved = OpenRouterModelDefaults.ResolveModelId(model);
            if (resolved != model)
            {
                _logger.LogWarning(
                    "ScanCardAsync received UI sentinel model {Original}; substituting {Resolved}. " +
                    "This indicates a callsite that forgot to resolve before scanning.",
                    model, resolved);
                model = resolved;
            }

            var dataUrls = new List<string> { await EncodeImageToDataUrl(imagePath) };

            // Verified-fields hints unlock the slimmer Enhance prompt body that
            // skips the identification tips and asks the LLM to focus on the
            // visual-pattern fields. Quick scans always use the lot-labeling
            // prompt regardless of hint mode.
            string promptBody;
            if (scanDepth == ScanDepth.Quick)
                promptBody = QuickScanPromptBody;
            else if (ocrHint != null && ocrHint.VerifiedFieldNames.Count > 0)
                promptBody = EnhancePromptBody;
            else
                promptBody = ScanPromptBody;

            // Pull the parallel candidate list once. The same list goes into the
            // prompt preamble (as a "pick from this list" instruction) AND the
            // json_schema enum below — they have to match or the LLM will pick
            // a value that gets rejected by the schema.
            var parallelCandidates = ResolveParallelCandidates(ocrHint);
            var hintPreamble = ocrHint != null ? BuildOcrHintPreamble(ocrHint) : string.Empty;
            var candidatesPreamble = BuildParallelCandidatesPreamble(parallelCandidates);

            string prompt;
            if (!string.IsNullOrEmpty(backImagePath) && File.Exists(backImagePath))
            {
                dataUrls.Add(await EncodeImageToDataUrl(backImagePath));
                prompt = hintPreamble + candidatesPreamble + "You are given the FRONT and BACK images of the same sports card. The first image is the FRONT, the second is the BACK. Analyze BOTH images together to extract all identifying information. The back often contains the card number, set name, manufacturer, and serial number." + promptBody;
            }
            else
            {
                prompt = hintPreamble + candidatesPreamble + "Analyze this sports card image and extract all identifying information." + promptBody;
            }

            var settings = _settingsService.Load();
            var apiKey = settings.OpenRouterApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("OpenRouter API key is not configured. Go to Settings to enter your key.");

            var modelsToTry = GetFallbackChain(model);
            Exception? lastException = null;
            var failedModels = new List<string>();

            foreach (var currentModel in modelsToTry)
            {
                try
                {
                    _logger.LogInformation("Attempting scan with model {Model} (depth: {Depth})...", currentModel, scanDepth);

                    var rawContent = await TryScanModelAsync(dataUrls, prompt, currentModel, apiKey, parallelCandidates, ct);
                    var content = StripCodeBlocks(rawContent);

                    var scannedData = JsonSerializer.Deserialize<ScannedCardData>(content);
                    if (scannedData == null)
                        throw new JsonException("Deserialized to null");

                    _logger.LogInformation("Scan succeeded with model {Model}", currentModel);

                    var card = MapToCard(scannedData, imagePath);
                    if (!string.IsNullOrEmpty(backImagePath))
                        card.ImagePathBack = backImagePath;

                    // Drift guard: if the LLM disobeyed the "echo verbatim"
                    // instruction on a directory-confirmed field, restore the
                    // verified value and log the disagreement so we can spot
                    // misbehaving model picks. No-op when no verified hint
                    // was supplied (legacy soft-hint path).
                    if (ocrHint != null && ocrHint.VerifiedFieldNames.Count > 0)
                        ApplyVerifiedFieldOverrides(card, ocrHint);

                    return new ScanResult
                    {
                        Card = card,
                        VisualCues = MapToVisualCues(scannedData.VisualCues),
                        AllVisibleText = scannedData.AllVisibleText ?? new List<string>(),
                        Confidences = MapToConfidences(scannedData.Confidence)
                    };
                }
                catch (OpenRouterRateLimitException rlEx)
                {
                    lastException = rlEx;
                    if (rlEx.Scope == RateLimitScope.AccountPerDay)
                    {
                        _logger.LogError("Daily account rate limit hit on {Model}. Aborting chain.", currentModel);
                        throw; // propagate — don't walk the chain
                    }
                    if (rlEx.Scope == RateLimitScope.AccountPerMinute)
                    {
                        var waitMs = (rlEx.RetryAfterSeconds ?? 60) * 1000;
                        _logger.LogWarning("Per-minute rate limit on {Model}. Waiting {Wait}ms before walking chain.", currentModel, waitMs);
                        await Task.Delay(waitMs, ct);
                    }
                    else
                    {
                        _logger.LogWarning("Rate limit [{Scope}] on {Model}. Walking chain.", rlEx.Scope, currentModel);
                    }
                    failedModels.Add(currentModel);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // User clicked Cancel — stop immediately, don't walk the chain.
                    _logger.LogInformation("Scan cancelled by user on model {Model}.", currentModel);
                    throw;
                }
                catch (TaskCanceledException ex)
                {
                    // HttpClient timeout — model took too long to respond.
                    _logger.LogWarning("Model {Model} timed out (server too slow). Walking chain.", currentModel);
                    failedModels.Add(currentModel);
                    lastException = new TimeoutException(
                        $"Model {currentModel} timed out — the AI server took too long to respond.", ex);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("Model {Model} returned invalid JSON. Walking chain.", currentModel);
                    failedModels.Add(currentModel);
                    lastException = ex;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("No response content"))
                {
                    _logger.LogWarning("Model {Model} returned no content. Walking chain.", currentModel);
                    failedModels.Add(currentModel);
                    lastException = ex;
                }
                catch (HttpRequestException ex) when (IsWalkableHttpError(ex))
                {
                    _logger.LogWarning("Model {Model} failed ({Error}). Walking chain.", currentModel, ex.Message);
                    failedModels.Add(currentModel);
                    lastException = ex;
                }
            }

            _logger.LogError(lastException, "Model {Model} failed: {Error}", modelsToTry[0], lastException?.Message);
            throw new InvalidOperationException(
                lastException?.Message ?? $"Model {modelsToTry[0]} failed — please try again or check your network connection.",
                lastException);
        }

        /// <summary>
        /// Sends a single scan request with exponential backoff on 5xx errors
        /// (2s, 4s, 8s, 16s, 32s before giving up and letting the caller walk the chain).
        /// Also retries up to 2 times on connection-level timeouts (TaskCanceledException
        /// where the user did not cancel) — these are typically stale-connection resets or
        /// transient upstream drops, not real 5-minute waits.
        /// 429s are converted to <see cref="OpenRouterRateLimitException"/> and re-thrown.
        /// </summary>
        private async Task<string> TryScanModelAsync(
            List<string> dataUrls, string prompt, string modelId, string apiKey,
            IReadOnlyList<string> parallelCandidates, CancellationToken ct)
        {
            var backoffDelaysMs = new[] { 2000, 4000, 8000, 16000, 32000 };
            var attempt = 0;
            var timeoutAttempt = 0;

            while (true)
            {
                try
                {
                    return await SendSingleRequestAsync(dataUrls, prompt, modelId, apiKey, parallelCandidates, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw; // user cancelled — propagate immediately
                }
                catch (TaskCanceledException) when (timeoutAttempt < 2)
                {
                    // Transient connection timeout (not user-cancelled). Retry before giving up.
                    timeoutAttempt++;
                    _logger.LogWarning(
                        "Model {Model} connection dropped on attempt {N} (transient). Retrying in 10s.",
                        modelId, timeoutAttempt);
                    await Task.Delay(10_000, ct);
                }
                catch (OpenRouterRateLimitException)
                {
                    throw; // 429 — caller decides per-scope behavior
                }
                catch (HttpRequestException ex) when (Is5xxError(ex))
                {
                    if (attempt < backoffDelaysMs.Length)
                    {
                        _logger.LogWarning(
                            "Model {Model} returned 5xx on attempt {N}. Retrying in {Delay}ms.",
                            modelId, attempt + 1, backoffDelaysMs[attempt]);
                        await Task.Delay(backoffDelaysMs[attempt], ct);
                        attempt++;
                    }
                    else
                    {
                        _logger.LogWarning("Model {Model} exhausted 5xx retries. Walking chain.", modelId);
                        throw; // let caller walk chain
                    }
                }
            }
        }

        public async Task<string> SendCustomPromptAsync(string imagePath, string prompt, string? backImagePath = null, string model = OpenRouterModelDefaults.DefaultFreeModelId)
        {
            var dataUrls = new List<string> { await EncodeImageToDataUrl(imagePath) };

            if (!string.IsNullOrEmpty(backImagePath) && File.Exists(backImagePath))
                dataUrls.Add(await EncodeImageToDataUrl(backImagePath));

            return await SendVisionRequestAsync(dataUrls, prompt, model);
        }

        private async Task<string> EncodeImageToDataUrl(string imagePath)
        {
            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            var base64 = Convert.ToBase64String(imageBytes);
            var ext = Path.GetExtension(imagePath).ToLower().TrimStart('.');
            var mediaType = ext switch
            {
                "png" => "image/png",
                "webp" => "image/webp",
                _ => "image/jpeg"
            };
            return $"data:{mediaType};base64,{base64}";
        }

        private async Task<string> SendVisionRequestAsync(List<string> dataUrls, string prompt, string model)
        {
            var settings = _settingsService.Load();
            var apiKey = settings.OpenRouterApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("OpenRouter API key is not configured. Go to Settings to enter your key.");

            // Build fallback chain starting from the requested model
            var modelsToTry = GetFallbackChain(model);

            Exception? lastException = null;

            foreach (var currentModel in modelsToTry)
            {
                try
                {
                    _logger.LogDebug("Trying model {Model}", currentModel);
                    // Custom-prompt path (e.g. eBay title enricher) — null
                    // candidates skip the card json_schema since the response
                    // shape is whatever the prompt asked for, not ScannedCardData.
                    var result = await SendSingleRequestAsync(dataUrls, prompt, currentModel, apiKey, parallelCandidates: null, CancellationToken.None);
                    _logger.LogInformation("Scan succeeded with model {Model}", currentModel);
                    return result;
                }
                catch (HttpRequestException ex) when (IsRetryableHttpError(ex))
                {
                    _logger.LogWarning(ex, "Model {Model} failed with retryable error, trying next", currentModel);
                    lastException = ex;
                    continue;
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogWarning(ex, "Model {Model} timed out, trying next", currentModel);
                    lastException = ex;
                    continue;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("No response content"))
                {
                    _logger.LogWarning(ex, "Model {Model} returned no content, trying next", currentModel);
                    lastException = ex;
                    continue;
                }
            }

            _logger.LogError(lastException, "All {Count} models failed", modelsToTry.Count);
            throw new InvalidOperationException(
                $"All models failed. Last error: {lastException?.Message}", lastException);
        }

        private async Task<string> SendSingleRequestAsync(
            List<string> dataUrls, string prompt, string model, string apiKey,
            IReadOnlyList<string>? parallelCandidates, CancellationToken ct)
        {
            var contentParts = new List<OpenRouterContentPart>();
            foreach (var dataUrl in dataUrls)
            {
                contentParts.Add(new OpenRouterContentPart
                {
                    Type = "image_url",
                    ImageUrl = new OpenRouterImageUrl { Url = dataUrl }
                });
            }
            contentParts.Add(new OpenRouterContentPart { Type = "text", Text = prompt });

            var request = new OpenRouterRequest
            {
                Model = model,
                // Verified-fields Enhance pushes the response token count up because
                // the LLM is asked to echo every confirmed identity field verbatim
                // alongside visual_cues + confidence dict + all_visible_text. 4096
                // started truncating in real Enhance runs (graded cards with full
                // text). 8192 is comfortable headroom while still well under any
                // major-model context window.
                MaxTokens = 8192,
                Messages = new List<OpenRouterMessage>
                {
                    new() { Role = "user", Content = contentParts }
                },
                // Strict json_schema gates the response shape when this is a card
                // scan (parallelCandidates non-null). parallel_name's enum stops
                // models from inventing parallel names that aren't in our reference
                // data. OpenRouter forwards the schema to providers that support it;
                // ones that don't ignore response_format gracefully — the existing
                // JSON parser + StripCodeBlocks fallback covers them. Custom-prompt
                // callers (eBay title enricher) pass null to skip the schema.
                ResponseFormat = parallelCandidates != null
                    ? OpenRouterCardSchemaBuilder.BuildResponseFormat(parallelCandidates)
                    : null,
            };

            var jsonRequest = JsonSerializer.Serialize(request);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
            {
                Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            httpRequest.Headers.Add("X-Title", "FlipKit");

            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // Parse scope and Retry-After before throwing so the caller can act on them.
                response.Headers.TryGetValues("Retry-After", out var retryAfterValues);
                var retryAfterHeader = retryAfterValues?.FirstOrDefault();
                throw OpenRouterRateLimitParser.Parse(responseBody, retryAfterHeader, model);
            }

            if (!response.IsSuccessStatusCode)
                // Include integer status code so Is5xxError / IsWalkableHttpError can detect
                // the status by digit substring. Pre-Phase 5a only "404"/"NotFound" worked;
                // see AUDIT-2026-05 §5.9 for the original bug.
                throw new HttpRequestException($"OpenRouter API error ({(int)response.StatusCode} {response.StatusCode}): {responseBody}");

            var apiResponse = JsonSerializer.Deserialize<OpenRouterResponse>(responseBody);
            if (apiResponse?.Choices == null || apiResponse.Choices.Count == 0)
                throw new InvalidOperationException("No response content from AI model.");

            var choice = apiResponse.Choices[0];
            var content = choice?.Message?.Content;

            if (string.IsNullOrEmpty(content))
                throw new InvalidOperationException("No response content from AI model.");

            // Check if response was cut off due to token limit
            if (choice?.FinishReason == "length")
            {
                _logger.LogWarning("AI response was truncated due to token limit for model {Model}", model);
            }

            return content;
        }

        private static List<string> GetFallbackChain(string startModel)
        {
            // Always try exactly the requested model. Multi-model rotation is the
            // caller's responsibility (ScanWithAutoRotationAsync, BulkScan loop, Web
            // auto-rotation). Silently substituting a different model for an explicit
            // pick would confuse users (and potentially use a deprecated model).
            return new List<string> { startModel };
        }

        // 429 is now converted to OpenRouterRateLimitException in SendSingleRequestAsync,
        // so this helper only needs to handle 5xx (handled with backoff in TryScanModelAsync)
        // and 404 / model-not-found (walk chain immediately).
        private static bool Is5xxError(HttpRequestException ex)
        {
            var msg = ex.Message;
            return msg.Contains("500") || msg.Contains("502") || msg.Contains("503") || msg.Contains("504");
        }

        private static bool IsWalkableHttpError(HttpRequestException ex)
        {
            var msg = ex.Message;
            return msg.Contains("404") || msg.Contains("NotFound");
        }

        // Kept for SendVisionRequestAsync (custom prompts) which still uses the old pattern.
        private static bool IsRetryableHttpError(HttpRequestException ex)
        {
            var msg = ex.Message;
            return msg.Contains("404") || msg.Contains("NotFound")
                || msg.Contains("500") || msg.Contains("502")
                || msg.Contains("503") || msg.Contains("504");
        }

        private static string StripCodeBlocks(string content)
        {
            content = content.Trim();

            // Handle markdown code blocks
            if (content.Contains("```json"))
            {
                var parts = content.Split(new[] { "```json" }, StringSplitOptions.None);
                if (parts.Length > 1)
                {
                    var jsonPart = parts[1].Split(new[] { "```" }, StringSplitOptions.None)[0];
                    content = jsonPart.Trim();
                }
            }
            else if (content.Contains("```"))
            {
                var parts = content.Split(new[] { "```" }, StringSplitOptions.None);
                if (parts.Length > 1)
                {
                    var jsonPart = parts[1].Split(new[] { "```" }, StringSplitOptions.None)[0];
                    content = jsonPart.Trim();
                }
            }

            // Find JSON boundaries if there's extra text
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                content = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
            }

            return content.Trim();
        }

        internal static string BuildOcrHintPreamble(OcrHint hint)
        {
            return hint.VerifiedFieldNames.Count > 0
                ? BuildLockedHintPreamble(hint)
                : BuildSoftHintPreamble(hint);
        }

        /// <summary>
        /// Legacy preamble: every supplied field is a soft suggestion. The LLM
        /// is told to verify with its vision and may override any value. Used
        /// when the caller hasn't validated against the checklist directory.
        /// </summary>
        internal static string BuildSoftHintPreamble(OcrHint hint)
        {
            var sb = new StringBuilder("PRELIMINARY OCR DATA (treat as hints, not ground truth — verify with your vision):\n");
            if (!string.IsNullOrEmpty(hint.PlayerName)) sb.AppendLine($"- Player name from OCR: {hint.PlayerName}");
            if (hint.Year.HasValue) sb.AppendLine($"- Year from OCR: {hint.Year}");
            if (!string.IsNullOrEmpty(hint.CardNumber)) sb.AppendLine($"- Card number from OCR: {hint.CardNumber}");
            if (!string.IsNullOrEmpty(hint.Manufacturer)) sb.AppendLine($"- Manufacturer from OCR: {hint.Manufacturer}");
            if (!string.IsNullOrEmpty(hint.Brand)) sb.AppendLine($"- Brand from OCR: {hint.Brand}");
            if (!string.IsNullOrEmpty(hint.SetName)) sb.AppendLine($"- Set name from OCR: {hint.SetName}");
            if (hint.AllVisibleText.Count > 0)
                sb.AppendLine($"- Raw OCR text: {string.Join("; ", hint.AllVisibleText.Take(20))}");
            sb.AppendLine();
            return sb.ToString();
        }

        /// <summary>
        /// Verified-fields preamble: lists every confirmed field with its
        /// expected JSON key and value, then any unverified suggestions, then
        /// the raw OCR text. Used by the Enhance flow when the caller has
        /// already validated identity fields via the checklist directory.
        /// The accompanying EnhancePromptBody tells the LLM to echo confirmed
        /// fields verbatim and focus on visual-pattern fields the OCR can't see.
        /// </summary>
        internal static string BuildLockedHintPreamble(OcrHint hint)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CONFIRMED FIELDS — these have been validated against our checklist database.");
            sb.AppendLine("Echo these EXACT values verbatim in your JSON response. Do NOT re-derive or change them.");
            sb.AppendLine();

            void AppendIfVerified(string jsonKey, string? value)
            {
                if (!hint.VerifiedFieldNames.Contains(jsonKey)) return;
                if (string.IsNullOrEmpty(value)) return;
                sb.AppendLine($"- {jsonKey}: \"{value}\"");
            }
            void AppendIfVerifiedRaw(string jsonKey, string? rawValue)
            {
                if (!hint.VerifiedFieldNames.Contains(jsonKey)) return;
                if (string.IsNullOrEmpty(rawValue)) return;
                sb.AppendLine($"- {jsonKey}: {rawValue}");
            }

            AppendIfVerified("player_name", hint.PlayerName);
            AppendIfVerifiedRaw("year", hint.Year?.ToString());
            AppendIfVerified("card_number", hint.CardNumber);
            AppendIfVerified("manufacturer", hint.Manufacturer);
            AppendIfVerified("brand", hint.Brand);
            AppendIfVerified("set_name", hint.SetName);
            AppendIfVerified("team", hint.Team);
            AppendIfVerified("sport", hint.Sport);
            AppendIfVerified("parallel_name", hint.ParallelName);
            AppendIfVerified("serial_numbered", hint.SerialNumbered);
            AppendIfVerifiedRaw("is_rookie", hint.IsRookie?.ToString().ToLowerInvariant());
            AppendIfVerifiedRaw("is_auto", hint.IsAuto?.ToString().ToLowerInvariant());
            AppendIfVerifiedRaw("is_relic", hint.IsRelic?.ToString().ToLowerInvariant());
            AppendIfVerifiedRaw("is_graded", hint.IsGraded?.ToString().ToLowerInvariant());
            AppendIfVerified("grade_company", hint.GradeCompany);
            AppendIfVerified("grade_value", hint.GradeValue);

            // Suggestive (unverified) fields — same fields above but where the
            // value is populated yet NOT in VerifiedFieldNames. The LLM may
            // override these from the image; they're only there to anchor.
            var suggestiveLines = new List<string>();
            void Suggest(string jsonKey, string? value)
            {
                if (hint.VerifiedFieldNames.Contains(jsonKey)) return;
                if (string.IsNullOrEmpty(value)) return;
                suggestiveLines.Add($"- {jsonKey}: \"{value}\" (unverified)");
            }
            void SuggestRaw(string jsonKey, string? rawValue)
            {
                if (hint.VerifiedFieldNames.Contains(jsonKey)) return;
                if (string.IsNullOrEmpty(rawValue)) return;
                suggestiveLines.Add($"- {jsonKey}: {rawValue} (unverified)");
            }
            Suggest("player_name", hint.PlayerName);
            SuggestRaw("year", hint.Year?.ToString());
            Suggest("card_number", hint.CardNumber);
            Suggest("manufacturer", hint.Manufacturer);
            Suggest("brand", hint.Brand);
            Suggest("set_name", hint.SetName);
            Suggest("team", hint.Team);
            Suggest("sport", hint.Sport);
            Suggest("parallel_name", hint.ParallelName);
            Suggest("serial_numbered", hint.SerialNumbered);

            if (suggestiveLines.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("UNVERIFIED OCR HINTS — verify with your vision; you may override:");
                foreach (var line in suggestiveLines) sb.AppendLine(line);
            }

            if (hint.AllVisibleText.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("RAW OCR TEXT (front + back, in order):");
                foreach (var line in hint.AllVisibleText.Take(30))
                    sb.AppendLine($"  {line}");
            }

            sb.AppendLine();
            return sb.ToString();
        }

        /// <summary>
        /// Pulls the parallel candidate list from the registered provider, using
        /// whatever metadata the OcrHint exposes. Returns an empty list when no
        /// provider is registered (test scaffold) or no metadata is available —
        /// callers treat empty as "skip enum constraint, free-form parallel name."
        /// </summary>
        private IReadOnlyList<string> ResolveParallelCandidates(OcrHint? hint)
        {
            if (_parallelProvider == null) return Array.Empty<string>();
            return _parallelProvider.GetCandidates(
                manufacturer: hint?.Manufacturer,
                brand: hint?.Brand,
                year: hint?.Year,
                sport: hint?.Sport);
        }

        /// <summary>
        /// Renders the candidate list as a prompt block. The same list is
        /// attached to the request's response_format json_schema enum, so the
        /// prompt and the schema agree on what's acceptable. Returns empty
        /// string when there are no candidates — leaves the prompt unconstrained.
        /// </summary>
        internal static string BuildParallelCandidatesPreamble(IReadOnlyList<string> candidates)
        {
            if (candidates.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("KNOWN PARALLELS (set parallel_name to one of these names, or null if no visual match):");
            foreach (var name in candidates) sb.AppendLine($"  - {name}");
            sb.AppendLine();
            sb.AppendLine("If you see a clear shimmer / foil / refractor pattern but none of the listed names matches what you observe, set parallel_name to null and describe the pattern in condition_notes. Do NOT invent a parallel name that isn't on the list above.");
            sb.AppendLine();
            return sb.ToString();
        }

        private static Card MapToCard(ScannedCardData data, string imagePath)
        {
            var card = new Card
            {
                PlayerName = data.PlayerName ?? "Unknown Player",
                CardNumber = data.CardNumber,
                Year = data.Year,
                Manufacturer = data.Manufacturer,
                Brand = data.Brand,
                SetName = data.SetName,
                Team = data.Team,
                VariationType = data.VariationType ?? "Base",
                ParallelName = data.ParallelName,
                SerialNumbered = data.SerialNumbered,
                IsRookie = data.IsRookie ?? false,
                IsAuto = data.IsAuto ?? false,
                IsRelic = data.IsRelic ?? false,
                IsShortPrint = data.IsShortPrint ?? false,
                IsGraded = data.IsGraded ?? false,
                GradeCompany = data.GradeCompany,
                GradeValue = data.GradeValue,
                AutoGrade = data.AutoGrade,
                CertNumber = data.CertNumber,
                ImagePathFront = imagePath,
                Condition = "Near Mint",
                DataSource = CardDataSource.Ai,
            };

            if (Enum.TryParse<Sport>(data.Sport, true, out var sport))
                card.Sport = sport;

            if (card.Sport.HasValue)
            {
                card.WhatnotSubcategory = card.Sport.Value switch
                {
                    Sport.Football => "Football Cards",
                    Sport.Baseball => "Baseball Cards",
                    Sport.Basketball => "Basketball Cards",
                    _ => null
                };
            }

            if (!string.IsNullOrWhiteSpace(data.ConditionNotes))
                card.Notes = $"Condition notes: {data.ConditionNotes}";

            return card;
        }

        /// <summary>
        /// For each field name in <paramref name="hint"/>.VerifiedFieldNames,
        /// compares the value the LLM returned (now on <paramref name="card"/>)
        /// against the verified value on the hint. If they differ, restores
        /// the hint value and logs a warning so misbehaving model picks can
        /// be flagged. Field-name keys match the JSON schema keys the prompt
        /// uses (player_name, year, brand, …). String compares are case-
        /// insensitive; bool / int compare exact. Sport parses the hint
        /// string back to the enum before comparing.
        /// </summary>
        internal void ApplyVerifiedFieldOverrides(Card card, OcrHint hint)
        {
            void RestoreString(string field, Func<string?> read, Action<string?> write, string? hintValue)
            {
                if (!hint.VerifiedFieldNames.Contains(field)) return;
                if (string.IsNullOrEmpty(hintValue)) return;
                var current = read();
                if (string.Equals(current, hintValue, StringComparison.OrdinalIgnoreCase)) return;
                _logger.LogWarning(
                    "LLM drifted on confirmed field '{Field}': returned '{Llm}', restoring '{Verified}'",
                    field, current, hintValue);
                write(hintValue);
            }

            RestoreString("player_name",     () => card.PlayerName,     v => card.PlayerName = v ?? string.Empty, hint.PlayerName);
            RestoreString("card_number",     () => card.CardNumber,     v => card.CardNumber = v,                 hint.CardNumber);
            RestoreString("manufacturer",    () => card.Manufacturer,   v => card.Manufacturer = v,               hint.Manufacturer);
            RestoreString("brand",           () => card.Brand,          v => card.Brand = v,                      hint.Brand);
            RestoreString("set_name",        () => card.SetName,        v => card.SetName = v,                    hint.SetName);
            RestoreString("team",            () => card.Team,           v => card.Team = v,                       hint.Team);
            RestoreString("parallel_name",   () => card.ParallelName,   v => card.ParallelName = v,               hint.ParallelName);
            RestoreString("serial_numbered", () => card.SerialNumbered, v => card.SerialNumbered = v,             hint.SerialNumbered);
            RestoreString("grade_company",   () => card.GradeCompany,   v => card.GradeCompany = v,               hint.GradeCompany);
            RestoreString("grade_value",     () => card.GradeValue,     v => card.GradeValue = v,                 hint.GradeValue);

            // Year: int? compare
            if (hint.VerifiedFieldNames.Contains("year") && hint.Year.HasValue && card.Year != hint.Year)
            {
                _logger.LogWarning(
                    "LLM drifted on confirmed field 'year': returned '{Llm}', restoring '{Verified}'",
                    card.Year, hint.Year);
                card.Year = hint.Year;
            }

            // Sport: parse hint string to enum, compare to card.Sport (Sport?)
            if (hint.VerifiedFieldNames.Contains("sport") && !string.IsNullOrEmpty(hint.Sport)
                && Enum.TryParse<Sport>(hint.Sport, ignoreCase: true, out var hintSport)
                && card.Sport != hintSport)
            {
                _logger.LogWarning(
                    "LLM drifted on confirmed field 'sport': returned '{Llm}', restoring '{Verified}'",
                    card.Sport, hintSport);
                card.Sport = hintSport;
            }

            // Booleans: only flag drift when hint value is non-null
            void RestoreBool(string field, Func<bool> read, Action<bool> write, bool? hintValue)
            {
                if (!hint.VerifiedFieldNames.Contains(field)) return;
                if (!hintValue.HasValue) return;
                var current = read();
                if (current == hintValue.Value) return;
                _logger.LogWarning(
                    "LLM drifted on confirmed field '{Field}': returned '{Llm}', restoring '{Verified}'",
                    field, current, hintValue.Value);
                write(hintValue.Value);
            }
            RestoreBool("is_rookie", () => card.IsRookie, v => card.IsRookie = v, hint.IsRookie);
            RestoreBool("is_auto",   () => card.IsAuto,   v => card.IsAuto = v,   hint.IsAuto);
            RestoreBool("is_relic",  () => card.IsRelic,  v => card.IsRelic = v,  hint.IsRelic);
            RestoreBool("is_graded", () => card.IsGraded, v => card.IsGraded = v, hint.IsGraded);
        }

        private static VisualCues? MapToVisualCues(ScannedVisualCues? cues)
        {
            if (cues == null) return null;

            return new VisualCues
            {
                BorderColor = cues.BorderColor,
                CardFinish = cues.CardFinish,
                HasFoil = cues.HasFoil ?? false,
                HasRefractorPattern = cues.HasRefractorPattern ?? false,
                HasSerialNumber = cues.HasSerialNumber ?? false,
                SerialNumberLocation = cues.SerialNumberLocation,
                BackgroundPattern = cues.BackgroundPattern,
                TextColor = cues.TextColor,
                HasRookieLogo = cues.HasRookieLogo ?? false,
                HasAutoSticker = cues.HasAutoSticker ?? false,
                HasRelicSwatch = cues.HasRelicSwatch ?? false
            };
        }

        private static List<FieldConfidence> MapToConfidences(Dictionary<string, string>? confidence)
        {
            var result = new List<FieldConfidence>();
            if (confidence == null) return result;

            foreach (var kvp in confidence)
            {
                var level = kvp.Value?.ToLowerInvariant() switch
                {
                    "high" => VerificationConfidence.High,
                    "medium" => VerificationConfidence.Medium,
                    "low" => VerificationConfidence.Low,
                    _ => VerificationConfidence.Medium
                };

                result.Add(new FieldConfidence
                {
                    FieldName = kvp.Key,
                    Value = null,
                    Confidence = level,
                    Reason = $"AI confidence: {kvp.Value}"
                });
            }

            return result;
        }
    }
}
