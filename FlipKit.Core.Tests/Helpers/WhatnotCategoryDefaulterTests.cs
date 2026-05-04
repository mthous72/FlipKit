using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Tests.Helpers;

public class WhatnotCategoryDefaulterTests
{
    // ApplyDefaults: only fills blanks — never overrides explicit user values.

    [Fact]
    public void Should_DefaultCategoryToSportsCards_When_CategoryIsEmpty()
    {
        var card = new Card { WhatnotCategory = string.Empty, Sport = Sport.Baseball };
        WhatnotCategoryDefaulter.ApplyDefaults(card);
        Assert.Equal("Sports Cards", card.WhatnotCategory);
    }

    [Fact]
    public void Should_PreserveExistingCategory_When_AlreadySet()
    {
        var card = new Card { WhatnotCategory = "Trading Card Games", Sport = Sport.Baseball };
        WhatnotCategoryDefaulter.ApplyDefaults(card);
        Assert.Equal("Trading Card Games", card.WhatnotCategory);
    }

    [Fact]
    public void Should_PreserveExistingSubcategory_When_AlreadySet()
    {
        var card = new Card
        {
            WhatnotCategory = "Sports Cards",
            WhatnotSubcategory = "Custom Bucket",
            Sport = Sport.Baseball,
        };
        WhatnotCategoryDefaulter.ApplyDefaults(card);
        Assert.Equal("Custom Bucket", card.WhatnotSubcategory);
    }

    [Fact]
    public void Should_DeriveSubcategoryFromSport_When_SportsCardsAndSportSet()
    {
        var card = new Card { WhatnotCategory = "Sports Cards", Sport = Sport.Basketball };
        WhatnotCategoryDefaulter.ApplyDefaults(card);
        Assert.Equal("Basketball Singles", card.WhatnotSubcategory);
    }

    [Fact]
    public void Should_LeaveSubcategoryBlank_When_SportsCardsButNoSport()
    {
        var card = new Card { WhatnotCategory = "Sports Cards", Sport = null };
        WhatnotCategoryDefaulter.ApplyDefaults(card);
        Assert.Null(card.WhatnotSubcategory);
    }

    [Fact]
    public void Should_LeaveSubcategoryBlank_When_CategoryIsNotSportsCards()
    {
        // Trading Card Games has no derivation — user must pick (validator catches).
        var card = new Card { WhatnotCategory = "Trading Card Games", Sport = Sport.Baseball };
        WhatnotCategoryDefaulter.ApplyDefaults(card);
        Assert.Null(card.WhatnotSubcategory);
    }

    [Fact]
    public void Should_FallBackToOtherSportsCards_When_SportNotInExplicitMapping()
    {
        // Wrestling, Golf, Tennis, Racing, MMA all hit the default arm.
        var card = new Card { WhatnotCategory = "Sports Cards", Sport = Sport.MMA };
        WhatnotCategoryDefaulter.ApplyDefaults(card);
        Assert.Equal("Other Sports Cards", card.WhatnotSubcategory);
    }

    [Fact]
    public void Should_MapBaseballToBaseballSingles_When_SportIsBaseball()
    {
        var card = new Card { WhatnotCategory = "Sports Cards", Sport = Sport.Baseball };
        WhatnotCategoryDefaulter.ApplyDefaults(card);
        Assert.Equal("Baseball Singles", card.WhatnotSubcategory);
    }

    [Fact]
    public void Should_MapBasketballToBasketballSingles_When_SportIsBasketball()
    {
        var card = new Card { WhatnotCategory = "Sports Cards", Sport = Sport.Basketball };
        WhatnotCategoryDefaulter.ApplyDefaults(card);
        Assert.Equal("Basketball Singles", card.WhatnotSubcategory);
    }

    [Fact]
    public void Should_MapFootballToFootballSingles_When_SportIsFootball()
    {
        var card = new Card { WhatnotCategory = "Sports Cards", Sport = Sport.Football };
        WhatnotCategoryDefaulter.ApplyDefaults(card);
        Assert.Equal("Football Singles", card.WhatnotSubcategory);
    }

    [Fact]
    public void Should_MapHockeyToHockeySingles_When_SportIsHockey()
    {
        var card = new Card { WhatnotCategory = "Sports Cards", Sport = Sport.Hockey };
        WhatnotCategoryDefaulter.ApplyDefaults(card);
        Assert.Equal("Hockey Singles", card.WhatnotSubcategory);
    }

    [Fact]
    public void Should_MapSoccerToSoccerSingles_When_SportIsSoccer()
    {
        var card = new Card { WhatnotCategory = "Sports Cards", Sport = Sport.Soccer };
        WhatnotCategoryDefaulter.ApplyDefaults(card);
        Assert.Equal("Soccer Singles", card.WhatnotSubcategory);
    }
}
