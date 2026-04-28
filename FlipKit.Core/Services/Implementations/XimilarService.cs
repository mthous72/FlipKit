using System;
using System.IO;
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

        public async Task<XimilarResult?> RecognizeCardAsync(string imagePath)
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
                _logger.LogInformation("Attempting Ximilar recognition for {ImagePath}", imagePath);

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
                    Records = new() { new XimilarRecord { Base64 = base64 } }
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

                // Check for best match (visual similarity search result)
                if (record.Best != null && record.Best.Score > 0.7)
                {
                    var card = MapBestMatchToCard(record.Best, imagePath);
                    _logger.LogInformation("Ximilar found match: {Player} {Year} {Brand} (score: {Score})",
                        card.PlayerName, card.Year, card.Brand, record.Best.Score);

                    return new XimilarResult
                    {
                        Success = true,
                        Card = card,
                        Confidence = record.Best.Score,
                        RawResponse = responseBody
                    };
                }

                // Check for object detection with labels
                if (record.Objects != null && record.Objects.Count > 0)
                {
                    var obj = record.Objects[0];
                    if (obj.Labels != null && obj.Labels.Count > 0)
                    {
                        var card = MapLabelsToCard(obj, imagePath);
                        var confidence = obj.Probability;

                        _logger.LogInformation("Ximilar detected card via labels (confidence: {Confidence})", confidence);

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

        private static Card MapBestMatchToCard(XimilarBestMatch best, string imagePath)
        {
            var card = new Card
            {
                PlayerName = best.PlayerName ?? "Unknown Player",
                CardNumber = best.CardNumber,
                Team = best.Team,
                Manufacturer = best.Manufacturer,
                Brand = best.Brand,
                ParallelName = best.Parallel ?? best.Variation,
                SerialNumbered = best.SerialNumbered,
                IsRookie = best.IsRookie ?? false,
                IsAuto = best.IsAuto ?? false,
                IsRelic = best.IsRelic ?? false,
                ImagePathFront = imagePath,
                Condition = "Near Mint",
                VariationType = DetermineVariationType(best)
            };

            // Parse year
            if (int.TryParse(best.Year, out var year))
                card.Year = year;

            // Parse sport
            if (!string.IsNullOrEmpty(best.Sport) && Enum.TryParse<Sport>(best.Sport, true, out var sport))
            {
                card.Sport = sport;
                card.WhatnotSubcategory = sport switch
                {
                    Sport.Football => "Football Cards",
                    Sport.Baseball => "Baseball Cards",
                    Sport.Basketball => "Basketball Cards",
                    _ => null
                };
            }

            // Store eBay URL in notes if available
            if (!string.IsNullOrEmpty(best.EbayUrl))
                card.Notes = $"Ximilar eBay ref: {best.EbayUrl}";

            return card;
        }

        private static Card MapLabelsToCard(XimilarObject obj, string imagePath)
        {
            var card = new Card
            {
                PlayerName = "Unknown Player",
                ImagePathFront = imagePath,
                Condition = "Near Mint",
                VariationType = "Base"
            };

            if (obj.Labels != null)
            {
                foreach (var label in obj.Labels)
                {
                    // Ximilar labels may include category info like "Trading Card", "Sports Card", etc.
                    // The specific field mapping depends on how Ximilar structures the response
                    if (label.Name != null)
                    {
                        // Try to extract useful info from label names
                        var name = label.Name.ToLower();
                        if (name.Contains("football"))
                            card.Sport = Sport.Football;
                        else if (name.Contains("baseball"))
                            card.Sport = Sport.Baseball;
                        else if (name.Contains("basketball"))
                            card.Sport = Sport.Basketball;
                    }
                }
            }

            return card;
        }

        private static string DetermineVariationType(XimilarBestMatch best)
        {
            if (best.IsAuto == true && best.IsRelic == true)
                return "Patch Auto";
            if (best.IsAuto == true)
                return "Auto";
            if (best.IsRelic == true)
                return "Relic";
            if (!string.IsNullOrEmpty(best.Parallel))
                return "Parallel";
            if (!string.IsNullOrEmpty(best.Variation))
                return "Insert";

            return "Base";
        }
    }
}
