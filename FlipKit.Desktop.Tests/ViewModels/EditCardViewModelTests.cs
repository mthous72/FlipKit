using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Desktop.Tests.ViewModels;

public class EditCardViewModelTests
{
    private static EditCardViewModel Create(
        ICardRepository? repo = null,
        INavigationService? nav = null,
        IFileDialogService? dialog = null,
        IImageUploadService? upload = null) =>
        new(repo ?? Substitute.For<ICardRepository>(),
            nav ?? Substitute.For<INavigationService>(),
            dialog ?? Substitute.For<IFileDialogService>(),
            upload ?? Substitute.For<IImageUploadService>(),
            NullLogger<EditCardViewModel>.Instance);

    private static Card SampleCard(int id = 7) => new()
    {
        Id = id,
        PlayerName = "Mike Trout",
        Year = 2026,
        Brand = "Bowman",
        ImagePathFront = "/tmp/front.jpg",
        ImagePath3 = "/tmp/extra.jpg",
        ImageUrl1 = "https://i.ibb.co/x.jpg",
    };

    // === LoadCardAsync ===

    [Fact]
    public async Task Should_PopulateCardDetailFromRepository_When_LoadCardSucceeds()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardAsync(7).Returns(SampleCard());
        var vm = Create(repo: repo);

        await vm.LoadCardAsync(7);

        Assert.NotNull(vm.CardDetail);
        Assert.Equal("Mike Trout", vm.CardDetail!.PlayerName);
        Assert.Equal("/tmp/front.jpg", vm.ImagePathFront);
        Assert.Equal("https://i.ibb.co/x.jpg", vm.ImageUrl1);
        Assert.Single(vm.AdditionalPhotos); // ImagePath3 populated
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Should_SetErrorMessage_When_CardNotFound()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardAsync(99).Returns((Card?)null);
        var vm = Create(repo: repo);

        await vm.LoadCardAsync(99);

        Assert.Contains("not found", vm.ErrorMessage);
        Assert.Null(vm.CardDetail);
    }

    [Fact]
    public async Task Should_SetErrorMessage_When_LoadThrows()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardAsync(1).Returns<Card?>(_ => throw new Exception("db down"));
        var vm = Create(repo: repo);

        await vm.LoadCardAsync(1);

        Assert.Contains("Failed to load", vm.ErrorMessage);
    }

    // === DisplayImage prefers hosted URL over local path ===

    [Fact]
    public async Task Should_PreferHostedUrl_When_BothPathAndUrlPresent()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardAsync(7).Returns(SampleCard());
        var vm = Create(repo: repo);

        await vm.LoadCardAsync(7);

        // ImageUrl1 is set, so DisplayImageFront uses it.
        Assert.Equal("https://i.ibb.co/x.jpg", vm.DisplayImageFront);
    }

    [Fact]
    public async Task Should_FallBackToLocalPath_When_NoHostedUrl()
    {
        var card = SampleCard();
        card.ImageUrl1 = null;
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardAsync(7).Returns(card);
        var vm = Create(repo: repo);

        await vm.LoadCardAsync(7);

        Assert.Equal("/tmp/front.jpg", vm.DisplayImageFront);
    }

    // === Additional photo commands ===

    [Fact]
    public async Task Should_AddPhotoFromDialog_When_AddCommandFires()
    {
        var dialog = Substitute.For<IFileDialogService>();
        dialog.OpenImageFileAsync().Returns("/tmp/new.jpg");
        var vm = Create(dialog: dialog);

        await vm.AddAdditionalPhotoCommand.ExecuteAsync(null);

        Assert.Single(vm.AdditionalPhotos);
        Assert.Equal("/tmp/new.jpg", vm.AdditionalPhotos[0].Path);
    }

    [Fact]
    public async Task Should_NotAddPhoto_When_AlreadyAtMaxCapacity()
    {
        var dialog = Substitute.For<IFileDialogService>();
        dialog.OpenImageFileAsync().Returns("/tmp/new.jpg");
        var vm = Create(dialog: dialog);
        for (int i = 0; i < EditCardViewModel.MaxAdditionalPhotos; i++)
            vm.AdditionalPhotos.Add(new PhotoSlot($"/tmp/{i}.jpg"));

        await vm.AddAdditionalPhotoCommand.ExecuteAsync(null);

        Assert.Equal(EditCardViewModel.MaxAdditionalPhotos, vm.AdditionalPhotos.Count);
        await dialog.DidNotReceive().OpenImageFileAsync();
    }

    [Fact]
    public void Should_RemovePhoto_When_RemoveCommandFires()
    {
        var vm = Create();
        var slot = new PhotoSlot("/tmp/x.jpg");
        vm.AdditionalPhotos.Add(slot);

        vm.RemoveAdditionalPhotoCommand.Execute(slot);

        Assert.Empty(vm.AdditionalPhotos);
    }

    // === SaveAsync ===

    [Fact]
    public async Task Should_DoNothing_When_SavingWithoutLoadedCard()
    {
        var repo = Substitute.For<ICardRepository>();
        var vm = Create(repo: repo);

        await vm.SaveCommand.ExecuteAsync(null);

        await repo.DidNotReceive().UpdateCardAsync(Arg.Any<Card>());
    }

    [Fact]
    public async Task Should_PersistEditsAndNavigateToInventory_When_SavingLoadedCard()
    {
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardAsync(7).Returns(SampleCard());
        var nav = Substitute.For<INavigationService>();
        var vm = Create(repo: repo, nav: nav);
        await vm.LoadCardAsync(7);
        vm.CardDetail!.PlayerName = "Updated Name";

        await vm.SaveCommand.ExecuteAsync(null);

        await repo.Received(1).UpdateCardAsync(Arg.Is<Card>(c => c.PlayerName == "Updated Name"));
        await nav.Received(1).NavigateToInventoryAsync();
    }

    [Fact]
    public async Task Should_NavigateToInventory_When_CancelCommandFires()
    {
        var nav = Substitute.For<INavigationService>();
        var vm = Create(nav: nav);

        await vm.CancelCommand.ExecuteAsync(null);

        await nav.Received(1).NavigateToInventoryAsync();
    }

    [Fact]
    public async Task Should_SwallowImageUploadFailure_When_NetworkUnavailable()
    {
        // The save flow tries to upload missing URLs but should never block on failure.
        var repo = Substitute.For<ICardRepository>();
        repo.GetCardAsync(7).Returns(SampleCard());
        var upload = Substitute.For<IImageUploadService>();
        upload.UploadCardImagesAsync(Arg.Any<List<string?>>())
              .Returns<List<string?>>(_ => throw new Exception("network down"));
        var nav = Substitute.For<INavigationService>();
        var vm = Create(repo: repo, nav: nav, upload: upload);
        await vm.LoadCardAsync(7);

        await vm.SaveCommand.ExecuteAsync(null);

        // Save still succeeds — error is logged, not surfaced.
        await repo.Received(1).UpdateCardAsync(Arg.Any<Card>());
        await nav.Received(1).NavigateToInventoryAsync();
    }
}
