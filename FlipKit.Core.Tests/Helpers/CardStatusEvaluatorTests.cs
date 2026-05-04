using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Tests.Helpers;

public class CardStatusEvaluatorTests
{
    // HasAnyImage: any of the 16 image slots (8 paths + 8 URLs) returns true.

    [Fact]
    public void Should_ReturnFalse_When_AllImageSlotsAreEmpty()
    {
        var card = new Card();
        Assert.False(CardStatusEvaluator.HasAnyImage(card));
    }

    [Fact]
    public void Should_ReturnTrue_When_FrontImagePathIsSet()
    {
        var card = new Card { ImagePathFront = "/tmp/front.jpg" };
        Assert.True(CardStatusEvaluator.HasAnyImage(card));
    }

    [Fact]
    public void Should_ReturnTrue_When_OnlyHostedUrlIsSet()
    {
        // Hosted URL with no local path is the post-upload state — still "has image".
        var card = new Card { ImageUrl1 = "https://i.ibb.co/xyz/front.jpg" };
        Assert.True(CardStatusEvaluator.HasAnyImage(card));
    }

    [Fact]
    public void Should_ReturnTrue_When_AnyExtraSlotIsSet()
    {
        // Slots 3-8 are user-attached extras; cover one to exercise the path.
        var card = new Card { ImagePath7 = "/tmp/edge.jpg" };
        Assert.True(CardStatusEvaluator.HasAnyImage(card));
    }

    // HasPrice: ListingPrice must be non-null AND > 0.

    [Fact]
    public void Should_ReturnFalse_When_ListingPriceIsNull()
    {
        var card = new Card { ListingPrice = null };
        Assert.False(CardStatusEvaluator.HasPrice(card));
    }

    [Fact]
    public void Should_ReturnFalse_When_ListingPriceIsZero()
    {
        var card = new Card { ListingPrice = 0m };
        Assert.False(CardStatusEvaluator.HasPrice(card));
    }

    [Fact]
    public void Should_ReturnTrue_When_ListingPriceIsPositive()
    {
        var card = new Card { ListingPrice = 5.00m };
        Assert.True(CardStatusEvaluator.HasPrice(card));
    }

    // Evaluate: terminal states preserved; otherwise Ready (image+price) or Draft.

    [Fact]
    public void Should_PreserveListedStatus_When_AlreadyListed()
    {
        // Listed is terminal — set by the export flow, not derived from current state.
        var card = new Card { Status = CardStatus.Listed };
        Assert.Equal(CardStatus.Listed, CardStatusEvaluator.Evaluate(card));
    }

    [Fact]
    public void Should_PreserveSoldStatus_When_AlreadySold()
    {
        var card = new Card { Status = CardStatus.Sold };
        Assert.Equal(CardStatus.Sold, CardStatusEvaluator.Evaluate(card));
    }

    [Fact]
    public void Should_ReturnReady_When_HasImageAndPrice()
    {
        var card = new Card
        {
            Status = CardStatus.Draft,
            ImageUrl1 = "https://i.ibb.co/xyz/front.jpg",
            ListingPrice = 10m,
        };
        Assert.Equal(CardStatus.Ready, CardStatusEvaluator.Evaluate(card));
    }

    [Fact]
    public void Should_ReturnDraft_When_HasImageButNoPrice()
    {
        var card = new Card
        {
            Status = CardStatus.Draft,
            ImageUrl1 = "https://i.ibb.co/xyz/front.jpg",
            ListingPrice = null,
        };
        Assert.Equal(CardStatus.Draft, CardStatusEvaluator.Evaluate(card));
    }

    [Fact]
    public void Should_ReturnDraft_When_HasPriceButNoImage()
    {
        var card = new Card { Status = CardStatus.Draft, ListingPrice = 10m };
        Assert.Equal(CardStatus.Draft, CardStatusEvaluator.Evaluate(card));
    }

    [Fact]
    public void Should_DeriveStatusFromConditions_When_StatusIsPriced()
    {
        // Priced is a non-terminal status — Evaluate should re-derive.
        // With no image, even with a price, falls back to Draft.
        var card = new Card { Status = CardStatus.Priced, ListingPrice = 10m };
        Assert.Equal(CardStatus.Draft, CardStatusEvaluator.Evaluate(card));
    }
}
