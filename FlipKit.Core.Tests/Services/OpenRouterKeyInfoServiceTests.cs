using System.Net;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations;
using FlipKit.Core.Tests.Infrastructure;
using NSubstitute;

namespace FlipKit.Core.Tests.Services;

public class OpenRouterKeyInfoServiceTests
{
    private static (OpenRouterKeyInfoService sut, StubHttpMessageHandler handler) Build(
        StubHttpMessageHandler handler,
        string apiKey = "sk-or-test-key")
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { OpenRouterApiKey = apiKey });
        var sut = new OpenRouterKeyInfoService(new HttpClient(handler), settings);
        return (sut, handler);
    }

    // The docs-shape body. Keep separately so multiple tests can assert against it.
    // Uses snake_case JSON like the real OpenRouter response.
    private const string SampleSuccessBody = @"{
        ""data"": {
            ""label"": ""my flipkit key"",
            ""limit"": 25.0,
            ""limit_remaining"": 17.42,
            ""limit_reset"": ""monthly"",
            ""usage"": 12.58,
            ""usage_daily"": 1.25,
            ""usage_weekly"": 4.10,
            ""usage_monthly"": 7.58,
            ""is_free_tier"": false,
            ""include_byok_in_limit"": true,
            ""byok_usage"": 0.0,
            ""byok_usage_daily"": 0.0,
            ""byok_usage_weekly"": 0.0,
            ""byok_usage_monthly"": 0.0
        }
    }";

    [Fact]
    public async Task Should_RoundTripAllFields_When_BodyMatchesDocsShape()
    {
        var (sut, _) = Build(new StubHttpMessageHandler(HttpStatusCode.OK, SampleSuccessBody));

        var info = await sut.GetAsync();

        Assert.Equal("my flipkit key", info.Label);
        Assert.Equal(25.0m, info.Limit);
        Assert.Equal(17.42m, info.LimitRemaining);
        Assert.Equal(12.58m, info.Usage);
        Assert.Equal(1.25m, info.UsageDaily);
        Assert.Equal(4.10m, info.UsageWeekly);
        Assert.Equal(7.58m, info.UsageMonthly);
        Assert.False(info.IsFreeTier);
        // limit_reset = "monthly" is not a parseable timestamp, so we tolerate
        // it by leaving LimitReset null. UI just hides the timestamp line.
        Assert.Null(info.LimitReset);
        // FetchedAt is stamped at call time.
        Assert.True((DateTimeOffset.UtcNow - info.FetchedAt).TotalSeconds < 5);
    }

    [Fact]
    public async Task Should_AcceptNullLimit_When_KeyIsUnlimited()
    {
        // Most paid keys have no explicit credit limit set — both `limit` and
        // `limit_remaining` come back as JSON null. UI shows "no limit".
        const string body = @"{
            ""data"": {
                ""label"": ""unlimited"",
                ""limit"": null,
                ""limit_remaining"": null,
                ""limit_reset"": null,
                ""usage"": 5.50,
                ""usage_daily"": 0.10,
                ""usage_weekly"": 0.50,
                ""usage_monthly"": 5.50,
                ""is_free_tier"": false
            }
        }";
        var (sut, _) = Build(new StubHttpMessageHandler(HttpStatusCode.OK, body));

        var info = await sut.GetAsync();

        Assert.Null(info.Limit);
        Assert.Null(info.LimitRemaining);
        Assert.Null(info.LimitReset);
        Assert.Equal(5.50m, info.Usage);
    }

    [Fact]
    public async Task Should_AttachBearerHeader_When_Calling()
    {
        // The whole point of /api/v1/key over /api/v1/models is that this one
        // requires auth. If we forget the Bearer header it returns 401, not
        // useful data. Verify the header lands.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SampleSuccessBody);
        var (sut, _) = Build(handler, apiKey: "sk-or-zzz");

        await sut.GetAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://openrouter.ai/api/v1/key", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("sk-or-zzz", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Should_ThrowInvalidOperation_When_ApiKeyMissing()
    {
        // Empty key = user hasn't set up Settings yet. Throw a friendly type
        // so the SettingsViewModel can phrase a "set your key first" message
        // instead of a generic network failure.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SampleSuccessBody);
        var (sut, _) = Build(handler, apiKey: "");

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetAsync());

        Assert.Empty(handler.Requests); // never hit the wire
    }

    [Fact]
    public async Task Should_ThrowPaymentRequired_When_ServerReturns402()
    {
        // 402 Payment Required = negative credit balance. Typed so the toast
        // path can show a sticky red notification.
        var (sut, _) = Build(new StubHttpMessageHandler(HttpStatusCode.PaymentRequired, "balance is -$0.42"));

        var ex = await Assert.ThrowsAsync<OpenRouterPaymentRequiredException>(() => sut.GetAsync());

        Assert.Equal(OpenRouterKeyInfoService.KeyEndpointSentinel, ex.ModelId);
        Assert.Contains("-$0.42", ex.ResponseBody);
    }

    [Fact]
    public async Task Should_ThrowRateLimit_When_ServerReturns429()
    {
        // 429 with "daily" / "credit" body → AccountPerDay scope (matches the
        // existing scanner-side parser semantics). Same toast plumbing handles
        // both endpoints.
        var handler = new StubHttpMessageHandler(_ =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("daily quota exceeded"),
            };
            resp.Headers.TryAddWithoutValidation("Retry-After", "60");
            return resp;
        });
        var (sut, _) = Build(handler);

        var ex = await Assert.ThrowsAsync<OpenRouterRateLimitException>(() => sut.GetAsync());

        Assert.Equal(RateLimitScope.AccountPerDay, ex.Scope);
        Assert.Equal(60, ex.RetryAfterSeconds);
    }

    [Fact]
    public async Task Should_ThrowHttpRequestException_When_ServerReturnsGeneric5xx()
    {
        var (sut, _) = Build(new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "boom"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetAsync());

        // Body content + status code both surface in the message so logs are debuggable.
        Assert.Contains("500", ex.Message);
        Assert.Contains("boom", ex.Message);
    }
}
