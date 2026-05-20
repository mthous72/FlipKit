using System.Net;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations;
using FlipKit.Core.Tests.Infrastructure;
using NSubstitute;

namespace FlipKit.Core.Tests.Services;

public class CardsightSubscriptionServiceTests
{
    private static (CardsightSubscriptionService sut, StubHttpMessageHandler handler) Build(
        StubHttpMessageHandler handler,
        string apiKey = "cs-test-key")
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { CardsightApiKey = apiKey });
        var sut = new CardsightSubscriptionService(new HttpClient(handler), settings);
        return (sut, handler);
    }

    // Real CardSight /v1/subscription shape: aggregate `calls` + per-key list.
    private const string SampleSuccessBody = @"{
        ""calls"": 120,
        ""api_keys"": [
            { ""key"": ""cs-****abcd"", ""calls"": 120 }
        ]
    }";

    [Fact]
    public async Task Should_MapCallsAndComputeRemaining_When_BodyMatchesApiShape()
    {
        var (sut, _) = Build(new StubHttpMessageHandler(HttpStatusCode.OK, SampleSuccessBody));

        var status = await sut.GetAsync();

        Assert.Equal(120, status.CallsUsed);
        Assert.Equal(750, status.FreeTierMonthlyQuota);
        // remaining = max(0, 750 - 120)
        Assert.Equal(630, status.CallsRemaining);
        var key = Assert.Single(status.ApiKeys);
        Assert.Equal("cs-****abcd", key.Key);
        Assert.Equal(120, key.Calls);
        // FetchedAt is stamped at call time.
        Assert.True((DateTimeOffset.UtcNow - status.FetchedAt).TotalSeconds < 5);
    }

    [Fact]
    public async Task Should_ClampRemainingToZero_When_UsageExceedsFreeTierQuota()
    {
        // Paid users can exceed 750. Remaining must never go negative.
        const string body = @"{ ""calls"": 1200, ""api_keys"": [] }";
        var (sut, _) = Build(new StubHttpMessageHandler(HttpStatusCode.OK, body));

        var status = await sut.GetAsync();

        Assert.Equal(1200, status.CallsUsed);
        Assert.Equal(0, status.CallsRemaining);
        Assert.Empty(status.ApiKeys);
    }

    [Fact]
    public async Task Should_AttachApiKeyHeader_When_Calling()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SampleSuccessBody);
        var (sut, _) = Build(handler, apiKey: "cs-zzz");

        await sut.GetAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.cardsight.ai/v1/subscription", request.RequestUri!.ToString());
        Assert.True(request.Headers.TryGetValues("X-API-Key", out var values));
        Assert.Equal("cs-zzz", Assert.Single(values!));
    }

    [Fact]
    public async Task Should_ThrowNotConfigured_When_ApiKeyMissing()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SampleSuccessBody);
        var (sut, _) = Build(handler, apiKey: "");

        var ex = await Assert.ThrowsAsync<CardsightException>(() => sut.GetAsync());

        Assert.Equal(CardsightFailureReason.NotConfigured, ex.Reason);
        Assert.Empty(handler.Requests); // never hit the wire
    }

    [Fact]
    public async Task Should_ThrowInvalidKey_When_ServerReturns401()
    {
        var (sut, _) = Build(new StubHttpMessageHandler(HttpStatusCode.Unauthorized, "bad key"));

        var ex = await Assert.ThrowsAsync<CardsightException>(() => sut.GetAsync());

        Assert.Equal(CardsightFailureReason.InvalidKey, ex.Reason);
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task Should_ThrowQuotaExceeded_When_ServerReturns402()
    {
        var (sut, _) = Build(new StubHttpMessageHandler(HttpStatusCode.PaymentRequired, "over quota"));

        var ex = await Assert.ThrowsAsync<CardsightException>(() => sut.GetAsync());

        Assert.Equal(CardsightFailureReason.QuotaExceeded, ex.Reason);
    }

    [Fact]
    public async Task Should_ThrowRateLimited_When_ServerReturns429()
    {
        var (sut, _) = Build(new StubHttpMessageHandler(HttpStatusCode.TooManyRequests, "slow down"));

        var ex = await Assert.ThrowsAsync<CardsightException>(() => sut.GetAsync());

        Assert.Equal(CardsightFailureReason.RateLimited, ex.Reason);
    }

    [Fact]
    public async Task Should_ThrowTransient_When_ServerReturns5xx()
    {
        var (sut, _) = Build(new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "boom"));

        var ex = await Assert.ThrowsAsync<CardsightException>(() => sut.GetAsync());

        Assert.Equal(CardsightFailureReason.Transient, ex.Reason);
    }
}
