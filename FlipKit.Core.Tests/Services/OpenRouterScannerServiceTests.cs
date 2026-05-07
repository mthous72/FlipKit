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
    public async Task Should_Throw_When_ModelReturns404()
    {
        // GetFallbackChain now returns a single model — no silent substitution.
        // A 404 on the explicit model should throw so the caller can handle it.
        var handler = new StubHttpMessageHandler(HttpStatusCode.NotFound,
            @"{""error"":""model not found""}");
        var svc = CreateService(handler);
        using var image = new TempImageFile();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ScanCardAsync(image.Path));
        Assert.Contains("404", ex.Message); // actual API error is surfaced directly
        Assert.Equal(1, handler.Requests.Count); // only one model tried
    }

    [Fact]
    public async Task Should_ThrowWithSummaryOfFailures_When_AllModelsFail()
    {
        // GetFallbackChain returns a single model, so exactly one HTTP request is made
        // before the scan fails. Multi-model rotation is the caller's responsibility.
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(@"{""error"":""model not found""}"),
            });
        var svc = CreateService(handler);
        using var image = new TempImageFile();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ScanCardAsync(image.Path));
        Assert.Contains("404", ex.Message); // last exception's message surfaced directly
        Assert.Equal(1, handler.Requests.Count);
    }

    [Fact]
    public async Task Should_FallBackOn5xx_When_FirstModelReturnsServerError()
    {
        // Positive test for the Phase 5a D2 fix — pre-fix, 500/502/503/504 errors
        // propagated immediately because IsRetryableHttpError only matched digit
        // substrings ("500") but HttpStatusCode.InternalServerError.ToString() is
        // the enum name ("InternalServerError") with no digit. The fix added the
        // integer to the throw-site message format.
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(@"{""error"":""upstream failure""}"),
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
        Assert.Equal(2, handler.Requests.Count); // proved fallback happened on 500
    }

    [Fact]
    public async Task Should_Throw_When_ModelReturnsInvalidJson()
    {
        // GetFallbackChain returns a single model — invalid JSON from the chosen model
        // surfaces as an exception rather than silently falling back to another model.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK,
            OpenRouterResponseWith("not even close to JSON"));
        var svc = CreateService(handler);
        using var image = new TempImageFile();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ScanCardAsync(image.Path));
        Assert.Contains("not even close to JSON", ex.Message); // JsonException surfaced directly
        Assert.Equal(1, handler.Requests.Count);
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

    // ============================================================
    // OcrHint preamble + drift guard
    // ============================================================

    [Fact]
    public void BuildSoftHintPreamble_Used_When_VerifiedFieldNames_Empty()
    {
        var hint = new OcrHint
        {
            PlayerName = "Justin Herbert",
            Year = 2024,
            Brand = "Mosaic",
        };

        var preamble = OpenRouterScannerService.BuildOcrHintPreamble(hint);

        Assert.Contains("PRELIMINARY OCR DATA", preamble);
        Assert.DoesNotContain("CONFIRMED FIELDS", preamble);
        Assert.Contains("Justin Herbert", preamble);
    }

    [Fact]
    public void BuildLockedHintPreamble_Used_When_AnyField_IsVerified()
    {
        var hint = new OcrHint
        {
            PlayerName = "Justin Herbert",
            Year = 2024,
            Brand = "Mosaic",
            CardNumber = "12",   // populated but unverified
            VerifiedFieldNames = { "player_name", "year", "brand" },
        };

        var preamble = OpenRouterScannerService.BuildOcrHintPreamble(hint);

        // CONFIRMED section lists the verified fields with JSON-key labels
        Assert.Contains("CONFIRMED FIELDS", preamble);
        Assert.Contains("player_name: \"Justin Herbert\"", preamble);
        Assert.Contains("year: 2024", preamble);
        Assert.Contains("brand: \"Mosaic\"", preamble);
        // UNVERIFIED section captures populated-but-not-verified fields
        Assert.Contains("UNVERIFIED OCR HINTS", preamble);
        Assert.Contains("card_number: \"12\" (unverified)", preamble);
        // Soft-hint preamble should NOT also be appended
        Assert.DoesNotContain("PRELIMINARY OCR DATA", preamble);
    }

    [Fact]
    public void BuildLockedHintPreamble_IncludesRawOcrText_WhenPresent()
    {
        var hint = new OcrHint
        {
            PlayerName = "X",
            VerifiedFieldNames = { "player_name" },
            AllVisibleText = { "TOPPS CHROME 2024", "Aaron Judge", "New York Yankees" },
        };

        var preamble = OpenRouterScannerService.BuildOcrHintPreamble(hint);

        Assert.Contains("RAW OCR TEXT", preamble);
        Assert.Contains("TOPPS CHROME 2024", preamble);
        Assert.Contains("Aaron Judge", preamble);
        Assert.Contains("New York Yankees", preamble);
    }

    [Fact]
    public void ApplyVerifiedFieldOverrides_Restores_PlayerName_When_LlmDrifts()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}"); // unused
        var svc = CreateService(handler);

        var card = new Card { PlayerName = "Justin Herburt" }; // LLM mistyped
        var hint = new OcrHint
        {
            PlayerName = "Justin Herbert",
            VerifiedFieldNames = { "player_name" },
        };

        svc.ApplyVerifiedFieldOverrides(card, hint);

        Assert.Equal("Justin Herbert", card.PlayerName);
    }

    [Fact]
    public void ApplyVerifiedFieldOverrides_DoesNotModify_When_LlmEchoedCorrectly()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        var svc = CreateService(handler);

        var card = new Card { PlayerName = "Justin Herbert" };
        var hint = new OcrHint
        {
            PlayerName = "Justin Herbert",
            VerifiedFieldNames = { "player_name" },
        };

        svc.ApplyVerifiedFieldOverrides(card, hint);

        Assert.Equal("Justin Herbert", card.PlayerName); // unchanged
    }

    [Fact]
    public void ApplyVerifiedFieldOverrides_LeavesUnverifiedFields_Untouched()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        var svc = CreateService(handler);

        // ParallelName is populated on the hint but NOT in VerifiedFieldNames.
        // The LLM was free to override it; the drift guard should not restore.
        var card = new Card { PlayerName = "Justin Herbert", ParallelName = "Disco Prizm" };
        var hint = new OcrHint
        {
            PlayerName = "Justin Herbert",
            ParallelName = "Silver",
            VerifiedFieldNames = { "player_name" },
        };

        svc.ApplyVerifiedFieldOverrides(card, hint);

        Assert.Equal("Justin Herbert", card.PlayerName);
        Assert.Equal("Disco Prizm", card.ParallelName); // LLM's value kept
    }

    [Fact]
    public void ApplyVerifiedFieldOverrides_Restores_Year_When_LlmDrifts()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        var svc = CreateService(handler);

        var card = new Card { Year = 2023 }; // LLM picked the wrong year
        var hint = new OcrHint
        {
            Year = 2024,
            VerifiedFieldNames = { "year" },
        };

        svc.ApplyVerifiedFieldOverrides(card, hint);

        Assert.Equal(2024, card.Year);
    }

    [Fact]
    public void ApplyVerifiedFieldOverrides_Restores_Sport_FromString_To_Enum()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        var svc = CreateService(handler);

        var card = new Card { Sport = Sport.Baseball }; // LLM said wrong sport
        var hint = new OcrHint
        {
            Sport = "Football",
            VerifiedFieldNames = { "sport" },
        };

        svc.ApplyVerifiedFieldOverrides(card, hint);

        Assert.Equal(Sport.Football, card.Sport);
    }
}
