using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Export;
using FlipKit.Core.Tests.Infrastructure;
using NSubstitute;

namespace FlipKit.Core.Tests.Services;

public class SkuGeneratorTests
{
    private static ISettingsService SettingsWith(string prefix = "FK-", int padWidth = 6)
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { SkuPrefix = prefix, SkuPadWidth = padWidth });
        return settings;
    }

    // GenerateNextSkuAsync: MAX(numeric_suffix) + 1, formatted with prefix + padding.

    [Fact]
    public async Task Should_GenerateFirstSku_When_DatabaseIsEmpty()
    {
        using var db = TestDbContext.Create();
        var gen = new SkuGenerator(db.Context, SettingsWith());

        var sku = await gen.GenerateNextSkuAsync();

        Assert.Equal("FK-000001", sku);
    }

    [Fact]
    public async Task Should_IncrementMaxExistingSku_When_CardsExist()
    {
        using var db = TestDbContext.Create();
        db.Context.Cards.Add(new Card { PlayerName = "A", Sku = "FK-000003" });
        db.Context.Cards.Add(new Card { PlayerName = "B", Sku = "FK-000007" });
        db.Context.Cards.Add(new Card { PlayerName = "C", Sku = "FK-000005" });
        await db.Context.SaveChangesAsync();

        var gen = new SkuGenerator(db.Context, SettingsWith());
        var sku = await gen.GenerateNextSkuAsync();

        // Max is 7, next is 8 — even though "C" inserted last.
        Assert.Equal("FK-000008", sku);
    }

    [Fact]
    public async Task Should_NotReuseDeletedSkus_When_GeneratingNext()
    {
        // Use MAX (not COUNT) so that deleted SKUs are never reused — preserves
        // the invariant even when a card with a higher SKU was deleted.
        using var db = TestDbContext.Create();
        db.Context.Cards.Add(new Card { PlayerName = "A", Sku = "FK-000010" });
        await db.Context.SaveChangesAsync();
        db.Context.Cards.RemoveRange(db.Context.Cards);
        await db.Context.SaveChangesAsync();
        // ... but a different card had used 5 before deletion of 10
        db.Context.Cards.Add(new Card { PlayerName = "Survivor", Sku = "FK-000005" });
        await db.Context.SaveChangesAsync();

        var gen = new SkuGenerator(db.Context, SettingsWith());
        var sku = await gen.GenerateNextSkuAsync();

        Assert.Equal("FK-000006", sku);
    }

    [Fact]
    public async Task Should_IgnoreNonNumericSkuSuffixes_When_FindingMax()
    {
        // Custom-format SKUs (manually entered) shouldn't break the auto-generator.
        using var db = TestDbContext.Create();
        db.Context.Cards.Add(new Card { PlayerName = "A", Sku = "FK-CUSTOM-LABEL" });
        db.Context.Cards.Add(new Card { PlayerName = "B", Sku = "FK-000003" });
        await db.Context.SaveChangesAsync();

        var gen = new SkuGenerator(db.Context, SettingsWith());
        var sku = await gen.GenerateNextSkuAsync();

        Assert.Equal("FK-000004", sku);
    }

    [Fact]
    public async Task Should_IgnoreCardsWithDifferentPrefix_When_FindingMax()
    {
        // Switching prefixes mid-stream shouldn't see the old prefix's SKUs.
        using var db = TestDbContext.Create();
        db.Context.Cards.Add(new Card { PlayerName = "A", Sku = "OLD-000099" });
        await db.Context.SaveChangesAsync();

        var gen = new SkuGenerator(db.Context, SettingsWith(prefix: "FK-"));
        var sku = await gen.GenerateNextSkuAsync();

        Assert.Equal("FK-000001", sku);
    }

    [Fact]
    public async Task Should_IgnoreCardsWithNullSku_When_FindingMax()
    {
        using var db = TestDbContext.Create();
        db.Context.Cards.Add(new Card { PlayerName = "A", Sku = null });
        await db.Context.SaveChangesAsync();

        var gen = new SkuGenerator(db.Context, SettingsWith());
        var sku = await gen.GenerateNextSkuAsync();

        Assert.Equal("FK-000001", sku);
    }

    [Fact]
    public async Task Should_HonorCustomPrefixAndPadWidth_When_SettingsOverrideDefaults()
    {
        using var db = TestDbContext.Create();
        var gen = new SkuGenerator(db.Context, SettingsWith(prefix: "ABC-", padWidth: 4));

        var sku = await gen.GenerateNextSkuAsync();

        Assert.Equal("ABC-0001", sku);
    }

    [Fact]
    public async Task Should_FallBackToDefaults_When_SettingsHasNullPrefixOrZeroPad()
    {
        using var db = TestDbContext.Create();
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { SkuPrefix = null!, SkuPadWidth = 0 });

        var gen = new SkuGenerator(db.Context, settings);
        var sku = await gen.GenerateNextSkuAsync();

        // Defaults: prefix "FK-", padWidth 6.
        Assert.Equal("FK-000001", sku);
    }

    // IsSkuAvailableAsync: true when no other card claims the SKU.

    [Fact]
    public async Task Should_ReturnTrue_When_SkuIsUnused()
    {
        using var db = TestDbContext.Create();
        var gen = new SkuGenerator(db.Context, SettingsWith());

        var available = await gen.IsSkuAvailableAsync("FK-000999");

        Assert.True(available);
    }

    [Fact]
    public async Task Should_ReturnFalse_When_SkuIsAlreadyClaimed()
    {
        using var db = TestDbContext.Create();
        db.Context.Cards.Add(new Card { PlayerName = "A", Sku = "FK-000005" });
        await db.Context.SaveChangesAsync();

        var gen = new SkuGenerator(db.Context, SettingsWith());
        var available = await gen.IsSkuAvailableAsync("FK-000005");

        Assert.False(available);
    }

    [Fact]
    public async Task Should_ReturnTrue_When_SkuIsClaimedByExcludedCard()
    {
        // Edit flow: when the user is editing card 5, the card's own SKU shouldn't
        // count as "in use" — otherwise saving without changing the SKU would fail.
        using var db = TestDbContext.Create();
        var card = new Card { PlayerName = "A", Sku = "FK-000005" };
        db.Context.Cards.Add(card);
        await db.Context.SaveChangesAsync();

        var gen = new SkuGenerator(db.Context, SettingsWith());
        var available = await gen.IsSkuAvailableAsync("FK-000005", excludeCardId: card.Id);

        Assert.True(available);
    }

    [Fact]
    public async Task Should_ReturnFalse_When_SkuIsNullOrWhitespace()
    {
        using var db = TestDbContext.Create();
        var gen = new SkuGenerator(db.Context, SettingsWith());

        Assert.False(await gen.IsSkuAvailableAsync(""));
        Assert.False(await gen.IsSkuAvailableAsync("   "));
        Assert.False(await gen.IsSkuAvailableAsync(null!));
    }
}
