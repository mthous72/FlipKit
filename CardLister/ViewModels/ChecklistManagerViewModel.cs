using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace FlipKit.Desktop.ViewModels
{
    public partial class ChecklistManagerViewModel : ViewModelBase
    {
        private readonly IChecklistLearningService _checklistService;
        private readonly IFileDialogService _fileDialogService;
        private readonly ILogger<ChecklistManagerViewModel> _logger;

        [ObservableProperty] private ObservableCollection<SetChecklist> _checklists = new();
        [ObservableProperty] private ObservableCollection<MissingChecklist> _missingChecklists = new();
        [ObservableProperty] private SetChecklist? _selectedChecklist;
        [ObservableProperty] private ObservableCollection<ChecklistCard> _selectedCards = new();
        [ObservableProperty] private ObservableCollection<string> _selectedVariations = new();
        [ObservableProperty] private string? _searchText;
        [ObservableProperty] private string? _statusMessage;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private bool _showDetail;

        // Summary stats
        [ObservableProperty] private int _totalChecklists;
        [ObservableProperty] private int _totalCards;
        [ObservableProperty] private int _seededCount;
        [ObservableProperty] private int _learnedCount;
        [ObservableProperty] private int _importedCount;

        public ChecklistManagerViewModel(
            IChecklistLearningService checklistService,
            IFileDialogService fileDialogService,
            ILogger<ChecklistManagerViewModel> logger)
        {
            _checklistService = checklistService;
            _fileDialogService = fileDialogService;
            _logger = logger;
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            try
            {
                IsLoading = true;
                var all = await _checklistService.GetAllChecklistsAsync();
                Checklists = new ObservableCollection<SetChecklist>(all);

                var missing = await _checklistService.GetMissingChecklistsAsync();
                MissingChecklists = new ObservableCollection<MissingChecklist>(missing);

                // Update stats
                TotalChecklists = all.Count;
                TotalCards = all.Sum(c => c.Cards?.Count ?? 0);
                SeededCount = all.Count(c => c.DataSource == "seed");
                LearnedCount = all.Count(c => c.DataSource == "learned");
                ImportedCount = all.Count(c => c.DataSource == "imported" || c.DataSource == "mixed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load checklists");
                StatusMessage = "Failed to load checklists";
            }
            finally
            {
                IsLoading = false;
            }
        }

        partial void OnSelectedChecklistChanged(SetChecklist? value)
        {
            if (value != null)
            {
                SelectedCards = new ObservableCollection<ChecklistCard>(value.Cards ?? new List<ChecklistCard>());
                SelectedVariations = new ObservableCollection<string>(value.KnownVariations ?? new List<string>());
                ShowDetail = true;
            }
            else
            {
                SelectedCards.Clear();
                SelectedVariations.Clear();
                ShowDetail = false;
            }
        }

        [RelayCommand]
        private void CloseDetail()
        {
            SelectedChecklist = null;
            ShowDetail = false;
        }

        [RelayCommand]
        private async Task ImportAsync()
        {
            try
            {
                var filePath = await _fileDialogService.OpenFileAsync("Import Checklist", new[] { "json" });
                if (string.IsNullOrEmpty(filePath)) return;

                var result = await _checklistService.ImportChecklistAsync(filePath);
                if (result.Success)
                {
                    StatusMessage = $"Imported: {result.CardsAdded} cards, {result.VariationsAdded} variations added";
                    await LoadAsync();
                }
                else
                {
                    StatusMessage = $"Import failed: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Import failed");
                StatusMessage = "Import failed";
            }
        }

        [RelayCommand]
        private async Task ExportAsync()
        {
            if (SelectedChecklist == null) return;
            try
            {
                var filePath = await _fileDialogService.SaveFileAsync(
                    "Export Checklist",
                    $"{SelectedChecklist.Year}-{SelectedChecklist.Manufacturer}-{SelectedChecklist.Brand}.json",
                    new[] { "json" });
                if (string.IsNullOrEmpty(filePath)) return;

                await _checklistService.ExportChecklistAsync(SelectedChecklist.Id, filePath);
                StatusMessage = "Checklist exported successfully";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Export failed");
                StatusMessage = "Export failed";
            }
        }

        [RelayCommand]
        private async Task DeleteSelectedAsync()
        {
            if (SelectedChecklist == null) return;
            try
            {
                await _checklistService.DeleteChecklistAsync(SelectedChecklist.Id);
                StatusMessage = $"Deleted {SelectedChecklist.Brand} {SelectedChecklist.Year}";
                SelectedChecklist = null;
                await LoadAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete failed");
                StatusMessage = "Delete failed";
            }
        }
    }
}
