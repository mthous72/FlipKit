using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services.ApiModels;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services
{
    public class XimilarService : IXimilarService
    {
        // Ximilar Collectibles Recognition API endpoints
        private const string SportCardApiUrl = "https://api.ximilar.com/collectibles/v2/sport_id";
        private const string TcgApiUrl = "https://api.ximilar.com/collectibles/v2/tcg_id";
        private const string SlabApiUrl = "https://api.ximilar.com/collectibles/v2/slab_id";

        private readonly HttpClient _httpClient;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<XimilarService> _logger;

        public XimilarService(HttpClient httpClient, ISettingsService settingsService, ILogger<XimilarService> logger)
        {
            _httpClient = httpClient;
            _settingsService = settingsService;
            _logger = logger;
        }

        public bool IsConfigured
        {
            get
            {
                var settings = _settingsService.Load();
                return !string.IsNullOrWhiteSpace(settings.XimilarApiKey);
            }
        }

        public async Task<XimilarResult?> RecognizeCardAsync(string imagePath, bool useMagicAi = false)
        {
            var settings = _settingsService.Load();
            var apiKey = settings.XimilarApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogDebug("Ximilar API key not configured, skipping Ximilar recognition");
                return null;
            }

            try
            {
                _logger.LogInformation("Attempting Ximilar recognition for {ImagePath} (magic_ai: {MagicAi})",
                    imagePath, useMagicAi);

                var imageBytes = await File.ReadAllBytesAsync(imagePath);
                var base64Raw = Convert.ToBase64String(imageBytes);

                // Ximilar requires data URI format: data:image/jpeg;base64,{base64data}
                var extension = Path.GetExtension(imagePath).ToLowerInvariant();
                var mimeType = extension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => "image/jpeg" // Default to JPEG
                };
                var base64 = $"data:{mimeType};base64,{base64Raw}";

                var request = new XimilarRequest
                {
                    Records = new() { new XimilarRecord { Base64 = base64 } },
                    MagicAi = useMagicAi
                };

                var jsonRequest = JsonSerializer.Serialize(request);
                // Use sport_id endpoint for sports cards (most common use case)
                // TODO: Could add logic to try tcg_id for Pokemon/Magic cards
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, SportCardApiUrl)
                {
                    Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")
                };
                httpRequest.Headers.Add("Authorization", $"Token {apiKey}");

                var response = await _httpClient.SendAsync(httpRequest);
                var responseBody = await response.Content.ReadAsStringAsync();

                _logger.LogDebug("Ximilar response: {Response}", responseBody);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Ximilar API error ({StatusCode}): {Response}", response.StatusCode, responseBody);
                    return new XimilarResult
                    {
                        Success = false,
                        ErrorMessage = $"Ximilar API error: {response.StatusCode}"
                    };
                }

                var ximilarResponse = JsonSerializer.Deserialize<XimilarResponse>(responseBody);

                if (ximilarResponse?.Records == null || ximilarResponse.Records.Count == 0)
                {
                    _logger.LogInformation("Ximilar returned no records");
                    return new XimilarResult
                    {
                        Success = false,
                        ErrorMessage = "No records returned from Ximilar"
                    };
                }

                var record = ximilarResponse.Records[0];

                // Check for object detection with identification (best_match)
                if (record.Objects != null && record.Objects.Count > 0)
                {
                    var obj = record.Objects[0];
                    var bestMatch = obj.Identification?.BestMatch;

                    if (bestMatch != null)
                    {
                        var card = MapBestMatchToCard(bestMatch, obj.Tags, imagePath);
                        var confidence = obj.Probability;

                        _logger.LogInformation("Ximilar found match: {Player} {Year} {SetName} (confidence: {Confidence})",
                            card.PlayerName, card.Year, card.Brand, confidence);

                        return new XimilarResult
                        {
                            Success = true,
                            Card = card,
                            Confidence = confidence,
                            RawResponse = responseBody
                        };
                    }

                    // Fallback: Try to extract info from tags if no identification
                    if (obj.Tags != null)
                    {
                        var card = MapTagsToCard(obj.Tags, imagePath);
                        var confidence = obj.Probability;

                        _logger.LogInformation("Ximilar detected card via tags (confidence: {Confidence})", confidence);

                        return new XimilarResult
                        {
                            Success = true,
                            Card = card,
                            Confidence = confidence,
                            RawResponse = responseBody
                        };
                    }
                }

                _logger.LogInformation("Ximilar did not find a confident match");
                return new XimilarResult
                {
                    Success = false,
                    ErrorMessage = "No confident match found",
                    RawResponse = responseBody
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ximilar recognition failed");
                return new XimilarResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<bool> TestConnectionAsync(string apiKey)
        {
            try
            {
                // Use a minimal request to the actual API endpoint to test authentication
                // We send an empty records array which should return quickly with auth status
                var request = new XimilarRequest
                {
                    Records = new() // Empty records list
                };

                var jsonRequest = JsonSerializer.Serialize(request);
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, SportCardApiUrl)
                {
                    Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")
                };
                httpRequest.Headers.Add("Authorization", $"Token {apiKey}");

                var response = await _httpClient.SendAsync(httpRequest);
                var responseBody = await response.Content.ReadAsStringAsync();

                _logger.LogDebug("Ximilar test connection response ({StatusCode}): {Response}",
                    response.StatusCode, responseBody);

                // 200 = success, 400 = bad request (but auth worked), 401/403 = auth failed
                return response.StatusCode != System.Net.HttpStatusCode.Unauthorized &&
                       response.StatusCode != System.Net.HttpStatusCode.Forbidden;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ximilar connection test failed");
                return false;
            }
        }

        private static Card MapBestMatchToCard(XimilarBestMatch best, XimilarTags? tags, string imagePath)
        {
            // Determine autograph from tags
            var isAuto = tags?.Autograph?.Count > 0 &&
                         tags.Autograph.Any(t => t.Name?.ToLower() == "autograph" && t.Probability > 0.5);

            // Determine if it's a rookie card from CardType
            var isRookie = best.CardType?.ToLower().Contains("rookie") == true;

            // Determine variation type
            var variationType = DetermineVariationType(tags, isAuto);

            var card = new Card
            {
                PlayerName = best.Name ?? "Unknown Player",
                CardNumber = best.CardNumber,
                Manufacturer = best.Company,
                Brand = best.SetName,
                IsRookie = isRookie,
                IsAuto = isAuto,
                ImagePathFront = imagePath,
                Condition = "Near Mint",
                VariationType = variationType
            };

            // Parse year
            if (int.TryParse(best.Year, out var year))
                card.Year = year;

            // Parse sport from Subcategory (MMA, Football, Baseball, etc.)
            var sportStr = best.Subcategory;
            if (!string.IsNullOrEmpty(sportStr))
            {
                // Map Ximilar sport names to our Sport enum
                Sport? sport = sportStr.ToLower() switch
                {
                    "football" => Sport.Football,
                    "baseball" => Sport.Baseball,
                    "basketball" => Sport.Basketball,
                    "hockey" => Sport.Hockey,
                    "soccer" => Sport.Soccer,
                    "mma" or "ufc" => Sport.MMA,
                    "wrestling" => Sport.Wrestling,
                    "golf" => Sport.Golf,
                    "tennis" => Sport.Tennis,
                    "racing" => Sport.Racing,
                    _ => null
                };

                if (sport != null)
                {
                    card.Sport = sport.Value;
                    card.WhatnotSubcategory = sport.Value switch
                    {
                        Sport.Football => "Football Cards",
                        Sport.Baseball => "Baseball Cards",
                        Sport.Basketball => "Basketball Cards",
                        Sport.Hockey => "Hockey Cards",
                        Sport.Soccer => "Soccer Cards",
                        Sport.MMA => "MMA Cards",
                        _ => null
                    };
                }
            }

            // Store eBay URL in notes if available
            if (!string.IsNullOrEmpty(best.Links?.Ebay))
                card.Notes = $"Ximilar eBay ref: {best.Links.Ebay}";

            // Add subset info if available (e.g., "UFC" for MMA cards)
            if (!string.IsNullOrEmpty(best.SubSet) && string.IsNullOrEmpty(card.Notes))
                card.Notes = $"Subset: {best.SubSet}";

            return card;
        }

        private static Card MapTagsToCard(XimilarTags tags, string imagePath)
        {
            var isAuto = tags.Autograph?.Any(t => t.Name?.ToLower() == "autograph" && t.Probability > 0.5) == true;
            var variationType = DetermineVariationType(tags, isAuto);

            var card = new Card
            {
                PlayerName = "Unknown Player",
                ImagePathFront = imagePath,
                Condition = "Near Mint",
                VariationType = variationType,
                IsAuto = isAuto
            };

            // Try to extract sport from Subcategory tags
            if (tags.Subcategory != null)
            {
                foreach (var subcatTag in tags.Subcategory)
                {
                    if (subcatTag.Name != null && subcatTag.Probability > 0.5)
                    {
                        var name = subcatTag.Name.ToLower();
                        Sport? sport = name switch
                        {
                            "football" => Sport.Football,
                            "baseball" => Sport.Baseball,
                            "basketball" => Sport.Basketball,
                            "hockey" => Sport.Hockey,
                            "soccer" => Sport.Soccer,
                            "mma" or "ufc" => Sport.MMA,
                            _ => null
                        };

                        if (sport != null)
                        {
                            card.Sport = sport.Value;
                            break;
                        }
                    }
                }
            }

            return card;
        }

        private static string DetermineVariationType(XimilarTags? tags, bool isAuto)
        {
            if (tags == null)
                return isAuto ? "Auto" : "Base";

            // Check for Foil/Holo
            var isFoilHolo = tags.FoilHolo?.Any(t =>
                (t.Name?.ToLower().Contains("foil") == true || t.Name?.ToLower().Contains("holo") == true) &&
                t.Probability > 0.5) == true;

            // Check for graded
            var isGraded = tags.Graded?.Any(t =>
                t.Name?.ToLower() == "graded" && t.Probability > 0.5) == true;

            if (isAuto && isFoilHolo)
                return "Foil Auto";
            if (isAuto)
                return "Auto";
            if (isFoilHolo)
                return "Refractor";
            if (isGraded)
                return "Graded";

            return "Base";
        }
    }
}
