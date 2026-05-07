using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations.SurpriseSets;
using NSubstitute;

namespace FlipKit.Core.Tests.Services.SurpriseSet;

public class SurpriseSetCsvExporterTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public SurpriseSetCsvExporterTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string TempCsv() => Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".csv");

    // === helpers ===

    private static Models.SurpriseSet ValidSet(int id = 1, SurpriseSetState state = SurpriseSetState.Draft) => new()
    {
        Id = id,
        Name = "Test Set",
        Title = "My Surprise Set",
        State = state,
        SpotPrice = 10m,
        SharedCondition = "Near Mint",
        SharedShippingProfile = "Standard",
        SharedListingType = "Buy it Now",
        SharedWhatnotCategory = "Sports Trading Cards",
        SharedImageUrl1 = "https://example.com/img1.jpg",
        Offerable = false,
        Cards = new List<Card>
        {
            new() { Id = 101, PlayerName = "Player A", SurpriseSetSlot = 1 },
            new() { Id = 102, PlayerName = "Player B", SurpriseSetSlot = 2 },
        },
    };

    private static SurpriseSetCsvExporter CreateExporter(
        ISurpriseSetRepository? repo = null,
        ISurpriseSetValidator? validator = null,
        ISurpriseSetDescriptionGenerator? descGen = null)
    {
        repo ??= Substitute.For<ISurpriseSetRepository>();

        if (validator == null)
        {
            validator = Substitute.For<ISurpriseSetValidator>();
            validator.Validate(Arg.Any<Models.SurpriseSet>(), Arg.Any<IList<Card>>())
                .Returns(new List<SurpriseSetIssue>());
        }

        if (descGen == null)
        {
            descGen = Substitute.For<ISurpriseSetDescriptionGenerator>();
            descGen.Generate(Arg.Any<Models.SurpriseSet>(), Arg.Any<IList<Card>>())
                .Returns("Test description.");
        }

        return new SurpriseSetCsvExporter(repo, validator, descGen);
    }

    // === set-not-found ===

    [Fact]
    public async Task Should_Throw_When_SetNotFound()
    {
        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(99).Returns((Models.SurpriseSet?)null);

        var exporter = CreateExporter(repo: repo);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => exporter.ExportAsync(99, TempCsv()));
    }

    // === locked state checks ===

    [Theory]
    [InlineData(SurpriseSetState.Live)]
    [InlineData(SurpriseSetState.Completed)]
    [InlineData(SurpriseSetState.Cancelled)]
    public async Task Should_Throw_When_SetIsInTerminalState(SurpriseSetState state)
    {
        var set = ValidSet(state: state);
        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(set.Id).Returns(set);

        var exporter = CreateExporter(repo: repo);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => exporter.ExportAsync(set.Id, TempCsv()));
    }

    // === validator gate ===

    [Fact]
    public async Task Should_ReturnFailure_When_ValidatorHasBlockingErrors()
    {
        var set = ValidSet();
        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(set.Id).Returns(set);

        var validator = Substitute.For<ISurpriseSetValidator>();
        validator.Validate(Arg.Any<Models.SurpriseSet>(), Arg.Any<IList<Card>>())
            .Returns(new List<SurpriseSetIssue>
            {
                new("MISSING_GALLERY", "Gallery image required", IssueSeverity.Error),
            });

        var outputPath = TempCsv();
        var exporter = CreateExporter(repo: repo, validator: validator);
        var result = await exporter.ExportAsync(set.Id, outputPath);

        Assert.False(result.Success);
        Assert.Single(result.BlockingIssues);
        Assert.Equal("MISSING_GALLERY", result.BlockingIssues[0].Code);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task Should_ExportDespiteWarnings_When_NoErrors()
    {
        var set = ValidSet();
        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(set.Id).Returns(set);

        var validator = Substitute.For<ISurpriseSetValidator>();
        validator.Validate(Arg.Any<Models.SurpriseSet>(), Arg.Any<IList<Card>>())
            .Returns(new List<SurpriseSetIssue>
            {
                new("MIXED_SPORT", "Mixed sports detected", IssueSeverity.Warning),
            });

        var descGen = Substitute.For<ISurpriseSetDescriptionGenerator>();
        descGen.Generate(Arg.Any<Models.SurpriseSet>(), Arg.Any<IList<Card>>()).Returns("desc");

        var outputPath = TempCsv();
        var exporter = CreateExporter(repo: repo, validator: validator, descGen: descGen);
        var result = await exporter.ExportAsync(set.Id, outputPath);

        Assert.True(result.Success);
        Assert.True(File.Exists(outputPath));
    }

    // === row count ===

    [Fact]
    public async Task Should_WriteOneRowPerCard()
    {
        var set = ValidSet(); // 2 cards
        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(set.Id).Returns(set);

        var outputPath = TempCsv();
        var exporter = CreateExporter(repo: repo);
        var result = await exporter.ExportAsync(set.Id, outputPath);

        Assert.True(result.Success);
        Assert.Equal(2, result.RowsWritten);

        // Header + 2 data rows = 3 non-empty lines
        var lines = await File.ReadAllLinesAsync(outputPath);
        Assert.Equal(3, lines.Length);
    }

    // === SKU format ===

    [Fact]
    public async Task Should_WriteCorrectSkuPerSlot()
    {
        var set = ValidSet(id: 7); // ID 7 → FK-SET-00007-001, FK-SET-00007-002
        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(set.Id).Returns(set);

        var outputPath = TempCsv();
        var exporter = CreateExporter(repo: repo);
        await exporter.ExportAsync(set.Id, outputPath);

        var content = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("FK-SET-00007-001", content);
        Assert.Contains("FK-SET-00007-002", content);
    }

    // === shared set fields propagated ===

    [Fact]
    public async Task Should_UseSetSharedFields_InEveryRow()
    {
        var set = ValidSet();
        set.SpotPrice = 15m;
        set.SharedCondition = "Excellent";
        set.SharedShippingProfile = "PWE";
        set.SharedImageUrl1 = "https://example.com/gallery.jpg";

        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(set.Id).Returns(set);

        var descGen = Substitute.For<ISurpriseSetDescriptionGenerator>();
        descGen.Generate(Arg.Any<Models.SurpriseSet>(), Arg.Any<IList<Card>>())
            .Returns("Shared description.");

        var outputPath = TempCsv();
        var exporter = CreateExporter(repo: repo, descGen: descGen);
        await exporter.ExportAsync(set.Id, outputPath);

        var lines = await File.ReadAllLinesAsync(outputPath);
        // Both data rows should contain the same shared fields.
        Assert.Contains("15", lines[1]);
        Assert.Contains("Excellent", lines[1]);
        Assert.Contains("PWE", lines[1]);
        Assert.Contains("https://example.com/gallery.jpg", lines[1]);
        Assert.Contains("Shared description.", lines[1]);
    }

    // === spot price rounding ===

    [Fact]
    public async Task Should_RoundSpotPriceToInteger()
    {
        var set = ValidSet();
        set.SpotPrice = 9.75m;

        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(set.Id).Returns(set);

        var outputPath = TempCsv();
        var exporter = CreateExporter(repo: repo);
        await exporter.ExportAsync(set.Id, outputPath);

        var content = await File.ReadAllTextAsync(outputPath);
        Assert.Contains(",10,", content); // rounds 9.75 → 10
    }

    [Fact]
    public async Task Should_ClampSpotPriceToOne_When_ZeroOrNegative()
    {
        var set = ValidSet();
        set.SpotPrice = 0m;

        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(set.Id).Returns(set);

        var outputPath = TempCsv();
        var exporter = CreateExporter(repo: repo);
        await exporter.ExportAsync(set.Id, outputPath);

        var content = await File.ReadAllTextAsync(outputPath);
        // Price field should be 1, not 0
        var priceColIndex = Array.IndexOf(FlipKit.Core.Services.Export.WhatnotExporter.Columns, "Price");
        var dataLine = (await File.ReadAllLinesAsync(outputPath))[1];
        var fields = dataLine.Split(',');
        Assert.Equal("1", fields[priceColIndex]);
    }

    // === state transition ===

    [Fact]
    public async Task Should_TransitionStateToDraftExported_When_StatIsDraft()
    {
        var set = ValidSet(state: SurpriseSetState.Draft);
        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(set.Id).Returns(set);

        var exporter = CreateExporter(repo: repo);
        await exporter.ExportAsync(set.Id, TempCsv());

        await repo.Received(1).UpdateAsync(Arg.Is<Models.SurpriseSet>(s =>
            s.State == SurpriseSetState.Exported && s.ExportedAt.HasValue));
    }

    [Fact]
    public async Task Should_NotTransitionState_When_AlreadyExported()
    {
        var set = ValidSet(state: SurpriseSetState.Exported);
        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(set.Id).Returns(set);

        var exporter = CreateExporter(repo: repo);
        var result = await exporter.ExportAsync(set.Id, TempCsv());

        Assert.True(result.Success);
        await repo.DidNotReceive().UpdateAsync(Arg.Any<Models.SurpriseSet>());
    }

    // === per-card cost basis ===

    [Fact]
    public async Task Should_WriteCardCostBasis_When_Present()
    {
        var set = ValidSet();
        set.Cards.ElementAt(0).CostBasis = 4.50m;

        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(set.Id).Returns(set);

        var outputPath = TempCsv();
        var exporter = CreateExporter(repo: repo);
        await exporter.ExportAsync(set.Id, outputPath);

        var content = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("4.50", content);
    }

    // === slot ordering ===

    [Fact]
    public async Task Should_OrderRowsBySlot_When_CardsAreUnordered()
    {
        var set = ValidSet();
        // Reverse the card order to confirm sorting by SurpriseSetSlot.
        set.Cards = new List<Card>
        {
            new() { Id = 102, PlayerName = "Second", SurpriseSetSlot = 2 },
            new() { Id = 101, PlayerName = "First",  SurpriseSetSlot = 1 },
        };

        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(set.Id).Returns(set);

        var outputPath = TempCsv();
        var exporter = CreateExporter(repo: repo);
        await exporter.ExportAsync(set.Id, outputPath);

        var lines = await File.ReadAllLinesAsync(outputPath);
        // First data row should have slot-1 SKU.
        Assert.Contains("FK-SET-00001-001", lines[1]);
        Assert.Contains("FK-SET-00001-002", lines[2]);
    }

    // === title truncation ===

    [Fact]
    public async Task Should_TruncateTitleAt80Chars()
    {
        var set = ValidSet();
        set.Title = new string('A', 90);

        var repo = Substitute.For<ISurpriseSetRepository>();
        repo.GetByIdWithCardsAsync(set.Id).Returns(set);

        var outputPath = TempCsv();
        var exporter = CreateExporter(repo: repo);
        await exporter.ExportAsync(set.Id, outputPath);

        var content = await File.ReadAllTextAsync(outputPath);
        Assert.DoesNotContain(new string('A', 81), content);
        Assert.Contains(new string('A', 80), content);
    }
}
