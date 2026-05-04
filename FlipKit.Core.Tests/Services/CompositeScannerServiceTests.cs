using System.Net;
using System.Text.Json;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Core.Tests.Services;

/// <summary>
/// Composite scanner tests use real <see cref="OpenRouterScannerService"/> and
/// real <see cref="XimilarService"/> instances backed by stub HTTP handlers
/// (per Phase 4b walk-through Q-a option ii — the OpenRouter scanner is a concrete
/// class with no virtual methods, so NSubstitute can't mock it cleanly without
/// extracting an interface in Phase 5).
/// </summary>
public class CompositeScannerServiceTests
{
    private static ISettingsService SettingsWith(string? openRouterKey = "or-key", string? ximilarKey = "xim-key") =>
        Substitute.For<ISettingsService>().Tap(s => s.Load().Returns(new AppSettings
        {
            OpenRouterApiKey = openRouterKey,
            XimilarApiKey = ximilarKey,
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

    private static (CompositeScannerService svc, StubHttpMessageHandler ximHandler, StubHttpMessageHandler orHandler) Build(
        XimilarScanMode mode,
        Func<HttpRequestMessage, HttpResponseMessage> ximResponder,
        Func<HttpRequestMessage, HttpResponseMessage> orResponder,
        ISettingsService? settings = null)
    {
        var settingsSvc = settings ?? SettingsWith();
        var ximHandler = new StubHttpMessageHandler(ximResponder);
        var orHandler = new StubHttpMessageHandler(orResponder);

        var ximilar = new XimilarService(new HttpClient(ximHandler), settingsSvc, NullLogger<XimilarService>.Instance);
        var openRouter = new OpenRouterScannerService(new HttpClient(orHandler), settingsSvc, NullLogger<OpenRouterScannerService>.Instance);
        var composite = new CompositeScannerService(ximilar, openRouter, NullLogger<CompositeScannerService>.Instance);
        return (composite, ximHandler, orHandler);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static HttpResponseMessage NotFound() =>
        new(HttpStatusCode.NotFound) { Content = new StringContent("{}") };

    // === Ximilar disabled mode ===

    [Fact]
    public async Task Should_SkipXimilarAndUseOpenRouter_When_ModeIsDisabled()
    {
        using var image = new TempImageFile();
        var (svc, ximH, orH) = Build(
            XimilarScanMode.Disabled,
            ximResponder: _ => throw new InvalidOperationException("Ximilar should not be called"),
            orResponder: _ => Json(OpenRouterBodyWith(MinimalScannedCardJson)));

        var result = await svc.ScanCardAsync(image.Path, ximilarMode: XimilarScanMode.Disabled);

        Assert.Empty(ximH.Requests);
        Assert.Single(orH.Requests);
        Assert.Equal("LLM Fallback Player", result.Card.PlayerName);
    }

    // === Ximilar not configured ===

    [Fact]
    public async Task Should_SkipXimilarAndUseOpenRouter_When_XimilarApiKeyMissing()
    {
        using var image = new TempImageFile();
        var (svc, ximH, orH) = Build(
            XimilarScanMode.Standard,
            ximResponder: _ => throw new InvalidOperationException("Ximilar should not be called"),
            orResponder: _ => Json(OpenRouterBodyWith(MinimalScannedCardJson)),
            settings: SettingsWith(ximilarKey: null));

        var result = await svc.ScanCardAsync(image.Path);

        Assert.Empty(ximH.Requests);
        Assert.Single(orH.Requests);
        Assert.Equal("LLM Fallback Player", result.Card.PlayerName);
    }

    // === Ximilar high confidence wins ===

    [Fact]
    public async Task Should_UseXimilarResult_When_ConfidenceIsHighEnough()
    {
        using var image = new TempImageFile();
        const string ximBody = @"{
            ""records"": [{
                ""_objects"": [{
                    ""prob"": 0.92,
                    ""_identification"": {
                        ""best_match"": {
                            ""name"": ""Ximilar Match"",
                            ""year"": ""2025"",
                            ""subcategory"": ""Baseball""
                        }
                    },
                    ""_tags"": {}
                }]
            }]
        }";

        var (svc, ximH, orH) = Build(
            XimilarScanMode.Standard,
            ximResponder: _ => Json(ximBody),
            orResponder: _ => throw new InvalidOperationException("OpenRouter should not be called"));

        var result = await svc.ScanCardAsync(image.Path);

        Assert.Single(ximH.Requests);
        Assert.Empty(orH.Requests);
        Assert.Equal("Ximilar Match", result.Card.PlayerName);
    }

    // === Ximilar low confidence falls through ===

    [Fact]
    public async Task Should_FallBackToOpenRouter_When_XimilarConfidenceIsLow()
    {
        // Ximilar found a match but confidence < 0.8 → fall back to LLM.
        using var image = new TempImageFile();
        const string ximBody = @"{
            ""records"": [{
                ""_objects"": [{
                    ""prob"": 0.5,
                    ""_identification"": {
                        ""best_match"": {
                            ""name"": ""Low Confidence Match"",
                            ""subcategory"": ""Baseball""
                        }
                    },
                    ""_tags"": {}
                }]
            }]
        }";

        var (svc, ximH, orH) = Build(
            XimilarScanMode.Standard,
            ximResponder: _ => Json(ximBody),
            orResponder: _ => Json(OpenRouterBodyWith(MinimalScannedCardJson)));

        var result = await svc.ScanCardAsync(image.Path);

        Assert.Single(ximH.Requests);
        Assert.Single(orH.Requests);
        Assert.Equal("LLM Fallback Player", result.Card.PlayerName); // OpenRouter wins
    }

    // === Ximilar returned no match ===

    [Fact]
    public async Task Should_FallBackToOpenRouter_When_XimilarReturnsNoMatch()
    {
        using var image = new TempImageFile();
        var (svc, ximH, orH) = Build(
            XimilarScanMode.Standard,
            ximResponder: _ => Json(@"{""records"":[]}"),
            orResponder: _ => Json(OpenRouterBodyWith(MinimalScannedCardJson)));

        var result = await svc.ScanCardAsync(image.Path);

        Assert.Single(ximH.Requests);
        Assert.Single(orH.Requests);
        Assert.Equal("LLM Fallback Player", result.Card.PlayerName);
    }

    // === Custom prompt always goes to OpenRouter ===

    [Fact]
    public async Task Should_AlwaysRouteCustomPromptToOpenRouter_When_UsingSendCustomPromptAsync()
    {
        using var image = new TempImageFile();
        var (svc, ximH, orH) = Build(
            XimilarScanMode.Standard, // even with Ximilar enabled...
            ximResponder: _ => throw new InvalidOperationException("Ximilar should not be called for custom prompts"),
            orResponder: _ => Json(OpenRouterBodyWith("custom answer text")));

        var result = await svc.SendCustomPromptAsync(image.Path, "What's on this card?");

        Assert.Empty(ximH.Requests);
        Assert.Single(orH.Requests);
        Assert.Equal("custom answer text", result);
    }
}
