using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Desktop.Tests.ViewModels;

public class ChecklistManagerViewModelTests
{
    private static ChecklistManagerViewModel Create(
        IChecklistLearningService? svc = null,
        IFileDialogService? dialog = null,
        IChecklistImportService? excel = null,
        IServiceProvider? provider = null) =>
        new(svc ?? Substitute.For<IChecklistLearningService>(),
            dialog ?? Substitute.For<IFileDialogService>(),
            excel ?? Substitute.For<IChecklistImportService>(),
            Substitute.For<IBrowserService>(),
            provider ?? new ServiceCollection().BuildServiceProvider(),
            NullLogger<ChecklistManagerViewModel>.Instance);

    private static List<SetChecklist> SampleChecklists() => new()
    {
        new SetChecklist
        {
            Id = 1, Manufacturer = "Topps", Brand = "Bowman", Year = 2026, DataSource = "seed",
            Cards = new() { new ChecklistCard { CardNumber = "1", PlayerName = "X" } },
            KnownVariations = new() { "Base", "Refractor" },
        },
        new SetChecklist
        {
            Id = 2, Manufacturer = "Panini", Brand = "Prizm", Year = 2026, DataSource = "learned",
            Cards = new() { new ChecklistCard { CardNumber = "1", PlayerName = "Y" } },
        },
        new SetChecklist
        {
            Id = 3, Manufacturer = "Topps", Brand = "Chrome", Year = 2025, DataSource = "imported",
        },
    };

    [Fact]
    public async Task Should_PopulateChecklistsAndStats_When_LoadAsyncRuns()
    {
        var svc = Substitute.For<IChecklistLearningService>();
        svc.GetAllChecklistsAsync().Returns(SampleChecklists());
        svc.GetMissingChecklistsAsync().Returns(new List<MissingChecklist>());
        var vm = Create(svc: svc);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Checklists.Count);
        Assert.Equal(3, vm.TotalChecklists);
        Assert.Equal(2, vm.TotalCards); // 2 cards across the populated lists
        Assert.Equal(1, vm.SeededCount);
        Assert.Equal(1, vm.LearnedCount);
        Assert.Equal(1, vm.ImportedCount);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Should_PopulateMissingChecklists_When_LoadAsyncRuns()
    {
        var svc = Substitute.For<IChecklistLearningService>();
        svc.GetAllChecklistsAsync().Returns(new List<SetChecklist>());
        svc.GetMissingChecklistsAsync().Returns(new List<MissingChecklist>
        {
            new() { Manufacturer = "Topps", Brand = "Heritage", Year = 2026, HitCount = 5 },
        });
        var vm = Create(svc: svc);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Single(vm.MissingChecklists);
    }

    [Fact]
    public async Task Should_ShowDetailWithCardsAndVariations_When_ChecklistSelected()
    {
        var vm = Create();
        var checklist = new SetChecklist
        {
            Cards = new() { new ChecklistCard { CardNumber = "1", PlayerName = "Test" } },
            KnownVariations = new() { "Refractor", "Gold" },
        };

        vm.SelectedChecklist = checklist;
        await Task.Yield();

        Assert.True(vm.ShowDetail);
        Assert.Single(vm.SelectedCards);
        Assert.Equal(2, vm.SelectedVariations.Count);
    }

    [Fact]
    public void Should_HideDetailAndClearSelection_When_CloseDetailFires()
    {
        var vm = Create();
        vm.SelectedChecklist = new SetChecklist { Cards = new(), KnownVariations = new() };

        vm.CloseDetailCommand.Execute(null);

        Assert.Null(vm.SelectedChecklist);
        Assert.False(vm.ShowDetail);
    }

    [Fact]
    public async Task Should_ImportFromUserSelectedFile_When_ImportCommandSucceeds()
    {
        var dialog = Substitute.For<IFileDialogService>();
        dialog.OpenFileAsync(Arg.Any<string>(), Arg.Any<string[]>()).Returns("/tmp/import.json");
        var svc = Substitute.For<IChecklistLearningService>();
        svc.ImportChecklistAsync("/tmp/import.json").Returns(new ChecklistImportResult
        {
            Success = true, CardsAdded = 5, VariationsAdded = 3,
        });
        svc.GetAllChecklistsAsync().Returns(new List<SetChecklist>());
        svc.GetMissingChecklistsAsync().Returns(new List<MissingChecklist>());
        var vm = Create(svc: svc, dialog: dialog);

        await vm.ImportCommand.ExecuteAsync(null);

        await svc.Received(1).ImportChecklistAsync("/tmp/import.json");
        Assert.Contains("5 cards", vm.StatusMessage);
    }

    [Fact]
    public async Task Should_ShowFailureMessage_When_ImportFails()
    {
        var dialog = Substitute.For<IFileDialogService>();
        dialog.OpenFileAsync(Arg.Any<string>(), Arg.Any<string[]>()).Returns("/tmp/bad.json");
        var svc = Substitute.For<IChecklistLearningService>();
        svc.ImportChecklistAsync("/tmp/bad.json").Returns(new ChecklistImportResult
        {
            Success = false, ErrorMessage = "Invalid format",
        });
        var vm = Create(svc: svc, dialog: dialog);

        await vm.ImportCommand.ExecuteAsync(null);

        Assert.Contains("Invalid format", vm.StatusMessage);
    }

    [Fact]
    public async Task Should_DoNothing_When_ImportCancelsFileDialog()
    {
        var dialog = Substitute.For<IFileDialogService>();
        dialog.OpenFileAsync(Arg.Any<string>(), Arg.Any<string[]>()).Returns((string?)null);
        var svc = Substitute.For<IChecklistLearningService>();
        var vm = Create(svc: svc, dialog: dialog);

        await vm.ImportCommand.ExecuteAsync(null);

        await svc.DidNotReceiveWithAnyArgs().ImportChecklistAsync(default!);
    }

    [Fact]
    public async Task Should_DoNothing_When_ExportFiresWithoutSelection()
    {
        var svc = Substitute.For<IChecklistLearningService>();
        var vm = Create(svc: svc);

        await vm.ExportCommand.ExecuteAsync(null);

        await svc.DidNotReceiveWithAnyArgs().ExportChecklistAsync(default, default!);
    }

    [Fact]
    public async Task Should_DelegateToService_When_ExportCommandRunsWithSelection()
    {
        var dialog = Substitute.For<IFileDialogService>();
        dialog.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>()).Returns("/tmp/out.json");
        var svc = Substitute.For<IChecklistLearningService>();
        var vm = Create(svc: svc, dialog: dialog);
        vm.SelectedChecklist = new SetChecklist { Id = 42, Cards = new(), KnownVariations = new() };

        await vm.ExportCommand.ExecuteAsync(null);

        await svc.Received(1).ExportChecklistAsync(42, "/tmp/out.json");
    }

    [Fact]
    public async Task Should_DeleteAndReload_When_DeleteSelectedCommandFires()
    {
        var svc = Substitute.For<IChecklistLearningService>();
        svc.GetAllChecklistsAsync().Returns(new List<SetChecklist>());
        svc.GetMissingChecklistsAsync().Returns(new List<MissingChecklist>());
        var vm = Create(svc: svc);
        vm.SelectedChecklist = new SetChecklist { Id = 7, Brand = "Test", Year = 2026, Cards = new(), KnownVariations = new() };

        await vm.DeleteSelectedCommand.ExecuteAsync(null);

        await svc.Received(1).DeleteChecklistAsync(7);
        await svc.Received(1).GetAllChecklistsAsync(); // reloaded after delete
        Assert.Null(vm.SelectedChecklist);
    }
}
