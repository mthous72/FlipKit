using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlipKit.Core.Models;
using FlipKit.Core.Services;

namespace FlipKit.Desktop.ViewModels
{
    public partial class SurpriseSetDetailViewModel : ViewModelBase
    {
        private readonly ISurpriseSetRepository _repository;
        private readonly ISurpriseSetValidator _validator;
        private readonly ISurpriseSetCsvExporter _csvExporter;
        private readonly IFileDialogService _fileDialog;
        private readonly INavigationService _navigation;

        [ObservableProperty] private SurpriseSet? _set;
        [ObservableProperty] private ObservableCollection<Card> _cards = new();
        [ObservableProperty] private ObservableCollection<SurpriseSetIssue> _issues = new();
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private bool _isExporting;
        [ObservableProperty] private string? _statusMessage;
        [ObservableProperty] private string? _exportStatusMessage;

        public bool HasErrors => Issues.Any(i => i.Severity == IssueSeverity.Error);
        public int CardCount => Cards.Count;

        public SurpriseSetDetailViewModel(
            ISurpriseSetRepository repository,
            ISurpriseSetValidator validator,
            ISurpriseSetCsvExporter csvExporter,
            IFileDialogService fileDialog,
            INavigationService navigation)
        {
            _repository = repository;
            _validator = validator;
            _csvExporter = csvExporter;
            _fileDialog = fileDialog;
            _navigation = navigation;
        }

        public async Task LoadAsync(int setId)
        {
            IsLoading = true;
            StatusMessage = null;
            ExportStatusMessage = null;
            try
            {
                Set = await _repository.GetByIdWithCardsAsync(setId);
                if (Set == null)
                {
                    StatusMessage = $"Set {setId} not found.";
                    return;
                }

                var ordered = Set.Cards.OrderBy(c => c.SurpriseSetSlot ?? int.MaxValue).ToList();
                Cards = new ObservableCollection<Card>(ordered);
                RefreshIssues(Set, ordered);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RemoveCardAsync(Card? card)
        {
            if (card == null || Set == null) return;
            try
            {
                await _repository.RemoveCardAsync(Set.Id, card.Id);
                await LoadAsync(Set.Id);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not remove card: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ExportCsvAsync()
        {
            if (Set == null) return;

            var path = await _fileDialog.SaveFileAsync(
                $"Export {Set.Name}",
                $"{Set.Name}-surprise-set.csv",
                new[] { "csv" });
            if (path == null) return;

            IsExporting = true;
            ExportStatusMessage = null;
            try
            {
                var result = await _csvExporter.ExportAsync(Set.Id, path);
                if (result.Success)
                {
                    ExportStatusMessage = $"Exported {result.RowsWritten} rows to {System.IO.Path.GetFileName(path)}.";
                    // Reload so the State badge updates to Exported.
                    await LoadAsync(Set.Id);
                }
                else
                {
                    var errorList = string.Join("; ", result.BlockingIssues.Select(i => i.Message));
                    ExportStatusMessage = $"Export blocked: {errorList}";
                }
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"Export failed: {ex.Message}";
            }
            finally
            {
                IsExporting = false;
            }
        }

        [RelayCommand]
        private async Task GoBackAsync() => await _navigation.NavigateAsync("SurpriseSets");

        private void RefreshIssues(SurpriseSet set, IList<Card> cards)
        {
            var found = _validator.Validate(set, cards);
            Issues = new ObservableCollection<SurpriseSetIssue>(found);
            OnPropertyChanged(nameof(HasErrors));
            OnPropertyChanged(nameof(CardCount));
        }
    }
}
