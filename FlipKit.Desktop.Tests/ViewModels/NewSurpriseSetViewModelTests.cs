using FlipKit.Core.Models.Enums;
using FlipKit.Desktop.ViewModels;

namespace FlipKit.Desktop.Tests.ViewModels;

public class NewSurpriseSetViewModelTests
{
    [Fact]
    public void Should_BeInvalid_When_NameIsEmpty()
    {
        var vm = new NewSurpriseSetViewModel();
        Assert.False(vm.IsValid);
    }

    [Fact]
    public void Should_BeInvalid_When_NameIsWhitespace()
    {
        var vm = new NewSurpriseSetViewModel { Name = "   " };
        Assert.False(vm.IsValid);
    }

    [Fact]
    public void Should_BecomeValid_When_NameIsAssigned()
    {
        var vm = new NewSurpriseSetViewModel();
        vm.Name = "May Baseball Lot";
        Assert.True(vm.IsValid);
    }

    [Fact]
    public void Should_RaisePropertyChangedForIsValid_When_NameChanges()
    {
        // Without OnNameChanged cascading the notification, IsValid stays
        // stale and the Create button's IsEnabled binding never fires —
        // which was the original "can't finalize" bug.
        var vm = new NewSurpriseSetViewModel();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        vm.Name = "New Set";

        Assert.Contains(nameof(NewSurpriseSetViewModel.IsValid), raised);
    }

    [Fact]
    public void Should_ClearValidationError_When_NameChanges()
    {
        var vm = new NewSurpriseSetViewModel { ValidationError = "Name is required." };
        vm.Name = "Now valid";
        Assert.Null(vm.ValidationError);
    }

    [Fact]
    public void Should_BuildDraftSet_When_NameOnlyProvided()
    {
        var vm = new NewSurpriseSetViewModel { Name = "  Trimmed Name  " };

        var set = vm.BuildSet();

        Assert.Equal("Trimmed Name", set.Name);
        Assert.Equal(SurpriseSetState.Draft, set.State);
        Assert.Equal("Trimmed Name", set.Title); // falls back to Name when Title blank
        Assert.Null(set.ShowName);
        Assert.Null(set.Notes);
    }

    [Fact]
    public void Should_PreferTitleOverName_When_TitleProvided()
    {
        var vm = new NewSurpriseSetViewModel
        {
            Name = "Internal",
            Title = "Public Listing Title",
        };

        var set = vm.BuildSet();

        Assert.Equal("Public Listing Title", set.Title);
    }

    [Fact]
    public void Should_DefaultWhatnotCategory_When_BlankProvided()
    {
        var vm = new NewSurpriseSetViewModel
        {
            Name = "X",
            SharedWhatnotCategory = "   ",
        };

        var set = vm.BuildSet();

        Assert.Equal("Sports Trading Cards", set.SharedWhatnotCategory);
    }
}
