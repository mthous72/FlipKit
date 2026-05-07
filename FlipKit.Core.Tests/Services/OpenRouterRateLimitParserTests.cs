using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services.Implementations;
using Xunit;

namespace FlipKit.Core.Tests.Services;

public class OpenRouterRateLimitParserTests
{
    // === DetectScope ===

    [Theory]
    [InlineData("provider rate limit exceeded")]
    [InlineData("upstream rate limit")]
    [InlineData("The provider returned a 429")]
    public void DetectScope_Returns_ProviderUpstream_When_ResponseMentionsProvider(string body)
    {
        Assert.Equal(RateLimitScope.ProviderUpstream, OpenRouterRateLimitParser.DetectScope(body));
    }

    [Theory]
    [InlineData("you have exceeded your daily quota")]
    [InlineData("daily limit reached")]
    [InlineData("insufficient credits")]
    [InlineData("credit quota exhausted")]
    [InlineData("per day limit")]
    public void DetectScope_Returns_AccountPerDay_When_ResponseMentionsDailyOrCredits(string body)
    {
        Assert.Equal(RateLimitScope.AccountPerDay, OpenRouterRateLimitParser.DetectScope(body));
    }

    [Theory]
    [InlineData("rate limit: 10 requests per minute")]
    [InlineData("rpm limit exceeded")]
    [InlineData("too many requests per second")]
    [InlineData("rps exceeded")]
    public void DetectScope_Returns_AccountPerMinute_When_ResponseMentionsPerMinute(string body)
    {
        Assert.Equal(RateLimitScope.AccountPerMinute, OpenRouterRateLimitParser.DetectScope(body));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("rate limit exceeded")]
    [InlineData("too many requests")]
    public void DetectScope_Returns_Unknown_When_BodyIsAmbiguous(string body)
    {
        Assert.Equal(RateLimitScope.Unknown, OpenRouterRateLimitParser.DetectScope(body));
    }

    [Fact]
    public void DetectScope_IsCaseInsensitive()
    {
        Assert.Equal(RateLimitScope.AccountPerDay, OpenRouterRateLimitParser.DetectScope("DAILY LIMIT EXCEEDED"));
        Assert.Equal(RateLimitScope.ProviderUpstream, OpenRouterRateLimitParser.DetectScope("PROVIDER THROTTLED"));
    }

    // Provider takes priority over day-limit keywords when both appear
    [Fact]
    public void DetectScope_PrioritizesProvider_Over_DailyKeywords()
    {
        var body = "provider upstream daily limit";
        Assert.Equal(RateLimitScope.ProviderUpstream, OpenRouterRateLimitParser.DetectScope(body));
    }

    // === ParseRetryAfter ===

    [Fact]
    public void ParseRetryAfter_Returns_Null_When_HeaderIsNull()
    {
        Assert.Null(OpenRouterRateLimitParser.ParseRetryAfter(null));
    }

    [Fact]
    public void ParseRetryAfter_Returns_Integer_When_HeaderIsNumeric()
    {
        Assert.Equal(60, OpenRouterRateLimitParser.ParseRetryAfter("60"));
    }

    [Fact]
    public void ParseRetryAfter_Returns_Integer_With_Whitespace()
    {
        Assert.Equal(30, OpenRouterRateLimitParser.ParseRetryAfter("  30  "));
    }

    [Fact]
    public void ParseRetryAfter_Returns_Null_When_HeaderIsNonNumeric()
    {
        Assert.Null(OpenRouterRateLimitParser.ParseRetryAfter("Fri, 01 Jan 2027 00:00:00 GMT"));
    }

    // === Parse (integration) ===

    [Fact]
    public void Parse_Combines_Scope_And_RetryAfter()
    {
        var ex = OpenRouterRateLimitParser.Parse(
            "rate limit: 10 requests per minute exceeded",
            "45",
            "google/gemini-flash-1.5");

        Assert.Equal(RateLimitScope.AccountPerMinute, ex.Scope);
        Assert.Equal(45, ex.RetryAfterSeconds);
        Assert.Equal("google/gemini-flash-1.5", ex.ModelId);
    }

    [Fact]
    public void Parse_Returns_AccountPerDay_With_Null_RetryAfter_When_HeaderMissing()
    {
        var ex = OpenRouterRateLimitParser.Parse(
            "you have exceeded your daily credit quota",
            null,
            "meta-llama/llama-3.2-11b-vision-instruct:free");

        Assert.Equal(RateLimitScope.AccountPerDay, ex.Scope);
        Assert.Null(ex.RetryAfterSeconds);
    }
}
