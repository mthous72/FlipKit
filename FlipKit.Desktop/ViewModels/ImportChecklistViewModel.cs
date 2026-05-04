using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlipKit.Core.Models;
using FlipKit.Core.Services;

namespace FlipKit.Desktop.ViewModels
{
    /// <summary>
    /// Drives <see cref="Views.ImportChecklistDialog"/>. Dialog-only — not a navigable
    /// "page" VM, so it intentionally inherits <see cref="ObservableObject"/> directly
    /// rather than <see cref="ViewModelBase"/>. The ViewLocator smoke test treats every
    /// ViewModelBase derivative as a page that needs a matching View.
    /// </summary>
    public partial class ImportChecklistViewModel : ObservableObject
    {
        private readonly IChecklistImportService _importService;
        private readonly IBrowserService _browserService;
        private ChecklistImportPreview? _preview;

        [ObservableProperty] private string _fileName = string.Empty;
        [ObservableProperty] private int? _year;
        [ObservableProperty] private string? _sport;
        [ObservableProperty] private string? _manufacturer;
        [ObservableProperty] private string? _brand;
        [ObservableProperty] private string? _setName;
        [ObservableProperty] private int _cardCount;
        [ObservableProperty] private int _subsetCount;
        [ObservableProperty] private string _detectedFormat = string.Empty;
        [ObservableProperty] private ObservableCollection<ChecklistCard> _firstRows = new();
        [ObservableProperty] private ObservableCollection<string> _warnings = new();
        [ObservableProperty] private string? _statusMessage;
        [ObservableProperty] private bool _isCommitting;
        [ObservableProperty] private bool _committed;
        [ObservableProperty] private ChecklistImportCommitResult? _commitResult;

        public ImportChecklistViewModel(IChecklistImportService importService, IBrowserService browserService)
        {
            _importService = importService;
            _browserService = browserService;
        }

        public void LoadPreview(ChecklistImportPreview preview)
        {
            _preview = preview;
            FileName = preview.Metadata.SourceFileName;
            Year = preview.Metadata.Year;
            Sport = preview.Metadata.Sport;
            Manufacturer = preview.Metadata.Manufacturer;
            Brand = preview.Metadata.Brand;
            SetName = preview.Metadata.SetName;
            CardCount = preview.CardCount;
            SubsetCount = preview.SubsetCount;
            DetectedFormat = preview.DetectedFormat.ToString();
            FirstRows = new ObservableCollection<ChecklistCard>(preview.Cards.Take(20));
            Warnings = new ObservableCollection<string>(preview.Warnings.Take(50));
        }

        [RelayCommand]
        private void OpenChecklistInsider()
        {
            _browserService.OpenUrl("https://www.checklistinsider.com/");
        }

        [RelayCommand]
        private async Task CommitAsync()
        {
            if (_preview == null) return;

            // Reflect any user edits back into the preview metadata before commit.
            _preview.Metadata.Year = Year;
            _preview.Metadata.Sport = string.IsNullOrWhiteSpace(Sport) ? null : Sport.Trim();
            _preview.Metadata.Manufacturer = string.IsNullOrWhiteSpace(Manufacturer) ? null : Manufacturer.Trim();
            _preview.Metadata.Brand = string.IsNullOrWhiteSpace(Brand) ? null : Brand.Trim();
            _preview.Metadata.SetName = string.IsNullOrWhiteSpace(SetName) ? null : SetName.Trim();

            try
            {
                IsCommitting = true;
                StatusMessage = "Importing...";
                CommitResult = await _importService.CommitAsync(_preview);
                if (CommitResult.Success)
                {
                    StatusMessage = CommitResult.ReplacedExisting
                        ? $"Replaced existing checklist — {CommitResult.CardsImported} cards across {CommitResult.SubsetCount} subsets."
                        : $"Imported {CommitResult.CardsImported} cards across {CommitResult.SubsetCount} subsets.";
                    Committed = true;
                }
                else
                {
                    StatusMessage = CommitResult.ErrorMessage ?? "Import failed.";
                }
            }
            finally
            {
                IsCommitting = false;
            }
        }
    }
}
