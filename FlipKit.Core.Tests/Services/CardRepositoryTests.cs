using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FlipKit.Core.Tests.Services;

public class CardRepositoryTests
{
    private static Card SampleCard(string player = "Mike Trout") => new()
    {
        PlayerName = player,
        Year = 2026,
        Brand = "Bowman",
        Team = "Angels",
        Manufacturer = "Topps",
        Sport = Sport.Baseball,
    };

    [Fact]
    public async Task Should_AssignIdAndStampTimestamps_When_InsertingCard()
    {
        using var db = TestDbContext.Create();
        var repo = new CardRepository(db.Context);
        var before = DateTime.UtcNow.AddSeconds(-1);

        var id = await repo.InsertCardAsync(SampleCard());

        Assert.True(id > 0);
        var saved = await db.Context.Cards.FindAsync(id);
        Assert.NotNull(saved);
        Assert.True(saved!.CreatedAt >= before);
        Assert.True(saved.UpdatedAt >= before);
    }

    [Fact]
    public async Task Should_BumpUpdatedAt_When_UpdatingCard()
    {
        using var db = TestDbContext.Create();
        var repo = new CardRepository(db.Context);
        var id = await repo.InsertCardAsync(SampleCard());
        var card = await db.Context.Cards.FindAsync(id);
        var originalUpdated = card!.UpdatedAt;
        await Task.Delay(10); // ensure UtcNow advances past UpdatedAt

        card.PlayerName = "Updated Name";
        await repo.UpdateCardAsync(card);

        var refreshed = await db.Context.Cards.AsNoTracking().FirstAsync(c => c.Id == id);
        Assert.True(refreshed.UpdatedAt > originalUpdated);
        Assert.Equal("Updated Name", refreshed.PlayerName);
    }

    [Fact]
    public async Task Should_DetachExistingTrackedEntity_When_UpdatingCard()
    {
        // The repository explicitly detaches any already-tracked instance before calling
        // Update — guards against the EF tracking conflict that the comment at line 34-41
        // calls out. Here we exercise that by updating an instance that's still tracked
        // from the initial Find.
        using var db = TestDbContext.Create();
        var repo = new CardRepository(db.Context);
        var id = await repo.InsertCardAsync(SampleCard());
        var tracked = await db.Context.Cards.FindAsync(id);

        var detached = SampleCard("Detached Copy");
        detached.Id = id;
        await repo.UpdateCardAsync(detached);

        var refreshed = await db.Context.Cards.AsNoTracking().FirstAsync(c => c.Id == id);
        Assert.Equal("Detached Copy", refreshed.PlayerName);
    }

    [Fact]
    public async Task Should_IncludePriceHistory_When_GettingCardById()
    {
        using var db = TestDbContext.Create();
        var card = SampleCard();
        card.PriceHistories.Add(new PriceHistory { ListingPrice =5m });
        card.PriceHistories.Add(new PriceHistory { ListingPrice =7m });
        db.Context.Cards.Add(card);
        await db.Context.SaveChangesAsync();

        var repo = new CardRepository(db.Context);
        var fetched = await repo.GetCardAsync(card.Id);

        Assert.NotNull(fetched);
        Assert.Equal(2, fetched!.PriceHistories.Count);
    }

    [Fact]
    public async Task Should_ReturnNull_When_GettingNonexistentCard()
    {
        using var db = TestDbContext.Create();
        var repo = new CardRepository(db.Context);

        var card = await repo.GetCardAsync(99999);

        Assert.Null(card);
    }

    [Fact]
    public async Task Should_OrderByCreatedAtDescending_When_GettingAllCards()
    {
        using var db = TestDbContext.Create();
        var repo = new CardRepository(db.Context);
        await repo.InsertCardAsync(SampleCard("First"));
        await Task.Delay(10);
        await repo.InsertCardAsync(SampleCard("Second"));

        var all = await repo.GetAllCardsAsync();

        // Newest first.
        Assert.Equal("Second", all[0].PlayerName);
        Assert.Equal("First", all[1].PlayerName);
    }

    [Fact]
    public async Task Should_FilterByStatus_When_StatusProvided()
    {
        using var db = TestDbContext.Create();
        var repo = new CardRepository(db.Context);
        var draft = SampleCard("Draft Player"); draft.Status = CardStatus.Draft;
        var listed = SampleCard("Listed Player"); listed.Status = CardStatus.Listed;
        await repo.InsertCardAsync(draft);
        await repo.InsertCardAsync(listed);

        var listedOnly = await repo.GetAllCardsAsync(status: CardStatus.Listed);

        Assert.Single(listedOnly);
        Assert.Equal("Listed Player", listedOnly[0].PlayerName);
    }

