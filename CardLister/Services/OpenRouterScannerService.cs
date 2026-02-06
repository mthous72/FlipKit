using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CardLister.Models;
using CardLister.Models.Enums;
using CardLister.Services.ApiModels;
using Microsoft.Extensions.Logging;

namespace CardLister.Services
{
    public class OpenRouterScannerService : IScannerService
    {
        private const string ApiUrl = "https://openrouter.ai/api/v1/chat/completions";

        public static readonly string[] FreeVisionModels = new[]
        {
            "nvidia/nemotron-nano-12b-v2-vl:free",
            "qwen/qwen2.5-vl-72b-instruct:free",
            "qwen/qwen2.5-vl-32b-instruct:free",
            "meta-llama/llama-4-maverick:free",
            "meta-llama/llama-4-scout:free",
            "google/gemma-3-27b-it:free",
            "mistralai/mistral-small-3.1-24b-instruct:free",
            "moonshotai/kimi-vl-a3b-thinking:free",
            "meta-llama/llama-3.2-11b-vision-instruct:free",
            "google/gemma-3-12b-it:free",
            "google/gemma-3-4b-it:free"
        };

        private const string ScanPromptBody = @"
Return ONLY a JSON object with these exact fields (use null for unknown values):

{
  ""player_name"": ""Full player name"",
  ""card_number"": ""Card number without # symbol"",
  ""year"": 2024,
  ""sport"": ""Football|Baseball|Basketball"",
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

        private readonly HttpClient _httpClient;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<OpenRouterScannerService> _logger;

        public OpenRouterScannerService(HttpClient httpClient, ISettingsService settingsService, ILogger<OpenRouterScannerService> logger)
        {
            _httpClient = httpClient;
            _settingsService = settingsService;
            _logger = logger;
        }

        public async Task<ScanResult> ScanCardAsync(string imagePath, string? backImagePath = null, string model = "nvidia/nemotron-nano-12b-v2-vl:free")
        {
            var dataUrls = new List<string> { await EncodeImageToDataUrl(imagePath) };

            string prompt;
            if (!string.IsNullOrEmpty(backImagePath) && File.Exists(backImagePath))
            {
                dataUrls.Add(await EncodeImageToDataUrl(backImagePath));
                prompt = "You are given the FRONT and BACK images of the same sports card. The first image is the FRONT, the second is the BACK. Analyze BOTH images together to extract all identifying information. The back often contains the card number, set name, manufacturer, and serial number." + ScanPromptBody;
            }
            else
            {
                prompt = "Analyze this sports card image and extract all identifying information." + ScanPromptBody;
            }

            var content = await SendVisionRequestAsync(dataUrls, prompt, model);
            content = StripCodeBlocks(content);

            var scannedData = JsonSerializer.Deserialize<ScannedCardData>(content)
                ?? throw new InvalidOperationException("Failed to parse AI response as card data.");

            var card = MapToCard(scannedData, imagePath);
            if (!string.IsNullOrEmpty(backImagePath))
                card.ImagePathBack = backImagePath;

            var visualCues = MapToVisualCues(scannedData.VisualCues);
            var confidences = MapToConfidences(scannedData.Confidence);

            return new ScanResult
            {
                Card = card,
                VisualCues = visualCues,
                AllVisibleText = scannedData.AllVisibleText ?? new List<string>(),
                Confidences = confidences
            };
        }

        public async Task<string> SendCustomPromptAsync(string imagePath, string prompt, string? backImagePath = null, string model = "nvidia/nemotron-nano-12b-v2-vl:free")
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
                    var result = await SendSingleRequestAsync(dataUrls, prompt, currentModel, apiKey);
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

        private async Task<string> SendSingleRequestAsync(List<string> dataUrls, string prompt, string model, string apiKey)
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
                MaxTokens = 2048,
                Messages = new List<OpenRouterMessage>
                {
                    new() { Role = "user", Content = contentParts }
                }
            };

            var jsonRequest = JsonSerializer.Serialize(request);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
            {
                Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            httpRequest.Headers.Add("X-Title", "CardLister");

            var response = await _httpClient.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"OpenRouter API error ({response.StatusCode}): {responseBody}");

            var apiResponse = JsonSerializer.Deserialize<OpenRouterResponse>(responseBody);
            return apiResponse?.Choices[0]?.Message?.Content
                ?? throw new InvalidOperationException("No response content from AI model.");
        }

        private static List<string> GetFallbackChain(string startModel)
        {
            var index = Array.IndexOf(FreeVisionModels, startModel);
            if (index >= 0)
            {
                // Start from the selected model and continue through the rest
                return FreeVisionModels.Skip(index).ToList();
            }

            // Model not in the free list (e.g. a paid model) — try it alone, then fall back to all free models
            var chain = new List<string> { startModel };
            chain.AddRange(FreeVisionModels);
            return chain;
        }

        private static bool IsRetryableHttpError(HttpRequestException ex)
        {
            var msg = ex.Message;
            // Rate limit (429) or server errors (5xx)
            return msg.Contains("429") || msg.Contains("500") || msg.Contains("502")
                || msg.Contains("503") || msg.Contains("504");
        }

        private static string StripCodeBlocks(string content)
        {
            content = content.Trim();
            if (content.Contains("```json"))
                content = content.Split("```json")[1].Split("```")[0];
            else if (content.Contains("```"))
                content = content.Split("```")[1].Split("```")[0];
            return content.Trim();
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
                Condition = "Near Mint"
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
