using FlipKit.Core.Data;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Get database path from environment or use default
var dbPath = Environment.GetEnvironmentVariable("CARDLISTER_DB_PATH")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlipKit", "cards.db");

// Add FlipKit.Core services (API only provides data access)
builder.Services.AddDbContext<FlipKitDbContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
});
builder.Services.AddScoped<ICardRepository, CardRepository>();

// Add CORS for local network access
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Enable CORS
app.UseCors();

// ============================================================================
// HEALTH CHECK / ROOT ENDPOINT
// ============================================================================

app.MapGet("/", () => Results.Ok(new
{
    Service = "FlipKit API",
    Version = "1.0",
    Status = "Running",
    Message = "API is healthy and ready to serve requests",
    Endpoints = new
    {
        Cards = "/api/cards",
        CardById = "/api/cards/{id}",
        UnpricedCards = "/api/cards/unpriced",
        StaleCards = "/api/cards/stale",
        CardStats = "/api/cards/stats",
        PriceHistory = "/api/cards/{id}/price-history",
        SoldCardsReport = "/api/reports/sold"
    }
}))
.WithName("HealthCheck")
.WithOpenApi();

// ============================================================================
// CARD CRUD ENDPOINTS
// ============================================================================

app.MapGet("/api/cards", async (
    ICardRepository repo,
    CardStatus? status,
    Sport? sport,
    int? limit,
    int? offset) =>
{
    var cards = await repo.GetAllCardsAsync(status, sport);

    // Apply pagination
    if (offset.HasValue)
        cards = cards.Skip(offset.Value).ToList();
    if (limit.HasValue)
        cards = cards.Take(limit.Value).ToList();

    return Results.Ok(cards);
})
.WithName("GetAllCards")
.WithOpenApi();

app.MapGet("/api/cards/{id:int}", async (int id, ICardRepository repo) =>
{
    var card = await repo.GetCardAsync(id);
    return card is not null ? Results.Ok(card) : Results.NotFound();
})
.WithName("GetCard")
.WithOpenApi();

app.MapPost("/api/cards", async (Card card, ICardRepository repo) =>
{
    var id = await repo.InsertCardAsync(card);
    card.Id = id;
    return Results.Created($"/api/cards/{id}", card);
})
.WithName("CreateCard")
.WithOpenApi();

app.MapPut("/api/cards/{id:int}", async (int id, Card card, ICardRepository repo) =>
{
    var existing = await repo.GetCardAsync(id);
    if (existing is null)
        return Results.NotFound();

    card.Id = id; // Ensure ID matches route
    await repo.UpdateCardAsync(card);
    return Results.Ok(card);
})
.WithName("UpdateCard")
.WithOpenApi();

app.MapDelete("/api/cards/{id:int}", async (int id, ICardRepository repo) =>
{
    var existing = await repo.GetCardAsync(id);
    if (existing is null)
        return Results.NotFound();

    await repo.DeleteCardAsync(id);
    return Results.NoContent();
})
.WithName("DeleteCard")
.WithOpenApi();

// ============================================================================
// SPECIALIZED CARD QUERIES
// ============================================================================

app.MapGet("/api/cards/unpriced", async (ICardRepository repo) =>
{
    var allCards = await repo.GetAllCardsAsync();
    var unpricedCards = allCards.Where(c => !c.ListingPrice.HasValue || c.ListingPrice == 0).ToList();
    return Results.Ok(unpricedCards);
})
.WithName("GetUnpricedCards")
.WithOpenApi();

app.MapGet("/api/cards/stale", async (ICardRepository repo, int? thresholdDays) =>
{
    var threshold = thresholdDays ?? 30; // Default to 30 days
    var cards = await repo.GetStaleCardsAsync(threshold);
    return Results.Ok(cards);
})
.WithName("GetStaleCards")
.WithOpenApi();

app.MapGet("/api/cards/stats", async (ICardRepository repo) =>
{
    var allCards = await repo.GetAllCardsAsync();
    var priced = allCards.Count(c => c.ListingPrice.HasValue && c.ListingPrice > 0);
    var totalValue = allCards.Where(c => c.ListingPrice.HasValue).Sum(c => c.ListingPrice ?? 0);

    return Results.Ok(new
    {
        TotalCards = allCards.Count,
        PricedCards = priced,
        UnpricedCards = allCards.Count - priced,
        TotalValue = totalValue,
        ByStatus = allCards.GroupBy(c => c.Status).Select(g => new { Status = g.Key, Count = g.Count() }),
        BySport = allCards.GroupBy(c => c.Sport).Select(g => new { Sport = g.Key, Count = g.Count() })
    });
})
.WithName("GetCardStats")
.WithOpenApi();

// ============================================================================
// PRICE HISTORY
// ============================================================================

app.MapGet("/api/cards/{id:int}/price-history", async (int id, ICardRepository repo) =>
{
    var card = await repo.GetCardAsync(id);
    if (card is null)
        return Results.NotFound();

    return Results.Ok(card.PriceHistories);
})
.WithName("GetCardPriceHistory")
.WithOpenApi();

