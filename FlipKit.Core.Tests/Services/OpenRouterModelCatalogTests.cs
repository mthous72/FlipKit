using System.Net;
using FlipKit.Core.Services.Scanning;
using FlipKit.Core.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlipKit.Core.Tests.Services;

public class OpenRouterModelCatalogTests
{
    private static OpenRouterModelCatalog CreateCatalog(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<OpenRouterModelCatalog>.Instance);

    /// <summary>
    /// Sample /api/v1/models response with a free vision model, a paid vision model,
    /// a non-vision text model (should be filtered out), and an Auto Router sentinel
    /// with negative pricing (should be filtered out).
    /// </summary>
    private const string SampleModelsResponse = @"{
        ""data"": [
            {
                ""id"": ""google/gemma-4-31b-it:free"",
                ""name"": ""Gemma 4 31B (free)"",
                ""description"": ""Free vision-language"",
                ""architecture"": { ""input_modalities"": [""text"", ""image""], ""output_modalities"": [""text""] },
                ""pricing"": { ""prompt"": ""0"", ""completion"": ""0"" }
            },
            {
                ""id"": ""anthropic/claude-3.5-sonnet"",
                ""name"": ""Claude 3.5 Sonnet"",
                ""description"": ""Premium vision"",
                ""architecture"": { ""input_modalities"": [""text"", ""image""], ""output_modalities"": [""text""] },
                ""pricing"": { ""prompt"": ""0.000003"", ""completion"": ""0.000015"" }
            },
            {
                ""id"": ""openrouter/auto"",
                ""name"": ""Auto Router"",
                ""description"": ""Routes to best model"",
                ""architecture"": { ""input_modalities"": [""text"", ""image""], ""output_modalities"": [""text""] },
                ""pricing"": { ""prompt"": ""-0.000001"", ""completion"": ""-0.000001"" }
            },
            {
                ""id"": ""google/gemma-3-27b-it"",
                ""name"": ""Gemma 3 27B (paid)"",
                ""description"": ""Cheap paid vision"",
                ""architecture"": { ""input_modalities"": [""text"", ""image""], ""output_modalities"": [""text""] },
                ""pricing"": { ""prompt"": ""0.0000001"", ""completion"": ""0.0000005"" }
            },
            {
                ""id"": ""text-only/something"",
                ""name"": ""Text Only"",
                ""description"": ""Not a vision model"",
                ""architecture"": { ""input_modalities"": [""text""], ""output_modalities"": [""text""] },
                ""pricing"": { ""prompt"": ""0"", ""completion"": ""0"" }
            },
            {
                ""id"": ""google/lyria-002"",
                ""name"": ""Lyria 002"",
                ""description"": ""Music generation (mis-classified as vision)"",
                ""architecture"": { ""input_modalities"": [""text"", ""image""], ""output_modalities"": [""text""] },
                ""pricing"": { ""prompt"": ""0.001"", ""completion"": ""0.001"" }
            }
        ]
    }";

    // === Filtering / classification ===

    [Fact]
    public async Task Should_OnlyReturnVisionLanguageModels_When_FetchingCatalog()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SampleModelsResponse);
        var catalog = CreateCatalog(handler);

        var result = await catalog.GetAsync();

        // Free: Gemma 4 31B free. Text-only and Lyria filtered out.
        Assert.Single(result.FreeVisionModels);
        Assert.Equal("google/gemma-4-31b-it:free", result.FreeVisionModels[0].Id);
    }

    [Fact]
    public async Task Should_FilterOutAutoRouterSentinel_When_NegativePriced()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SampleModelsResponse);
        var catalog = CreateCatalog(handler);

        var result = await catalog.GetAsync();

        Assert.DoesNotContain(result.PaidVisionModels, m => m.Id == "openrouter/auto");
    }

    [Fact]
    public async Task Should_FilterOutMusicGenerationModels_When_MisClassifiedAsVision()
    {
        // Lyria + similar audio/music gen models incorrectly report image input.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SampleModelsResponse);
        var catalog = CreateCatalog(handler);

        var result = await catalog.GetAsync();

        Assert.DoesNotContain(result.PaidVisionModels.Concat(result.FreeVisionModels), m => m.Id.Contains("lyria"));
    }

    [Fact]
    public async Task Should_SortPaidModelsCheapestFirst_When_FetchingCatalog()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SampleModelsResponse);
        var catalog = CreateCatalog(handler);

        var result = await catalog.GetAsync();

        // Gemma 3 27B paid (0.0000001) cheaper than Claude (0.000003).
        Assert.Equal(2, result.PaidVisionModels.Count);
        Assert.True(result.PaidVisionModels[0].PromptPricePerMillion < result.PaidVisionModels[1].PromptPricePerMillion);
        Assert.Equal("google/gemma-3-27b-it", result.PaidVisionModels[0].Id);
    }

    [Fact]
    public async Task Should_ConvertPerTokenPricingToPerMillion_When_Mapping()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SampleModelsResponse);
        var catalog = CreateCatalog(handler);

        var result = await catalog.GetAsync();

        var claude = result.PaidVisionModels.First(m => m.Id == "anthropic/claude-3.5-sonnet");
        // 0.000003 * 1_000_000 = 3.0
        Assert.Equal(3.0m, claude.PromptPricePerMillion);
        Assert.Equal(15.0m, claude.CompletionPricePerMillion);
    }

    // === Caching / single-flight ===

    [Fact]
    public async Task Should_OnlyMakeOneHttpCall_When_CalledMultipleTimes()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SampleModelsResponse);
        var catalog = CreateCatalog(handler);

        await catalog.GetAsync();
        await catalog.GetAsync();
        await catalog.GetAsync();

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Should_RefetchAfterInvalidate_When_CacheManuallyCleared()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SampleModelsResponse);
        var catalog = CreateCatalog(handler);

        await catalog.GetAsync();
        catalog.InvalidateCache();
        await catalog.GetAsync();

        Assert.Equal(2, handler.Requests.Count);
    }

    // === Failure path ===

    [Fact]
    public async Task Should_ReturnEmptyCatalog_When_FetchFails()
    {
        // Phase 5.2 will change this to return the static fallback catalog instead;
        // for now, the contract is "empty + warning logged + caller falls back gracefully".
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("network down"));
        var catalog = CreateCatalog(handler);

        var result = await catalog.GetAsync();

        Assert.Empty(result.FreeVisionModels);
        Assert.Empty(result.PaidVisionModels);
    }

    [Fact]
    public async Task Should_ReturnEmptyCatalog_When_DataFieldIsMissing()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        var catalog = CreateCatalog(handler);

        var result = await catalog.GetAsync();

        Assert.Empty(result.FreeVisionModels);
        Assert.Empty(result.PaidVisionModels);
    }
}
