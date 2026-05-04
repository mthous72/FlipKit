using System.Net;
using System.Text.Json;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Core.Tests.Services;

public class OpenRouterScannerServiceTests
{
    private static ISettingsService SettingsWithApiKey(string? key = "sk-or-test")
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { OpenRouterApiKey = key });
        return settings;
    }

    private static OpenRouterScannerService CreateService(StubHttpMessageHandler handler, ISettingsService? settings = null)
    {
        return new OpenRouterScannerService(
            new HttpClient(handler),
            settings ?? SettingsWithApiKey(),
            NullLogger<OpenRouterScannerService>.Instance);
    }

    /// <summary>
    /// Builds a successful OpenRouter chat-completions response wrapping the given
    /// inner content (which the scanner expects to be a JSON object matching ScannedCardData).
    /// </summary>
    private static string OpenRouterResponseWith(string innerContent)
    {
        // OpenRouter wraps the model output in choices[0].message.content as a string.
        var inner = JsonSerializer.Serialize(innerContent); // re-quotes + escapes inner JSON
        return $@"{{""choices"":[{{""message"":{{""content"":{inner}}},""finish_reason"":""stop""}}]}}";
    }

    private const string MinimalScannedCardJson = @"{
        ""player_name"": ""Mike Trout"",
        ""card_number"": ""BCP-1"",
        ""year"": 2026,
        ""sport"": ""Baseball"",
        ""manufacturer"": ""Topps"",
        ""brand"": ""Bowman"",
        ""set_name"": ""Bowman Chrome Prospects"",
        ""team"": ""Angels"",
        ""variation_type"": ""Refractor"",
        ""parallel_name"": ""Silver"",
        ""serial_numbered"": ""/199"",
        ""is_rookie"": true,
        ""is_auto"": false,
        ""is_relic"": false,
        ""is_short_print"": false,
        ""is_graded"": false,
        ""confidence"": { ""player_name"": ""high"", ""year"": ""high"" }
    }";

    // === Happy path ===

    [Fact]
    public async Task Should_ReturnMappedScanResult_When_OpenRouterRespondsSuccessfully()
    {
        using var image = new TempImageFile();
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            OpenRouterResponseWith(MinimalScannedCardJson));
        var svc = CreateService(handler);

        var result = await svc.ScanCardAsync(image.Path);

        Assert.Equal("Mike Trout", result.Card.PlayerName);
        Assert.Equal(2026, result.Card.Year);
        Assert.Equal(Sport.Baseball, result.Card.Sport);
        Assert.True(result.Card.IsRookie);
        Assert.Equal(image.Path, result.Card.ImagePathFront);
    }

    [Fact]
    public async Task Should_AttachAuthorizationHeader_When_SendingRequest()
    {
        using var image = new TempImageFile();
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            OpenRouterResponseWith(MinimalScannedCardJson));
        var svc = CreateService(handler, SettingsWithApiKey("sk-or-secret"));

        await svc.ScanCardAsync(image.Path);

        var auth = handler.Requests[0].Headers.Authorization;
        Assert.NotNull(auth);
        Assert.Equal("Bearer", auth!.Scheme);
        Assert.Equal("sk-or-secret", auth.Parameter);
    }

    [Fact]
    public async Task Should_DefaultVariationTypeToBase_When_ScannedDataOmitsIt()
    {
        // Card.VariationType has [Required]-style default of "Base"; the mapper enforces it
        // when the AI returns null for variation_type.
        var noVariation = MinimalScannedCardJson.Replace(@"""variation_type"": ""Refractor""", @"""variation_type"": null");
        using var image = new TempImageFile();
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            OpenRouterResponseWith(noVariation));
        var svc = CreateService(handler);

        var result = await svc.ScanCardAsync(image.Path);

        Assert.Equal("Base", result.Card.VariationType);
    }

    // === Code-block stripping ===

    [Fact]
    public async Task Should_StripJsonCodeFences_When_ModelWrapsResponseInMarkdown()
    {
        // Some models return ```json {...} ``` instead of bare JSON.
        var wrapped = "```json\n" + MinimalScannedCardJson + "\n```";
        using var image = new TempImageFile();
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            OpenRouterResponseWith(wrapped));
        var svc = CreateService(handler);

        var result = await svc.ScanCardAsync(image.Path);

        Assert.Equal("Mike Trout", result.Card.PlayerName);
    }

    [Fact]
    public async Task Should_ExtractJson_When_ModelPrependsExplanatoryText()
    {
        // Some models prepend prose like "Here is the JSON:" before the actual object.
        var withPrefix = "Here is the parsed card data:\n\n" + MinimalScannedCardJson;
        using var image = new TempImageFile();
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            OpenRouterResponseWith(withPrefix));
        var svc = CreateService(handler);

        var result = await svc.ScanCardAsync(image.Path);

        Assert.Equal("Mike Trout", result.Card.PlayerName);
    }

    // === Failure / fallback ===

    [Fact]
    public async Task Should_Throw_When_ApiKeyIsMissing()
    {
        using var image = new TempImageFile();
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        var svc = CreateService(handler, SettingsWithApiKey(key: null));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ScanCardAsync(image.Path));
        Assert.Contains("API key", ex.Message);
    }

    [Fact]
    public async Task Should_FallBackToNextModel_When_FirstModelReturns404()
    {
        // The fallback chain should walk free-models in order on retryable errors.
        // First request: 404 (model not found) → handler's 2nd response = success.
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(@"{""error"":""model not found""}"),
        });
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(OpenRouterResponseWith(MinimalScannedCardJson)),
        });

        var handler = new StubHttpMessageHandler(_ => responses.Dequeue());
        var svc = CreateService(handler);
        using var image = new TempImageFile();

        var result = await svc.ScanCardAsync(image.Path);

        Assert.Equal("Mike Trout", result.Card.PlayerName);
        Assert.Equal(2, handler.Requests.Count); // proved fallback happened
    }

    [Fact]
    public async Task Should_ThrowWithSummaryOfFailures_When_AllModelsFail()
    {
        // NB: using 404/NotFound to drive fallback — `IsRetryableHttpError` only
        // recognizes 404 reliably (it explicitly checks `msg.Contains("NotFound")`).
        // For 5xx / 429, the check looks for digit substrings ("500", "502", etc.),
        // but HttpClient surfaces those as enum names ("InternalServerError",
        // "ServiceUnavailable", "TooManyRequests") with no digit, so the retry
        // filter misses them and the exception propagates without fallback.
        // Bug logged for Phase 5 — see AUDIT-2026-05 follow-ups.
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(@"{""error"":""model not found""}"),
            });
        var svc = CreateService(handler);
        using var image = new TempImageFile();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ScanCardAsync(image.Path));
        Assert.Contains("All", ex.Message);
        Assert.Contains("models failed", ex.Message);
        // Default starting model is index 3 in FreeVisionModels (nemotron 12B), so the
        // chain walks 2 models (indices 3 and 4) before giving up.
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Should_FallBackOnInvalidJson_When_ModelReturnsGarbage()
    {
        // The first response is HTTP 200 but the inner content isn't valid JSON.
        // Scanner should treat that as a JsonException and fall through to the next model.
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(OpenRouterResponseWith("not even close to JSON")),
        });
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(OpenRouterResponseWith(MinimalScannedCardJson)),
        });

        var handler = new StubHttpMessageHandler(_ => responses.Dequeue());
        var svc = CreateService(handler);
        using var image = new TempImageFile();

        var result = await svc.ScanCardAsync(image.Path);

        Assert.Equal("Mike Trout", result.Card.PlayerName);
        Assert.Equal(2, handler.Requests.Count);
    }

    // === Custom prompt ===

    [Fact]
    public async Task Should_ReturnRawContentString_When_UsingSendCustomPromptAsync()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            OpenRouterResponseWith("Just some free-text answer."));
        var svc = CreateService(handler);
        using var image = new TempImageFile();

        var result = await svc.SendCustomPromptAsync(image.Path, "Describe this image.");

        Assert.Equal("Just some free-text answer.", result);
    }
}