app.MapPost("/api/cards/{id:int}/price-history", async (int id, PriceHistory history, ICardRepository repo) =>
{
    var card = await repo.GetCardAsync(id);
    if (card is null)
        return Results.NotFound();

    history.CardId = id; // Ensure CardId matches route
    await repo.AddPriceHistoryAsync(history);
    return Results.Created($"/api/cards/{id}/price-history", history);
})
.WithName("AddPriceHistory")
.WithOpenApi();

// ============================================================================
// REPORTS
// ============================================================================

app.MapGet("/api/reports/sold", async (
    ICardRepository repo,
    DateTime? startDate,
    DateTime? endDate) =>
{
    var allSoldCards = await repo.GetAllCardsAsync(CardStatus.Sold);

    // Filter by date range if provided
    var soldCards = allSoldCards;
    if (startDate.HasValue)
        soldCards = soldCards.Where(c => c.SaleDate >= startDate.Value).ToList();
    if (endDate.HasValue)
        soldCards = soldCards.Where(c => c.SaleDate <= endDate.Value).ToList();

    var totalRevenue = soldCards.Sum(c => c.SalePrice ?? 0);
    var totalCost = soldCards.Sum(c => c.CostBasis ?? 0);
    var netProfit = totalRevenue - totalCost;

    return Results.Ok(new
    {
        Cards = soldCards,
        Summary = new
        {
            TotalCards = soldCards.Count,
            TotalRevenue = totalRevenue,
            TotalCost = totalCost,
            NetProfit = netProfit
        }
    });
})
.WithName("GetSoldCardsReport")
.WithOpenApi();

// ============================================================================
// EXPORT
// ============================================================================
// Note: Export functionality is handled on the client side (Desktop/Web apps)
// The API only provides data access - CSV generation happens locally

// ============================================================================
// LEGACY SYNC ENDPOINTS (for backwards compatibility)
// ============================================================================

app.MapGet("/api/sync/status", async (ICardRepository repo) =>
{
    var lastUpdated = await repo.GetAllCardsAsync();
    var lastUpdatedTime = lastUpdated.Any()
        ? lastUpdated.Max(c => c.UpdatedAt)
        : DateTime.MinValue;

    return Results.Ok(new
    {
        Status = "Ready",
        LastUpdated = lastUpdatedTime,
        CardCount = lastUpdated.Count,
        ServerTime = DateTime.UtcNow
    });
})
.WithName("GetSyncStatus")
.WithOpenApi();

app.MapGet("/api/sync/cards", async (
    ICardRepository repo,
    DateTime? since) =>
{
    var cards = since.HasValue
        ? (await repo.GetAllCardsAsync())
            .Where(c => c.UpdatedAt > since.Value)
            .ToList()
        : await repo.GetAllCardsAsync();

    return Results.Ok(cards);
})
.WithName("GetSyncCards")
.WithOpenApi();

app.MapPost("/api/sync/push", async (
    ICardRepository repo,
    List<Card> cards) =>
{
    var synced = 0;
    var errors = new List<string>();

    foreach (var card in cards)
    {
        try
        {
            var existing = await repo.GetCardAsync(card.Id);
            if (existing != null)
            {
                await repo.UpdateCardAsync(card);
            }
            else
            {
                errors.Add($"Card {card.Id} not found on server - skipping");
                continue;
            }
            synced++;
        }
        catch (Exception ex)
        {
            errors.Add($"Card {card.Id}: {ex.Message}");
        }
    }

    return Results.Ok(new
    {
        Synced = synced,
        Failed = errors.Count,
        Errors = errors
    });
})
.WithName("PushSyncCards")
.WithOpenApi();

Console.WriteLine($"FlipKit API Server");
Console.WriteLine($"Database: {dbPath}");
Console.WriteLine($"Listening on: http://0.0.0.0:5000");
Console.WriteLine($"Access via Tailscale IP on port 5000");
Console.WriteLine($"");
Console.WriteLine($"Endpoints:");
Console.WriteLine($"  GET    /api/cards                - List all cards");
Console.WriteLine($"  GET    /api/cards/{{id}}          - Get single card");
Console.WriteLine($"  POST   /api/cards                - Create card");
Console.WriteLine($"  PUT    /api/cards/{{id}}          - Update card");
Console.WriteLine($"  DELETE /api/cards/{{id}}          - Delete card");
Console.WriteLine($"  GET    /api/cards/unpriced       - Get unpriced cards");
Console.WriteLine($"  GET    /api/cards/stale          - Get stale cards");
Console.WriteLine($"  GET    /api/cards/stats          - Get statistics");
Console.WriteLine($"  GET    /api/reports/sold         - Sold cards report");
Console.WriteLine($"  POST   /api/export/csv           - Export to CSV");

app.Run("http://0.0.0.0:5000");
