using System.Net;
using System.Text.Json;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlipKit.Core.Tests.Services;

public class ApiCardRepositoryTests
{
    private const string BaseUrl = "http://localhost:5001";

    private static ApiCardRepository CreateRepo(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler), BaseUrl, NullLogger<ApiCardRepository>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    // === Insert ===

    [Fact]
    public async Task Should_PostToApiCardsEndpoint_When_InsertingCard()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, @"{""id"":42}"));
        var repo = CreateRepo(handler);

        var id = await repo.InsertCardAsync(new Card { PlayerName = "Mike Trout" });

        Assert.Equal(42, id);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal($"{BaseUrl}/api/cards", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task Should_Throw_When_InsertReturnsErrorStatus()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.InternalServerError, ""));
        var repo = CreateRepo(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => repo.InsertCardAsync(new Card { PlayerName = "X" }));
    }

    // === Update ===

    [Fact]
    public async Task Should_PutToCardIdEndpoint_When_UpdatingCard()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, ""));
        var repo = CreateRepo(handler);

        await repo.UpdateCardAsync(new Card { Id = 7, PlayerName = "Aaron Judge" });

        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.Equal($"{BaseUrl}/api/cards/7", handler.Requests[0].RequestUri!.ToString());
    }

    // === Get single ===

    [Fact]
    public async Task Should_DeserializeCard_When_GettingById()
    {
        var card = new Card { Id = 5, PlayerName = "Mike Trout", Year = 2026 };
        var body = JsonSerializer.Serialize(card);
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, body));
        var repo = CreateRepo(handler);

        var result = await repo.GetCardAsync(5);

        Assert.NotNull(result);
        Assert.Equal("Mike Trout", result!.PlayerName);
        Assert.Equal(2026, result.Year);
    }

    [Fact]
    public async Task Should_ReturnNull_When_GetByIdReturns404()
    {
        // The repo treats 404 as "not found" — returns null without throwing.
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.NotFound, ""));
        var repo = CreateRepo(handler);

        var result = await repo.GetCardAsync(99);

        Assert.Null(result);
    }

    // === Get all + filters ===

    [Fact]
    public async Task Should_HitCardsEndpointWithoutFilters_When_GettingAllNoFilters()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, "[]"));
        var repo = CreateRepo(handler);

        await repo.GetAllCardsAsync();

        Assert.Equal($"{BaseUrl}/api/cards", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task Should_AppendStatusAndSportQueryParams_When_FiltersProvided()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, "[]"));
        var repo = CreateRepo(handler);

        await repo.GetAllCardsAsync(status: CardStatus.Listed, sport: Sport.Baseball);

        var url = handler.Requests[0].RequestUri!.ToString();
        Assert.Contains("status=Listed", url);
        Assert.Contains("sport=Baseball", url);
    }

    [Fact]
    public async Task Should_ReturnEmptyList_When_ApiReturnsNullBody()
    {
        // GetFromJsonAsync returns null for JSON `null`; repo defaults to empty list.
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, "null"));
        var repo = CreateRepo(handler);

        var result = await repo.GetAllCardsAsync();

        Assert.Empty(result);
    }

    // === Delete ===

    [Fact]
    public async Task Should_DeleteCardIdEndpoint_When_Deleting()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.NoContent, ""));
        var repo = CreateRepo(handler);

        await repo.DeleteCardAsync(11);

        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal($"{BaseUrl}/api/cards/11", handler.Requests[0].RequestUri!.ToString());
    }

    // === Specialized queries ===

    [Fact]
    public async Task Should_HitUnpricedEndpoint_When_GettingUnpriced()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, "[]"));
        var repo = CreateRepo(handler);

        await repo.GetUnpricedCardsAsync();

        Assert.Equal($"{BaseUrl}/api/cards/unpriced", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task Should_HitStaleEndpoint_When_GettingStale()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, "[]"));
        var repo = CreateRepo(handler);

        await repo.GetStaleCardsAsync(thresholdDays: 30);

        // Note: server doesn't currently take threshold as a parameter — Api just returns
        // its own staleness window. That's an inconsistency worth noting in Phase 5.
        Assert.Equal($"{BaseUrl}/api/cards/stale", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task Should_HitReportsSoldEndpointWithDateRange_When_GettingSoldCards()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, @"{""cards"":[]}"));
        var repo = CreateRepo(handler);

        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 6, 1);
        await repo.GetSoldCardsAsync(startDate: start, endDate: end);

        var url = handler.Requests[0].RequestUri!.ToString();
        Assert.StartsWith($"{BaseUrl}/api/reports/sold", url);
        Assert.Contains("startDate=", url);
        Assert.Contains("endDate=", url);
    }

    [Fact]
    public async Task Should_UrlEscapeSearchQuery_When_Searching()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, "[]"));
        var repo = CreateRepo(handler);

        await repo.SearchCardsAsync("Mike Trout");

        // Use AbsoluteUri to preserve percent-encoding; .ToString() may normalize/decode.
        Assert.Contains("search=Mike%20Trout", handler.Requests[0].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Should_PostPriceHistoryToCardSubResource_When_AddingHistory()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, ""));
        var repo = CreateRepo(handler);

        await repo.AddPriceHistoryAsync(new PriceHistory { CardId = 13, ListingPrice = 22m });

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal($"{BaseUrl}/api/cards/13/price-history", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task Should_DeserializeStatsResponse_When_GettingCardCount()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, @"{""totalCards"":1234}"));
        var repo = CreateRepo(handler);

        var count = await repo.GetCardCountAsync();

        Assert.Equal(1234, count);
    }
}
