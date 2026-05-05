using System.Net;
using System.Text;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations;
using FlipKit.Core.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Core.Tests.Services;

public class EbayBrowseApiClientTests
{
    // --- Token fetch ---

    [Fact]
    public async Task FetchTokenAsync_SendsBasicAuthAndFormBody()
    {
        const string tokenJson = @"{""access_token"":""tok123"",""expires_in"":7200,""token_type"":""Application Access Token""}";

        // Capture body while the request is still live (FormUrlEncodedContent is
        // disposed when the using-block in FetchTokenAsync exits, so we can't read
        // it from Requests[] after the call returns).
        string capturedBody = "";
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            capturedRequest = req;
            // ByteArrayContent is backed by a MemoryStream — GetResult() won't deadlock here.
#pragma warning disable xUnit1031
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
#pragma warning restore xUnit1031
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(tokenJson, Encoding.UTF8, "application/json"),
            };
        });

        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { EbayClientId = "cid", EbayClientSecret = "csec" });

        var client = new EbayBrowseApiClient(new HttpClient(handler), settings, NullLogger<EbayBrowseApiClient>.Instance);
        var (token, expiry) = await client.FetchTokenAsync(CancellationToken.None);

        Assert.Equal("tok123", token);
        Assert.True(expiry > DateTimeOffset.UtcNow.AddHours(1));

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.NotNull(capturedRequest.Headers.Authorization);
        Assert.Equal("Basic", capturedRequest.Headers.Authorization!.Scheme);

        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("cid:csec"));
        Assert.Equal(expected, capturedRequest.Headers.Authorization.Parameter);

        Assert.Contains("grant_type=client_credentials", capturedBody);
        Assert.Contains("scope=", capturedBody);
    }

    [Fact]
    public async Task FetchTokenAsync_ThrowsWhenCredentialsMissing()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { EbayClientId = "", EbayClientSecret = "" });

        var client = new EbayBrowseApiClient(new HttpClient(handler), settings, NullLogger<EbayBrowseApiClient>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.FetchTokenAsync(CancellationToken.None));
        Assert.Empty(handler.Requests);  // no HTTP call made
    }

    [Fact]
    public async Task FetchTokenAsync_ThrowsOnHttpError()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.Unauthorized, @"{""error"":""invalid_client""}");
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { EbayClientId = "bad", EbayClientSecret = "bad" });

        var client = new EbayBrowseApiClient(new HttpClient(handler), settings, NullLogger<EbayBrowseApiClient>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.FetchTokenAsync(CancellationToken.None));
    }

    // --- Token caching ---

    [Fact]
    public async Task GetOrRefreshTokenAsync_ReusesTokenWithinTtl()
    {
        var callCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    @$"{{""access_token"":""tok{callCount}"",""expires_in"":7200}}",
                    Encoding.UTF8, "application/json"),
            };
        });
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { EbayClientId = "cid", EbayClientSecret = "csec" });

        var client = new EbayBrowseApiClient(new HttpClient(handler), settings, NullLogger<EbayBrowseApiClient>.Instance);

        var t1 = await client.GetOrRefreshTokenAsync(CancellationToken.None);
        var t2 = await client.GetOrRefreshTokenAsync(CancellationToken.None);

        Assert.Equal(t1, t2);         // same token returned
        Assert.Equal(1, callCount);   // only one HTTP call
    }

    // --- Search URL builder ---

    [Theory]
    [InlineData("Justin Jefferson", "215", 20,
        "https://api.ebay.com/buy/browse/v1/item_summary/search?q=Justin%20Jefferson&category_ids=215&limit=20")]
    [InlineData("Mike Trout", "213", 5,
        "https://api.ebay.com/buy/browse/v1/item_summary/search?q=Mike%20Trout&category_ids=213&limit=5")]
    public void BuildSearchUrl_UrlEncodesQueryAndIncludesCategory(
        string query, string cat, int limit, string expectedUrl)
    {
        Assert.Equal(expectedUrl, EbayBrowseApiClient.BuildSearchUrl(query, cat, limit));
    }

    // --- Response parsing ---

    [Fact]
    public void ParseSearchResponse_ExtractsAllFields()
    {
        const string json = @"
{
  ""total"": 1,
  ""itemSummaries"": [
    {
      ""title"": ""2023 Prizm Justin Jefferson Silver"",
      ""price"": { ""value"": ""14.99"", ""currency"": ""USD"" },
      ""condition"": ""Used"",
      ""itemWebUrl"": ""https://www.ebay.com/itm/123"",
      ""buyingOptions"": [""FIXED_PRICE""]
    }
  ]
}";
        var results = EbayBrowseApiClient.ParseSearchResponse(json);

        Assert.Single(results);
        var r = results[0];
        Assert.Equal("2023 Prizm Justin Jefferson Silver", r.Title);
        Assert.Equal(14.99m, r.Price);
        Assert.Equal("USD", r.Currency);
        Assert.Equal("Used", r.Condition);
        Assert.Equal("https://www.ebay.com/itm/123", r.ItemUrl);
        Assert.Equal("FIXED_PRICE", r.BuyingOption);
    }

    [Fact]
    public void ParseSearchResponse_ReturnsEmpty_WhenNoItemSummaries()
    {
        var results = EbayBrowseApiClient.ParseSearchResponse(@"{""total"":0}");
        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResponse_SkipsItemsMissingUrl()
    {
        const string json = @"
{
  ""itemSummaries"": [
    { ""title"": ""Card A"", ""price"": { ""value"": ""10.00"", ""currency"": ""USD"" } },
    { ""title"": ""Card B"", ""itemWebUrl"": ""https://ebay.com/itm/456"", ""price"": { ""value"": ""5.00"", ""currency"": ""USD"" } }
  ]
}";
        var results = EbayBrowseApiClient.ParseSearchResponse(json);

        Assert.Single(results);
        Assert.Equal("Card B", results[0].Title);
    }

    [Fact]
    public void ParseSearchResponse_HandlesMultipleBuyingOptions_TakesFirst()
    {
        const string json = @"
{
  ""itemSummaries"": [{
    ""title"": ""T"",
    ""itemWebUrl"": ""https://ebay.com/itm/1"",
    ""price"": {""value"":""9.99"",""currency"":""USD""},
    ""buyingOptions"": [""AUCTION"",""BEST_OFFER""]
  }]
}";
        var results = EbayBrowseApiClient.ParseSearchResponse(json);
        Assert.Equal("AUCTION", results[0].BuyingOption);
    }
}
