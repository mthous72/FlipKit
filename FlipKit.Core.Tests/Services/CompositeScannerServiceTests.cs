using System.Net;
using System.Text.Json;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations;
using FlipKit.Core.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Core.Tests.Services;

/// <summary>
/// Composite scanner tests use real <see cref="OpenRouterScannerService"/> and
/// real <see cref="CardsightScannerService"/> instances backed by stub HTTP handlers.
/// CardSight runs first; OpenRouter is the fallback for miss / low-confidence / error.
/// </summary>
public class CompositeScannerServiceTests
{
    private static ISettingsService SettingsWith(
        string? openRouterKey = "or-key",
        string? cardsightKey = "cs-key",
        CardsightConfidenceTier minTier = CardsightConfidenceTier.Medium) =>
        Substitute.For<ISettingsService>().Tap(s => s.Load().Returns(new AppSettings
        {
            OpenRouterApiKey = openRouterKey,
            CardsightApiKey = cardsightKey,
            MinCardsightConfidence = minTier,
        }));

    private const string MinimalScannedCardJson = @"{
        ""player_name"": ""LLM Fallback Player"",
        ""year"": 2026,
        ""sport"": ""Baseball"",
        ""confidence"": { ""player_name"": ""high"" }
    }";

    private static string OpenRouterBodyWith(string innerContent)
    {
        var inner = JsonSerializer.Serialize(innerContent);
        return $@"{{""choices"":[{{""message"":{{""content"":{inner}}},""finish_reason"":""stop""}}]}}";
    }

    private static string CardsightBody(string playerName, string confidence, string cardId = "00000000-0000-0000-0000-000000000001") => $@"{{
        ""success"": true,
        ""requestId"": ""req-1"",
        ""detections"": [{{
            ""confidence"": ""{confidence}"",
            ""card"": {{
                ""id"": ""{cardId}"",
                ""name"": ""{playerName}"",
                ""year"": ""2025"",
                ""manufacturer"": ""Topps"",
                ""releaseName"": ""Topps Chrome"",
                ""setName"": ""Base Set"",
                ""number"": ""RC-1""
            }}
        }}]
    }}";

    private const string CardsightEmptyBody = @"{""success"": true, ""requestId"": ""req-empty"", ""detections"": []}";

    private static (CompositeScannerService svc, StubHttpMessageHandler csHandler, StubHttpMessageHandler orHandler) Build(
        Func<HttpRequestMessage, HttpResponseMessage> csResponder,
        Func<HttpRequestMessage, HttpResponseMessage> orResponder,
        ISettingsService? settings = null)
    {
        var settingsSvc = settings ?? SettingsWith();
        var csHandler = new StubHttpMessageHandler(csResponder);
        var orHandler = new StubHttpMessageHandler(orResponder);

        var cardsight = new CardsightScannerService(new HttpClient(csHandler), settingsSvc, NullLogger<CardsightScannerService>.Instance);
        var openRouter = new OpenRouterScannerService(new HttpClient(orHandler), settingsSvc, NullLogger<OpenRouterScannerService>.Instance);
        var composite = new CompositeScannerService(cardsight, openRouter, NullLogger<CompositeScannerService>.Instance);
        return (composite, csHandler, orHandler);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body) };

    // === CardSight not configured ===

    [Fact]
    public async Task Should_SkipCardsightAndUseOpenRouter_When_CardsightApiKeyMissing()
    {
        using var image = new TempImageFile();
        var (svc, csH, orH) = Build(
            csResponder: _ => throw new InvalidOperationException("CardSight should not be called"),
            orResponder: _ => Json(OpenRouterBodyWith(MinimalScannedCardJson)),
            settings: SettingsWith(cardsightKey: null));

        var result = await svc.ScanCardAsync(image.Path);

        Assert.Empty(csH.Requests);
        Assert.Single(orH.Requests);
        Assert.Equal("LLM Fallback Player", result.Card.PlayerName);
    }

    // === CardSight high-confidence match wins ===

    [Fact]
    public async Task Should_UseCardsightResult_When_ConfidenceMeetsThreshold()
    {
        using var image = new TempImageFile();
        var (svc, csH, orH) = Build(
            csResponder: _ => Json(CardsightBody("CardSight Match", "High")),
            orResponder: _ => throw new InvalidOperationException("OpenRouter should not be called"));

        var result = await svc.ScanCardAsync(image.Path);

        Assert.Single(csH.Requests);
        Assert.Empty(orH.Requests);
        Assert.Equal("CardSight Match", result.Card.PlayerName);
        Assert.Equal(CardsightScannerService.ProviderId, result.UsedModelId);
    }

    // === CardSight low-confidence falls through ===

    [Fact]
    public async Task Should_FallBackToOpenRouter_When_CardsightConfidenceBelowThreshold()
    {
        // Settings require Medium; CardSight returns Low → fall through.
        using var image = new TempImageFile();
        var (svc, csH, orH) = Build(
            csResponder: _ => Json(CardsightBody("Low Conf Match", "Low")),
            orResponder: _ => Json(OpenRouterBodyWith(MinimalScannedCardJson)));

        var result = await svc.ScanCardAsync(image.Path);

        Assert.Single(csH.Requests);
        Assert.Single(orH.Requests);
        Assert.Equal("LLM Fallback Player", result.Card.PlayerName);
    }

    // === CardSight returned no detections ===

    [Fact]
    public async Task Should_FallBackToOpenRouter_When_CardsightReturnsNoDetections()
    {
        using var image = new TempImageFile();
        var (svc, csH, orH) = Build(
            csResponder: _ => Json(CardsightEmptyBody),
            orResponder: _ => Json(OpenRouterBodyWith(MinimalScannedCardJson)));

        var result = await svc.ScanCardAsync(image.Path);

        Assert.Single(csH.Requests);
        Assert.Single(orH.Requests);
        Assert.Equal("LLM Fallback Player", result.Card.PlayerName);
    }

    // === CardSight HTTP error falls through ===

    [Fact]
    public async Task Should_FallBackToOpenRouter_When_CardsightReturnsServerError()
    {
        using var image = new TempImageFile();
        var (svc, csH, orH) = Build(
            csResponder: _ => Json(@"{""error"":""boom"",""code"":""server_error""}", HttpStatusCode.InternalServerError),
            orResponder: _ => Json(OpenRouterBodyWith(MinimalScannedCardJson)));

        var result = await svc.ScanCardAsync(image.Path);

        Assert.Single(csH.Requests);
        Assert.Single(orH.Requests);
        Assert.Equal("LLM Fallback Player", result.Card.PlayerName);
    }

    // === Custom prompt always goes to OpenRouter ===

    [Fact]
    public async Task Should_AlwaysRouteCustomPromptToOpenRouter_When_UsingSendCustomPromptAsync()
    {
        using var image = new TempImageFile();
        var (svc, csH, orH) = Build(
            csResponder: _ => throw new InvalidOperationException("CardSight should not be called for custom prompts"),
            orResponder: _ => Json(OpenRouterBodyWith("custom answer text")));

        var result = await svc.SendCustomPromptAsync(image.Path, "What's on this card?");

        Assert.Empty(csH.Requests);
        Assert.Single(orH.Requests);
        Assert.Equal("custom answer text", result);
    }
}
