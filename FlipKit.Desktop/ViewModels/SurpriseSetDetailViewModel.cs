using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;

namespace FlipKit.Desktop.ViewModels
{
    public partial class SurpriseSetDetailViewModel : ViewModelBase
    {
        private readonly ISurpriseSetRepository _repository;
        private readonly ISurpriseSetValidator _validator;
        private readonly ISurpriseSetCsvExporter _csvExporter;
        private readonly ISurpriseSetCompletionService _completionService;
        private readonly IFileDialogService _fileDialog;
        private readonly INavigationService _navigation;

        [ObservableProperty] private SurpriseSet? _set;
        [ObservableProperty] private ObservableCollection<Card> _cards = new();
        [ObservableProperty] private ObservableCollection<SurpriseSetIssue> _issues = new();
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private bool _isExporting;
        [ObservableProperty] private bool _isCompleting;
        [ObservableProperty] private string? _statusMessage;
        [ObservableProperty] private string? _exportStatusMessage;
        [ObservableProperty] private string? _completeStatusMessage;

        // Mark-completed form fields
        [ObservableProperty] private int _spotsSold;
        [ObservableProperty] private decimal _grossRevenue;
        [ObservableProperty] private decimal _totalFees;
        [ObservableProperty] private decimal _totalShipping;

        public bool HasErrors => Issues.Any(i => i.Severity == IssueSeverity.Error);
        public int CardCount => Cards.Count;
        public bool CanComplete => Set?.State is SurpriseSetState.Draft
            or SurpriseSetState.Exported
            or SurpriseSetState.Live;

        public SurpriseSetDetailViewModel(
            ISurpriseSetRepository repository,
            ISurpriseSetValidator validator,
            ISurpriseSetCsvExporter csvExporter,
            ISurpriseSetCompletionService completionService,
            IFileDialogService fileDialog,
            INavigationService navigation)
        {
            _repository = repository;
            _validator = validator;
            _csvExporter = csvExporter;
            _completionService = completionService;
            _fileDialog = fileDialog;
            _navigation = navigation;
        }

        public async Task LoadAsync(int setId)
        {
            IsLoading = true;
            StatusMessage = null;
            ExportStatusMessage = null;
            CompleteStatusMessage = null;
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
                SpotsSold = ordered.Count; // default to full sell-through
                GrossRevenue = Set.GrossRevenue ?? Set.SpotPrice * ordered.Count;
                TotalFees = Set.TotalFees ?? 0m;
                TotalShipping = Set.TotalShipping ?? 0m;
                OnPropertyChanged(nameof(CanComplete));
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
        private async Task MarkCompletedAsync()
        {
            if (Set == null) return;

            IsCompleting = true;
            CompleteStatusMessage = null;
            try
            {
                var result = await _completionService.CompleteAsync(Set.Id, new CompleteSetRequest
                {
                    SpotsSold = SpotsSold,
                    GrossRevenue = GrossRevenue,
                    TotalFees = TotalFees,
                    TotalShipping = TotalShipping,
                });

                if (result.Success)
                {
                    CompleteStatusMessage = $"Set marked Completed — {result.Allocations.Count(a => a.IsSold)} cards sold.";
                    await LoadAsync(Set.Id);
                }
                else
                {
                    CompleteStatusMessage = $"Error: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                CompleteStatusMessage = $"Failed: {ex.Message}";
            }
            finally
            {
                IsCompleting = false;
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
            OnPropertyChanged(nameof(CanComplete));
        }
    }
}
