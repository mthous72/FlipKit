using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using Microsoft.Extensions.Logging;

namespace FlipKit.Desktop.ViewModels
{
    /// <summary>
    /// Drives <see cref="Views.ImportEbayListingsDialog"/>. Same dialog-only
    /// pattern as <see cref="ImportChecklistViewModel"/> — inherits
    /// <see cref="ObservableObject"/> directly so the ViewLocator's "every page
    /// VM needs a matching View" smoke test stays green.
    /// </summary>
    public partial class ImportEbayListingsViewModel : ObservableObject
    {
        private readonly IFileDialogService _fileDialogService;
        private readonly IEbayListingImportService _importService;
        private readonly ILogger<ImportEbayListingsViewModel> _logger;

        private EbayListingImportPreview? _preview;

        [ObservableProperty] private string? _fileName;
        [ObservableProperty] private bool _isParsing;
        [ObservableProperty] private bool _isCommitting;
        [ObservableProperty] private bool _committed;
        [ObservableProperty] private int _insertCount;
        [ObservableProperty] private int _updateCount;
        [ObservableProperty] private string? _statusMessage;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private EbayListingImportResult? _commitResult;

        public ObservableCollection<EbayImportRowPreview> Rows { get; } = new();
        public ObservableCollection<string> Warnings { get; } = new();

        public bool HasPreview => _preview is not null && Rows.Count > 0;

        partial void OnIsParsingChanged(bool value) => OnPropertyChanged(nameof(HasPreview));

        public ImportEbayListingsViewModel(
            IFileDialogService fileDialogService,
            IEbayListingImportService importService,
            ILogger<ImportEbayListingsViewModel> logger)
        {
            _fileDialogService = fileDialogService;
            _importService = importService;
            _logger = logger;
        }

        [RelayCommand]
        private async Task BrowseAndParseAsync()
        {
            ErrorMessage = null;
            StatusMessage = null;

            var path = await _fileDialogService.OpenFileAsync(
                "Select eBay Seller Hub CSV export",
                new[] { "csv" });
            if (string.IsNullOrEmpty(path)) return;

            FileName = Path.GetFileName(path);
            await ParseAsync(path);
        }

        private async Task ParseAsync(string path)
        {
            IsParsing = true;
            StatusMessage = "Parsing CSV and enriching titles via OpenRouter (one batch per ~10 listings)…";
            Rows.Clear();
            Warnings.Clear();
            InsertCount = 0;
            UpdateCount = 0;

            try
            {
                using var stream = File.OpenRead(path);
                _preview = await _importService.ParseAsync(stream, Path.GetFileName(path));

                foreach (var row in _preview.Rows)
                    Rows.Add(row);
                foreach (var w in _preview.Warnings)
                    Warnings.Add(w);

                InsertCount = _preview.InsertCount;
                UpdateCount = _preview.UpdateCount;
                StatusMessage = $"Parsed {_preview.Rows.Count} listings ({InsertCount} new, {UpdateCount} updates). Review and click Import to save.";
                OnPropertyChanged(nameof(HasPreview));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "eBay listings preview failed for {Path}", path);
                ErrorMessage = $"Parse failed: {ex.Message}";
                StatusMessage = null;
            }
            finally
            {
                IsParsing = false;
            }
        }

        [RelayCommand]
        public async Task CommitAsync()
        {
            if (_preview is null) return;

            IsCommitting = true;
            ErrorMessage = null;
            StatusMessage = "Importing…";

            try
            {
                CommitResult = await _importService.CommitAsync(_preview);
                if (CommitResult.Errors.Count == 0)
                {
                    StatusMessage = $"Imported: {CommitResult.Inserted} new, {CommitResult.Updated} updated, {CommitResult.Skipped} skipped.";
                    Committed = true;
                }
                else
                {
                    StatusMessage = $"Imported with {CommitResult.Errors.Count} errors: {CommitResult.Inserted} new, {CommitResult.Updated} updated.";
                    foreach (var err in CommitResult.Errors)
                        Warnings.Add(err);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "eBay listings commit failed");
                ErrorMessage = $"Import failed: {ex.Message}";
            }
            finally
            {
                IsCommitting = false;
            }
        }
    }
}
