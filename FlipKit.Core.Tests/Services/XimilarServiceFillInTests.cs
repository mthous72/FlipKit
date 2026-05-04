using System.Net;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Core.Tests.Services;

/// <summary>
/// Phase 4e gap-fill — covers MapTagsToCard (the fallback path when Ximilar
/// detected an object but didn't return an _identification.best_match) and
/// DetermineVariationType (foil/holo / autograph / graded tag combinations).
/// </summary>
public class XimilarServiceFillInTests
{
    private static XimilarService Create(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler),
            Substitute.For<ISettingsService>().Tap(s => s.Load().Returns(new AppSettings { XimilarApiKey = "k" })),
            NullLogger<XimilarService>.Instance);

    [Fact]
    public async Task Should_MapTagsToCardSubcategorySport_When_NoIdentificationButTagsPresent()
    {
        // Object has tags (with high-prob subcategory) but no _identification → falls
        // through to MapTagsToCard. Sport gets parsed from the Subcategory tag.
        using var image = new TempImageFile();
        var body = @"{
            ""records"": [{
                ""_objects"": [{
                    ""prob"": 0.7,
                    ""_tags"": {
                        ""Subcategory"": [{ ""name"": ""Football"", ""prob"": 0.95 }]
                    }
                }]
            }]
        }";
        var svc = Create(new StubHttpMessageHandler(HttpStatusCode.OK, body));

        var result = await svc.RecognizeCardAsync(image.Path);

        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal("Unknown Player", result.Card!.PlayerName); // tags-only path
        Assert.Equal(Sport.Football, result.Card.Sport);
    }

    [Fact]
    public async Task Should_DetectAutographFromTags_When_AutographTagAboveProbabilityThreshold()
    {
        using var image = new TempImageFile();
        var body = @"{
            ""records"": [{
                ""_objects"": [{
                    ""prob"": 0.8,
                    ""_tags"": {
                        ""Autograph"": [{ ""name"": ""autograph"", ""prob"": 0.9 }],
                        ""Subcategory"": [{ ""name"": ""Baseball"", ""prob"": 0.95 }]
                    }
                }]
            }]
        }";
        var svc = Create(new StubHttpMessageHandler(HttpStatusCode.OK, body));

        var result = await svc.RecognizeCardAsync(image.Path);

        Assert.True(result!.Card!.IsAuto);
        Assert.Equal("Auto", result.Card.VariationType);
    }

    [Fact]
    public async Task Should_DetectFoilHoloFromTags_When_TagsHaveFoilName()
    {
        using var image = new TempImageFile();
        // No autograph tag → DetermineVariationType returns "Refractor" for foil/holo.
        var body = @"{
            ""records"": [{
                ""_objects"": [{
                    ""prob"": 0.85,
                    ""_tags"": {
                        ""Foil/Holo"": [{ ""name"": ""foil"", ""prob"": 0.9 }],
                        ""Subcategory"": [{ ""name"": ""Basketball"", ""prob"": 0.95 }]
                    }
                }]
            }]
        }";
        var svc = Create(new StubHttpMessageHandler(HttpStatusCode.OK, body));

        var result = await svc.RecognizeCardAsync(image.Path);

        Assert.Equal("Refractor", result!.Card!.VariationType);
    }

    [Fact]
    public async Task Should_DefaultToBaseVariation_When_NoSpecialTagsPresent()
    {
        using var image = new TempImageFile();
        var body = @"{
            ""records"": [{
                ""_objects"": [{
                    ""prob"": 0.85,
                    ""_tags"": { ""Subcategory"": [{ ""name"": ""Baseball"", ""prob"": 0.95 }] }
                }]
            }]
        }";
        var svc = Create(new StubHttpMessageHandler(HttpStatusCode.OK, body));

        var result = await svc.RecognizeCardAsync(image.Path);

        Assert.Equal("Base", result!.Card!.VariationType);
    }

    [Fact]
    public async Task Should_MapMmaSubcategoryToMmaSport_When_BestMatchIdentificationHasUfcSubcategory()
    {
        // The best_match path also handles MMA/UFC mapping — verify both alias terms.
        using var image = new TempImageFile();
        var body = @"{
            ""records"": [{
                ""_objects"": [{
                    ""prob"": 0.92,
                    ""_identification"": {
                        ""best_match"": {
                            ""name"": ""Conor McGregor"",
                            ""subcategory"": ""UFC""
                        }
                    },
                    ""_tags"": {}
                }]
            }]
        }";
        var svc = Create(new StubHttpMessageHandler(HttpStatusCode.OK, body));

        var result = await svc.RecognizeCardAsync(image.Path);

        Assert.Equal(Sport.MMA, result!.Card!.Sport);
        Assert.Equal("MMA Cards", result.Card.WhatnotSubcategory);
    }
}