    [Fact]
    public async Task Should_FilterBySport_When_SportProvided()
    {
        using var db = TestDbContext.Create();
        var repo = new CardRepository(db.Context);
        var bb = SampleCard("Baseball Player"); bb.Sport = Sport.Baseball;
        var fb = SampleCard("Football Player"); fb.Sport = Sport.Football;
        await repo.InsertCardAsync(bb);
        await repo.InsertCardAsync(fb);

        var fbOnly = await repo.GetAllCardsAsync(sport: Sport.Football);

        Assert.Single(fbOnly);
        Assert.Equal("Football Player", fbOnly[0].PlayerName);
    }

    [Fact]
    public async Task Should_RemoveCard_When_Deleting()
    {
        using var db = TestDbContext.Create();
        var repo = new CardRepository(db.Context);
        var id = await repo.InsertCardAsync(SampleCard());

        await repo.DeleteCardAsync(id);

        Assert.Null(await db.Context.Cards.FindAsync(id));
    }

    [Fact]
    public async Task Should_DoNothing_When_DeletingNonexistentCard()
    {
        // Delete is a no-op for missing IDs (no exception) — used by UI confirm-and-delete
        // flows where the row may have already been removed in a parallel session.
        using var db = TestDbContext.Create();
        var repo = new CardRepository(db.Context);

        await repo.DeleteCardAsync(99999);

        Assert.Equal(0, await repo.GetCardCountAsync());
    }

    [Fact]
    public async Task Should_MatchAcrossPlayerBrandTeamManufacturer_When_Searching()
    {
        using var db = TestDbContext.Create();
        var repo = new CardRepository(db.Context);
        await repo.InsertCardAsync(SampleCard("Mike Trout"));
        await repo.InsertCardAsync(new Card { PlayerName = "Aaron Judge", Brand = "Topps Chrome", Year = 2026, Manufacturer = "Topps", Team = "Yankees" });

        var byTeam = await repo.SearchCardsAsync("Yankees");
        var byBrand = await repo.SearchCardsAsync("chrome");
        var byPlayer = await repo.SearchCardsAsync("trout");

        Assert.Single(byTeam);
        Assert.Single(byBrand);
        Assert.Single(byPlayer);
    }

    [Fact]
    public async Task Should_OnlyReturnPricedCardsPastThreshold_When_GettingStale()
    {
        using var db = TestDbContext.Create();
        var repo = new CardRepository(db.Context);
        var stale = SampleCard("Stale"); stale.Status = CardStatus.Ready; stale.PriceDate = DateTime.UtcNow.AddDays(-45);
        var fresh = SampleCard("Fresh"); fresh.Status = CardStatus.Ready; fresh.PriceDate = DateTime.UtcNow.AddDays(-5);
        var sold = SampleCard("Sold"); sold.Status = CardStatus.Sold; sold.PriceDate = DateTime.UtcNow.AddDays(-90); // excluded
        var draft = SampleCard("Draft"); draft.Status = CardStatus.Draft; draft.PriceDate = DateTime.UtcNow.AddDays(-90); // excluded
        var noPrice = SampleCard("NoPrice"); noPrice.Status = CardStatus.Ready; noPrice.PriceDate = null; // excluded
        foreach (var c in new[] { stale, fresh, sold, draft, noPrice })
            await repo.InsertCardAsync(c);

        var result = await repo.GetStaleCardsAsync(thresholdDays: 30);

        Assert.Single(result);
        Assert.Equal("Stale", result[0].PlayerName);
    }

    [Fact]
    public async Task Should_StampRecordedAt_When_AddingPriceHistory()
    {
        using var db = TestDbContext.Create();
        var repo = new CardRepository(db.Context);
        var id = await repo.InsertCardAsync(SampleCard());
        var before = DateTime.UtcNow.AddSeconds(-1);

        await repo.AddPriceHistoryAsync(new PriceHistory { CardId = id, ListingPrice = 12m });

        var saved = await db.Context.PriceHistories.FirstAsync();
        Assert.True(saved.RecordedAt >= before);
        Assert.Equal(12m, saved.ListingPrice);
    }

    [Fact]
    public async Task Should_ReturnTotalCount_When_GettingCardCount()
    {
        using var db = TestDbContext.Create();
        var repo = new CardRepository(db.Context);
        await repo.InsertCardAsync(SampleCard("A"));
        await repo.InsertCardAsync(SampleCard("B"));
        await repo.InsertCardAsync(SampleCard("C"));

        Assert.Equal(3, await repo.GetCardCountAsync());
    }
}
