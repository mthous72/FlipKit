using System.Net;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Core.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Core.Tests.Services;

public class XimilarServiceTests
{
    private static ISettingsService SettingsWithKey(string? key) =>
        Substitute.For<ISettingsService>().Tap(s => s.Load().Returns(new AppSettings { XimilarApiKey = key }));

    private static XimilarService CreateService(StubHttpMessageHandler handler, string? apiKey = "test-key") =>
        new(new HttpClient(handler), SettingsWithKey(apiKey), NullLogger<XimilarService>.Instance);

    // === IsConfigured ===

    [Fact]
    public void Should_ReportConfigured_When_ApiKeyIsSet()
    {
        var svc = new XimilarService(new HttpClient(), SettingsWithKey("k"), NullLogger<XimilarService>.Instance);
        Assert.True(svc.IsConfigured);
    }

    [Fact]
    public void Should_ReportNotConfigured_When_ApiKeyIsMissing()
    {
        var svc = new XimilarService(new HttpClient(), SettingsWithKey(null), NullLogger<XimilarService>.Instance);
        Assert.False(svc.IsConfigured);
    }

    // === RecognizeCardAsync ===

    [Fact]
    public async Task Should_ReturnNull_When_ApiKeyMissing()
    {
        using var image = new TempImageFile();
        var svc = CreateService(new StubHttpMessageHandler(HttpStatusCode.OK, "{}"), apiKey: null);

        var result = await svc.RecognizeCardAsync(image.Path);

        Assert.Null(result);
    }

    [Fact]
    public async Task Should_ReturnFailureResult_When_ApiReturnsErrorStatus()
    {
        using var image = new TempImageFile();
        var handler = new StubHttpMessageHandler(HttpStatusCode.Unauthorized, @"{""detail"":""bad token""}");
        var svc = CreateService(handler);

        var result = await svc.RecognizeCardAsync(image.Path);

        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Contains("Unauthorized", result.ErrorMessage!);
    }

    [Fact]
    public async Task Should_ReturnFailureResult_When_NoRecordsReturned()
    {
        using var image = new TempImageFile();
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, @"{""records"":[]}");
        var svc = CreateService(handler);

        var result = await svc.RecognizeCardAsync(image.Path);

        Assert.NotNull(result);
        Assert.False(result!.Success);
    }

    [Fact]
    public async Task Should_ReturnFailureResult_When_RecordHasNoObjects()
    {
        using var image = new TempImageFile();
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, @"{""records"":[{}]}");
        var svc = CreateService(handler);

        var result = await svc.RecognizeCardAsync(image.Path);

        Assert.NotNull(result);
        Assert.False(result!.Success);
    }

    [Fact]
    public async Task Should_MapBestMatchToCard_When_ResponseHasIdentification()
    {
        using var image = new TempImageFile();
        var body = @"{
            ""records"": [{
                ""_objects"": [{
                    ""prob"": 0.93,
                    ""_identification"": {
                        ""best_match"": {
                            ""name"": ""Mike Trout"",
                            ""card_number"": ""BCP-1"",
                            ""year"": ""2026"",
                            ""set_name"": ""Bowman"",
                            ""subcategory"": ""Baseball"",
                            ""company"": ""Topps"",
                            ""card_type"": ""Rookie""
                        }
                    },
                    ""_tags"": {}
                }]
            }]
        }";
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, body);
        var svc = CreateService(handler);

        var result = await svc.RecognizeCardAsync(image.Path);

        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal("Mike Trout", result.Card!.PlayerName);
        Assert.Equal(2026, result.Card.Year);
        Assert.True(result.Card.IsRookie); // CardType "Rookie" → IsRookie=true
        Assert.Equal(0.93, result.Confidence);
    }

    [Fact]
    public async Task Should_AttachAuthorizationToken_When_SendingRequest()
    {
        using var image = new TempImageFile();
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, @"{""records"":[]}");
        var svc = CreateService(handler, apiKey: "ximilar-secret");

        await svc.RecognizeCardAsync(image.Path);

        Assert.Single(handler.Requests);
        var auth = handler.Requests[0].Headers.Authorization;
        Assert.NotNull(auth);
        Assert.Equal("Token", auth!.Scheme);
        Assert.Equal("ximilar-secret", auth.Parameter);
    }

    // === TestConnectionAsync ===

    [Fact]
    public async Task Should_ReturnTrue_When_ConnectionTestReturnsAnythingButAuthFailure()
    {
        // Per implementation: 401/403 = false, anything else (200, 400, 500, etc.) = true.
        var handler = new StubHttpMessageHandler(HttpStatusCode.BadRequest, @"{""error"":""bad request""}");
        var svc = CreateService(handler);

        Assert.True(await svc.TestConnectionAsync("any-key"));
    }

    [Fact]
    public async Task Should_ReturnFalse_When_ConnectionTestReturnsUnauthorizedOrForbidden()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.Unauthorized, @"{""detail"":""auth failed""}");
        var svc = CreateService(handler);

        Assert.False(await svc.TestConnectionAsync("bad-key"));
    }
}

internal static class NSubstituteExtensions
{
    /// <summary>
    /// Tiny helper for one-line setup-and-return when configuring substitutes.
    /// Allows: <c>Substitute.For&lt;IFoo&gt;().Tap(f => f.Bar().Returns(42))</c>.
    /// </summary>
    public static T Tap<T>(this T target, Action<T> configure)
    {
        configure(target);
        return target;
    }
}
