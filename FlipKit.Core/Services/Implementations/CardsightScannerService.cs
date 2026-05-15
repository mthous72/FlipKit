using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services.ApiModels;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services.Implementations
{
    public class CardsightScannerService
    {
        private const string ApiBase = "https://api.cardsight.ai";
        private const string IdentifyEndpoint = "/v1/identify/card";

        public const string ProviderId = "cardsight";

        private readonly HttpClient _httpClient;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<CardsightScannerService> _logger;

        public CardsightScannerService(
            HttpClient httpClient,
            ISettingsService settingsService,
            ILogger<CardsightScannerService> logger)
        {
            _httpClient = httpClient;
            _settingsService = settingsService;
            _logger = logger;
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_settingsService.Load().CardsightApiKey);

        public async Task<ScanResult> ScanCardAsync(
            string frontImagePath,
            string? backImagePath = null,
            CancellationToken ct = default)
        {
            var settings = _settingsService.Load();
            var apiKey = settings.CardsightApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new CardsightException(CardsightFailureReason.NotConfigured, "CardSight API key is not configured.");

            if (!File.Exists(frontImagePath))
                throw new CardsightException(CardsightFailureReason.BadRequest, $"Front image not found: {frontImagePath}");

            var response = await SendIdentifyRequestAsync(frontImagePath, apiKey, ct).ConfigureAwait(false);

            // Pick the first detection. Single-card scans almost always return one.
            var detection = response.Detections?.FirstOrDefault();
            if (detection is null)
                throw new CardsightException(CardsightFailureReason.NoMatch, "CardSight returned no detections.");

            var card = detection.Card;
            // card.id is only populated for *exact* card matches. Without it we have
            // only set-level info, which isn't actionable for inventory — treat as miss.
            if (card is null || string.IsNullOrWhiteSpace(card.Id))
                throw new CardsightException(CardsightFailureReason.NoMatch, "CardSight did not return an exact card match.");

            var detectedTier = ParseConfidence(detection.Confidence);
            if (detectedTier < settings.MinCardsightConfidence)
            {
                throw new CardsightException(
                    CardsightFailureReason.LowConfidence,
                    $"CardSight confidence {detection.Confidence ?? "(none)"} below threshold {settings.MinCardsightConfidence}.");
            }

            return BuildScanResult(card, detection, backImagePath);
        }

        private async Task<CardsightIdentifyResponse> SendIdentifyRequestAsync(
            string imagePath,
            string apiKey,
            CancellationToken ct)
        {
            using var multipart = new MultipartFormDataContent();
            // The HttpRequestMessage takes ownership of multipart; the underlying FileStream
            // is disposed when multipart is disposed.
            var fileStream = File.OpenRead(imagePath);
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(GuessMimeType(imagePath));
            multipart.Add(fileContent, "image", Path.GetFileName(imagePath));

            using var request = new HttpRequestMessage(HttpMethod.Post, ApiBase + IdentifyEndpoint)
            {
                Content = multipart
            };
            request.Headers.Add("X-API-Key", apiKey);

            HttpResponseMessage httpResponse;
            try
            {
                httpResponse = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                throw new CardsightException(CardsightFailureReason.Transient, $"CardSight HTTP error: {ex.Message}", inner: ex);
            }

            var body = await httpResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                ThrowForStatus(httpResponse.StatusCode, body);
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<CardsightIdentifyResponse>(body);
                if (parsed is null)
                    throw new CardsightException(CardsightFailureReason.Unknown, "CardSight returned empty response body.", responseBody: body);
                return parsed;
            }
            catch (JsonException ex)
            {
                throw new CardsightException(CardsightFailureReason.Unknown, "Failed to parse CardSight response.", responseBody: body, inner: ex);
            }
        }

        private static void ThrowForStatus(HttpStatusCode status, string body)
        {
            var reason = status switch
            {
                HttpStatusCode.Unauthorized => CardsightFailureReason.InvalidKey,
                HttpStatusCode.PaymentRequired => CardsightFailureReason.QuotaExceeded,
                HttpStatusCode.TooManyRequests => CardsightFailureReason.RateLimited,
                HttpStatusCode.BadRequest => CardsightFailureReason.BadRequest,
                HttpStatusCode.NotFound => CardsightFailureReason.NoMatch,
                HttpStatusCode.RequestTimeout => CardsightFailureReason.Transient,
                HttpStatusCode.InternalServerError => CardsightFailureReason.Transient,
                HttpStatusCode.BadGateway => CardsightFailureReason.Transient,
                HttpStatusCode.ServiceUnavailable => CardsightFailureReason.Transient,
                HttpStatusCode.GatewayTimeout => CardsightFailureReason.Transient,
                _ => CardsightFailureReason.Unknown
            };
            throw new CardsightException(reason, $"CardSight returned {(int)status} {status}.", (int)status, body);
        }

        private static ScanResult BuildScanResult(CardsightCardDetails card, CardsightDetection detection, string? backImagePath)
        {
            var result = new ScanResult
            {
                UsedModelId = ProviderId,
                Card = new Card
                {
                    PlayerName = card.Name ?? string.Empty,
                    CardNumber = card.Number,
                    Year = TryParseYear(card.Year),
                    Manufacturer = card.Manufacturer,
                    Brand = card.ReleaseName,
                    SetName = card.SetName,
                    ParallelName = card.Parallel?.Name,
                    SerialNumbered = card.Parallel?.NumberedTo is int p ? $"/{p}" : (card.NumberedTo is int n ? $"/{n}" : null),
                    VariationType = string.IsNullOrWhiteSpace(card.Parallel?.Name) ? "Base" : "Parallel",
                    DataSource = CardDataSource.Ai,
                    AiModelUsed = ProviderId,
                }
            };

            ApplyAttributes(result.Card, card.Attributes);
            ApplyGrading(result.Card, detection.Grading);

            if (!string.IsNullOrEmpty(backImagePath))
                result.Card.ImagePathBack = backImagePath;

            result.Confidences.Add(new FieldConfidence
            {
                FieldName = "cardsight_confidence",
                Value = detection.Confidence,
                Confidence = MapConfidenceForScoreboard(detection.Confidence),
                Reason = $"CardSight {detection.Confidence ?? "?"} match"
            });

            return result;
        }

        private static CardsightConfidenceTier ParseConfidence(string? confidence) => confidence switch
        {
            "High" => CardsightConfidenceTier.High,
            "Medium" => CardsightConfidenceTier.Medium,
            "Low" => CardsightConfidenceTier.Low,
            _ => CardsightConfidenceTier.Low
        };

        private static VerificationConfidence MapConfidenceForScoreboard(string? confidence) => confidence switch
        {
            "High" => VerificationConfidence.High,
            "Medium" => VerificationConfidence.Medium,
            _ => VerificationConfidence.Low
        };

        private static int? TryParseYear(string? year) =>
            int.TryParse(year, out var y) ? y : (int?)null;

        private static void ApplyAttributes(Card card, List<string>? attributes)
        {
            if (attributes is null) return;
            foreach (var attr in attributes)
            {
                if (string.IsNullOrWhiteSpace(attr)) continue;
                var lower = attr.ToLowerInvariant();
                if (lower.Contains("rookie")) card.IsRookie = true;
                if (lower.Contains("auto")) card.IsAuto = true;
                if (lower.Contains("relic") || lower.Contains("memorabilia") || lower.Contains("patch")) card.IsRelic = true;
                if (lower.Contains("short print") || lower == "sp") card.IsShortPrint = true;
                if (lower.Contains("super short print") || lower == "ssp") card.IsSSP = true;
            }
        }

        private static void ApplyGrading(Card card, CardsightGradingDetail? grading)
        {
            if (grading?.Company?.Name is null) return;
            card.IsGraded = true;
            card.GradeCompany = grading.Company.Name;
            card.GradeValue = grading.Grade?.Value;
            card.AutoGrade = grading.AutoGrade?.Value;
            if (!string.IsNullOrWhiteSpace(grading.Grade?.Condition))
                card.Condition = grading.Grade.Condition!;
        }

        private static string GuessMimeType(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".heic" or ".heif" => "image/heic",
                _ => "application/octet-stream"
            };
        }
    }
}
