using System.Text;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Core.Tests.Services;

public class EbayListingImportServiceTests
{
    private const string Header =
        "Item number,Title,Variation details,Custom label (SKU),Available quantity,Format,Currency,Start price,Auction Buy It Now price,Reserve price,Current price,Sold quantity,Watchers,Bids,Start date,End date,eBay category 1 name,eBay category 1 number,eBay category 2 name,eBay category 2 number,Condition,CD:Professional Grader - (ID: 27501),CD:Grade - (ID: 27502),CDA:Certification Number - (ID: 27503),CD:Card Condition - (ID: 40001),eBay Product ID(ePID),Listing site,P:UPC,P:EAN,P:ISBN";

    private static Stream Csv(params string[] lines)
        => new MemoryStream(Encoding.UTF8.GetBytes(Header + "\n" + string.Join("\n", lines)));

    private static EbayListingImportService Build(
        IEbayTitleEnricher? enricher = null,
        ICardRepository? repo = null)
    {
        if (enricher is null)
        {
            // Default enricher: returns N empty enrichments matching the input list size.
            // Only configured when the caller didn't supply their own; otherwise we'd
            // overwrite their .Returns() setup.
            enricher = Substitute.For<IEbayTitleEnricher>();
            enricher.EnrichAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var titles = call.Arg<IReadOnlyList<string>>();
                    return Task.FromResult<IReadOnlyList<EbayTitleEnrichment>>(
                        titles.Select(_ => new EbayTitleEnrichment(null, null, null, null, null)).ToList());
                });
        }

        repo ??= Substitute.For<ICardRepository>();
        return new EbayListingImportService(enricher, repo, NullLogger<EbayListingImportService>.Instance);
    }

    [Fact]
    public async Task Parse_ReturnsEmptyPreview_When_CsvHasNoRows()
    {
        var svc = Build();
        var preview = await svc.ParseAsync(Csv(), "empty.csv");
        Assert.Empty(preview.Rows);
        Assert.Single(preview.Warnings);
    }

    [Fact]
    public async Task Parse_RunsRulePass_AndPopulatesYearAndManufacturer()
    {
        var line = "\"1\",2025 Panini Select Jonathan Taylor #132,,,\"1\",\"FIXED_PRICE\",\"USD\",75.0,,,75.0,\"0\",,,Apr-29-26 17:04:25 PDT,May-29-26 17:04:25 PDT,,,,,Ungraded,,,,,,,,,";
        var svc = Build();

        var preview = await svc.ParseAsync(Csv(line), "in.csv");

        var row = Assert.Single(preview.Rows);
        Assert.Equal(2025, row.ParsedTitle.Year);
        Assert.Equal("Panini", row.ParsedTitle.Manufacturer);
        Assert.Equal("132", row.ParsedTitle.CardNumber);
        Assert.Equal(2025, row.ProposedCard.Year);
        Assert.Equal("Panini", row.ProposedCard.Manufacturer);
    }

    [Fact]
    public async Task Parse_AppliesEnrichment_ToProposedCard()
    {
        var line = "\"1\",Some title,,,\"1\",\"FIXED_PRICE\",\"USD\",10.0,,,10.0,\"0\",,,Apr-29-26 17:04:25 PDT,May-29-26 17:04:25 PDT,,,,,Ungraded,,,,,,,,,";
        var enricher = Substitute.For<IEbayTitleEnricher>();
        enricher.EnrichAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<EbayTitleEnrichment>>(
                new[] { new EbayTitleEnrichment("Patrick Mahomes", "Prizm", "Premier Level", "Silver", "Chiefs") }));
        var svc = Build(enricher: enricher);

        var preview = await svc.ParseAsync(Csv(line), "in.csv");

        var row = Assert.Single(preview.Rows);
        Assert.Equal("Patrick Mahomes", row.ProposedCard.PlayerName);
        Assert.Equal("Prizm", row.ProposedCard.Brand);
        Assert.Equal("Premier Level", row.ProposedCard.SetName);
        Assert.Equal("Silver", row.ProposedCard.ParallelName);
        Assert.Equal("Chiefs", row.ProposedCard.Team);
    }

    [Fact]
    public async Task Parse_FlagsExistingMatch_When_RepositoryFindsByEbayItemId()
    {
        var line = "\"42\",Mahomes Prizm,,,\"1\",\"FIXED_PRICE\",\"USD\",10.0,,,10.0,\"0\",,,Apr-29-26 17:04:25 PDT,May-29-26 17:04:25 PDT,,,,,Ungraded,,,,,,,,,";
        var repo = Substitute.For<ICardRepository>();
        var existing = new Card { Id = 7, EbayItemId = "42", PlayerName = "Patrick Mahomes" };
        repo.GetCardByEbayItemIdAsync("42").Returns(existing);
        var svc = Build(repo: repo);

        var preview = await svc.ParseAsync(Csv(line), "in.csv");

        var row = Assert.Single(preview.Rows);
        Assert.True(row.IsExistingMatch);
        Assert.Equal(7, row.ProposedCard.Id);              // mutated existing, not a fresh card
        Assert.Equal("Patrick Mahomes", row.ProposedCard.PlayerName);  // user value preserved
        Assert.Equal(1, preview.UpdateCount);
        Assert.Equal(0, preview.InsertCount);
    }

    [Fact]
    public async Task Parse_DoesNot_OverwriteUserPlayerName_With_NullEnrichment()
    {
        var line = "\"5\",Some Title,,,\"1\",\"FIXED_PRICE\",\"USD\",10.0,,,10.0,\"0\",,,Apr-29-26 17:04:25 PDT,May-29-26 17:04:25 PDT,,,,,Ungraded,,,,,,,,,";
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardByEbayItemIdAsync("5").Returns(new Card { Id = 99, EbayItemId = "5", PlayerName = "Existing User Value" });
        var svc = Build(repo: repo);

        var preview = await svc.ParseAsync(Csv(line), "in.csv");

        Assert.Equal("Existing User Value", preview.Rows[0].ProposedCard.PlayerName);
    }

    [Fact]
    public async Task Parse_PullsListingPrice_FromCurrentPriceColumn()
    {
        var line = "\"7\",Some Card,,,\"3\",\"FIXED_PRICE\",\"USD\",100.0,,,75.0,\"0\",,,Apr-29-26 17:04:25 PDT,May-29-26 17:04:25 PDT,,,,,Ungraded,,,,,,,,,";
        var svc = Build();

        var preview = await svc.ParseAsync(Csv(line), "in.csv");

        var row = Assert.Single(preview.Rows);
        Assert.Equal(75.0m, row.ProposedCard.ListingPrice);  // current price wins over start price
        Assert.Equal(3, row.ProposedCard.Quantity);
    }

    [Fact]
    public async Task Parse_StampsSport_From_TitleHeuristic()
    {
        // "NFL Gear" in the title triggers the Football inference.
        var line = "\"123\",Blake Corum RC 2024 National Treasures NFL Gear Dual Rookie Patch Auto 02/25,,,\"1\",\"FIXED_PRICE\",\"USD\",65.0,,,65.0,\"0\",,,Apr-29-26 17:02:34 PDT,May-29-26 17:02:34 PDT,,,,,Ungraded,,,,,,,,,";
        var svc = Build();

        var preview = await svc.ParseAsync(Csv(line), "in.csv");

        Assert.Equal(Sport.Football, preview.Rows[0].ProposedCard.Sport);
    }

    [Fact]
    public async Task Parse_PreservesUserSetSport_OnUpdate()
    {
        // Existing card has Basketball — re-import title says Football. User wins.
        var line = "\"4\",Some NFL Card,,,\"1\",\"FIXED_PRICE\",\"USD\",10.0,,,10.0,\"0\",,,Apr-29-26 17:04:25 PDT,May-29-26 17:04:25 PDT,,,,,Ungraded,,,,,,,,,";
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardByEbayItemIdAsync("4")
            .Returns(new Card { Id = 33, EbayItemId = "4", Sport = Sport.Basketball });
        var svc = Build(repo: repo);

        var preview = await svc.ParseAsync(Csv(line), "in.csv");

        Assert.Equal(Sport.Basketball, preview.Rows[0].ProposedCard.Sport);
    }

    [Fact]
    public async Task Parse_PullsGradingFields_From_EbayCustomColumns()
    {
        var line = "\"8\",PSA 10 Mahomes,,,\"1\",\"FIXED_PRICE\",\"USD\",500.0,,,500.0,\"0\",,,Apr-29-26 17:02:34 PDT,May-29-26 17:02:34 PDT,,,,,Graded,PSA,10,12345678,,,,,,";
        var svc = Build();

        var preview = await svc.ParseAsync(Csv(line), "in.csv");

        var row = Assert.Single(preview.Rows);
        Assert.True(row.ProposedCard.IsGraded);
        Assert.Equal("PSA", row.ProposedCard.GradeCompany);
        Assert.Equal("10", row.ProposedCard.GradeValue);
        Assert.Equal("12345678", row.ProposedCard.CertNumber);
    }

    [Fact]
    public async Task Parse_AddsWarning_When_EnricherThrows()
    {
        var line = "\"1\",Some Card,,,\"1\",\"FIXED_PRICE\",\"USD\",10.0,,,10.0,\"0\",,,Apr-29-26 17:04:25 PDT,May-29-26 17:04:25 PDT,,,,,Ungraded,,,,,,,,,";
        var enricher = Substitute.For<IEbayTitleEnricher>();
        enricher.EnrichAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<EbayTitleEnrichment>>(_ => throw new InvalidOperationException("API key missing"));
        var svc = Build(enricher: enricher);

        var preview = await svc.ParseAsync(Csv(line), "in.csv");

        Assert.Single(preview.Rows);  // row still produced from rule pass
        Assert.NotEmpty(preview.Warnings);
        Assert.Contains("API key missing", preview.Warnings[0]);
    }

    [Fact]
    public async Task Commit_InsertsNewRows_AndUpdatesExisting()
    {
        var lines = new[]
        {
            "\"new1\",2025 Panini Prizm Mahomes,,,\"1\",\"FIXED_PRICE\",\"USD\",10.0,,,10.0,\"0\",,,Apr-29-26 17:04:25 PDT,May-29-26 17:04:25 PDT,,,,,Ungraded,,,,,,,,,",
            "\"existing1\",2024 Bowman Chrome Holiday,,,\"1\",\"FIXED_PRICE\",\"USD\",20.0,,,20.0,\"0\",,,Apr-29-26 17:04:25 PDT,May-29-26 17:04:25 PDT,,,,,Ungraded,,,,,,,,,",
        };
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardByEbayItemIdAsync("existing1")
            .Returns(new Card { Id = 11, EbayItemId = "existing1", PlayerName = "X" });
        var svc = Build(repo: repo);

        var preview = await svc.ParseAsync(Csv(lines), "in.csv");
        var result = await svc.CommitAsync(preview);

        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Skipped);
        Assert.Empty(result.Errors);
        await repo.Received(1).InsertCardAsync(Arg.Is<Card>(c => c.EbayItemId == "new1"));
        await repo.Received(1).UpdateCardAsync(Arg.Is<Card>(c => c.Id == 11));
    }

    [Fact]
    public async Task Commit_RespectsSkipFlag()
    {
        var line = "\"99\",Some Card,,,\"1\",\"FIXED_PRICE\",\"USD\",10.0,,,10.0,\"0\",,,Apr-29-26 17:04:25 PDT,May-29-26 17:04:25 PDT,,,,,Ungraded,,,,,,,,,";
        var repo = Substitute.For<ICardRepository>();
        var svc = Build(repo: repo);
        var preview = await svc.ParseAsync(Csv(line), "in.csv");
        preview.Rows[0].Skip = true;

        var result = await svc.CommitAsync(preview);

        Assert.Equal(0, result.Inserted);
        Assert.Equal(0, result.Updated);
        Assert.Equal(1, result.Skipped);
        await repo.DidNotReceive().InsertCardAsync(Arg.Any<Card>());
    }

    [Fact]
    public async Task Commit_SetsNewRowsTo_Listed()
    {
        var line = "\"77\",2025 Panini Prizm Mahomes,,,\"1\",\"FIXED_PRICE\",\"USD\",10.0,,,10.0,\"0\",,,Apr-29-26 17:04:25 PDT,May-29-26 17:04:25 PDT,,,,,Ungraded,,,,,,,,,";
        var repo = Substitute.For<ICardRepository>();
        Card? captured = null;
        repo.InsertCardAsync(Arg.Do<Card>(c => captured = c)).Returns(1);
        var svc = Build(repo: repo);
        var preview = await svc.ParseAsync(Csv(line), "in.csv");

        await svc.CommitAsync(preview);

        Assert.NotNull(captured);
        Assert.Equal(CardStatus.Listed, captured!.Status);
    }
}
