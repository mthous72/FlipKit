using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CardLister.Models;
using CardLister.Models.Enums;
using CardLister.Helpers;
using CardLister.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CardLister.ViewModels
{
    public partial class InventoryViewModel : ViewModelBase
    {
        private readonly ICardRepository _cardRepository;
        private readonly ISettingsService _settingsService;
        private readonly IExportService _exportService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IImageUploadService _imageUploadService;
        private readonly IBrowserService _browserService;
        private readonly ILogger<InventoryViewModel> _logger;

        private List<Card> _allCards = new();

        [ObservableProperty] private ObservableCollection<SelectableCard> _filteredCards = new();
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedSport = "All";
        [ObservableProperty] private string _selectedStatus = "All";
        [ObservableProperty] private SelectableCard? _selectedItem;

        // Summary
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private int _pricedCount;
        [ObservableProperty] private int _needsPricingCount;
        [ObservableProperty] private decimal _totalValue;
        [ObservableProperty] private int _staleCardCount;

        // Delete confirmation dialog
        [ObservableProperty] private bool _showDeleteConfirmDialog;

        // Mark as Sold dialog
        [ObservableProperty] private bool _showSoldDialog;
        [ObservableProperty] private decimal? _soldSalePrice;
        [ObservableProperty] private string _soldPlatform = "Whatnot";
        [ObservableProperty] private DateTime _soldDate = DateTime.UtcNow;
        [ObservableProperty] private decimal? _soldFees;
        [ObservableProperty] private decimal? _soldShippingCost;
        [ObservableProperty] private decimal? _soldNetProfit;

        // Export
        [ObservableProperty] private bool _isUploading;
        [ObservableProperty] private int _uploadProgress;
        [ObservableProperty] private int _uploadTotal;
        [ObservableProperty] private string? _exportMessage;
        [ObservableProperty] private string? _exportError;
        [ObservableProperty] private int _selectedCount;

        public Card? SelectedCard => SelectedItem?.Card;

        public List<string> SportOptions { get; } = new() { "All", "Football", "Baseball", "Basketball" };
        public List<string> StatusOptions { get; } = new() { "All", "Draft", "Priced", "Ready", "Listed", "Sold" };

        public InventoryViewModel(
            ICardRepository cardRepository,
            ISettingsService settingsService,
            IExportService exportService,
            IFileDialogService fileDialogService,
            IImageUploadService imageUploadService,
            IBrowserService browserService,
            ILogger<InventoryViewModel> logger)
        {
            _cardRepository = cardRepository;
            _settingsService = settingsService;
            _exportService = exportService;
            _fileDialogService = fileDialogService;
            _imageUploadService = imageUploadService;
            _browserService = browserService;
            _logger = logger;

            LoadCardsAsync();
        }

        partial void OnSelectedItemChanged(SelectableCard? value)
        {
            OnPropertyChanged(nameof(SelectedCard));
        }

        [RelayCommand]
        private async Task EditSelectedAsync()
        {
            if (SelectedCard == null) return;
            if (App.Services.GetService(typeof(MainWindowViewModel)) is MainWindowViewModel mainVm)
                await mainVm.NavigateToEditCardAsync(SelectedCard.Id);
        }

        [RelayCommand]
        private void NavigateToReprice()
        {
            if (App.Services.GetService(typeof(MainWindowViewModel)) is MainWindowViewModel mainVm)
                mainVm.NavigateToCommand.Execute("Reprice");
        }

        partial void OnSearchTextChanged(string value) => ApplyFilters();
        partial void OnSelectedSportChanged(string value) => ApplyFilters();
        partial void OnSelectedStatusChanged(string value) => ApplyFilters();

        private async void LoadCardsAsync()
        {
            try
            {
                _allCards = await _cardRepository.GetAllCardsAsync();
                ApplyFilters();
                UpdateSummary();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load cards for inventory");
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            _allCards = await _cardRepository.GetAllCardsAsync();
            ApplyFilters();
            UpdateSummary();
        }

        [RelayCommand]
        private void RequestDeleteSelected()
        {
            if (SelectedCard == null) return;
            ShowDeleteConfirmDialog = true;
        }

        [RelayCommand]
        private void CancelDelete()
        {
            ShowDeleteConfirmDialog = false;
        }

        [RelayCommand]
        private async Task ConfirmDeleteAsync()
        {
            if (SelectedCard == null || SelectedItem == null) return;

            await _cardRepository.DeleteCardAsync(SelectedCard.Id);
            _allCards.Remove(SelectedCard);
            FilteredCards.Remove(SelectedItem);
            SelectedItem = null;
            ShowDeleteConfirmDialog = false;
            UpdateSummary();
        }

        partial void OnSoldSalePriceChanged(decimal? value)
        {
            if (value.HasValue)
            {
                var settings = _settingsService.Load();
                SoldFees = PriceCalculator.CalculateFees(value.Value, settings.WhatnotFeePercent);
                SoldNetProfit = value.Value - (SelectedCard?.CostBasis ?? 0) - (SoldFees ?? 0) - (SoldShippingCost ?? 0);
            }
        }

        [RelayCommand]
        private void OpenSoldDialog()
        {
            if (SelectedCard == null) return;
            SoldSalePrice = SelectedCard.ListingPrice;
            SoldPlatform = "Whatnot";
            SoldDate = DateTime.UtcNow;
            SoldShippingCost = 1.00m;
            OnSoldSalePriceChanged(SoldSalePrice);
            ShowSoldDialog = true;
        }

        [RelayCommand]
        private void CancelSoldDialog()
        {
            ShowSoldDialog = false;
        }

        [RelayCommand]
        private async Task ConfirmSoldAsync()
        {
            if (SelectedCard == null || !SoldSalePrice.HasValue) return;

            SelectedCard.SalePrice = SoldSalePrice;
            SelectedCard.SalePlatform = SoldPlatform;
            SelectedCard.SaleDate = SoldDate;
            SelectedCard.FeesPaid = SoldFees;
            SelectedCard.ShippingCost = SoldShippingCost;
            SelectedCard.NetProfit = SoldSalePrice.Value - (SelectedCard.CostBasis ?? 0) - (SoldFees ?? 0) - (SoldShippingCost ?? 0);
            SelectedCard.Status = CardStatus.Sold;

            await _cardRepository.UpdateCardAsync(SelectedCard);
            ShowSoldDialog = false;
            ApplyFilters();
            UpdateSummary();
        }

        // === Selection Commands ===

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var item in FilteredCards)
                item.IsSelected = true;
            UpdateSelectedCount();
        }

        [RelayCommand]
        private void DeselectAll()
        {
            foreach (var item in FilteredCards)
                item.IsSelected = false;
            UpdateSelectedCount();
        }

        [RelayCommand]
        private void UpdateSelectedCount()
        {
            SelectedCount = FilteredCards.Count(c => c.IsSelected);
        }

        // === Export Commands ===

        [RelayCommand]
        private async Task ExportSelectedCsvAsync()
        {
            var selected = FilteredCards.Where(c => c.IsSelected).Select(c => c.Card).ToList();

            if (selected.Count == 0)
            {
                ExportError = "No cards selected. Use the checkboxes to select cards for export.";
                return;
            }

            var warnings = new List<string>();
            foreach (var card in selected)
            {
                var errors = _exportService.ValidateCardForExport(card);
                if (errors.Count > 0)
                    warnings.Add($"{card.PlayerName}: {string.Join(", ", errors)}");
            }

            if (warnings.Count > 0)
            {
                ExportError = $"Validation issues: {string.Join("; ", warnings.Take(3))}";
                return;
            }

            var path = await _fileDialogService.SaveCsvFileAsync($"whatnot-export-{DateTime.Now:yyyy-MM-dd}.csv");
            if (path == null) return;

            try
            {
                ExportError = null;
                await _exportService.ExportCsvAsync(selected, path);
                ExportMessage = $"Exported {selected.Count} cards to CSV.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CSV export failed");
                ExportError = $"Export failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task UploadSelectedImagesAsync()
        {
            var needUpload = FilteredCards
                .Where(c => c.IsSelected && !string.IsNullOrEmpty(c.Card.ImagePathFront) && string.IsNullOrEmpty(c.Card.ImageUrl1))
                .Select(c => c.Card)
                .ToList();

            if (needUpload.Count == 0)
            {
                ExportMessage = "No selected cards need image upload.";
                return;
            }

            IsUploading = true;
            UploadTotal = needUpload.Count;
            UploadProgress = 0;
            ExportError = null;
            ExportMessage = null;

            try
            {
                foreach (var card in needUpload)
                {
                    try
                    {
                        var (url1, url2) = await _imageUploadService.UploadCardImagesAsync(
                            card.ImagePathFront!, card.ImagePathBack);

                        if (url1 != null) card.ImageUrl1 = url1;
                        if (url2 != null) card.ImageUrl2 = url2;

                        await _cardRepository.UpdateCardAsync(card);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Image upload failed for {Player}", card.PlayerName);
                        ExportError = $"Upload failed for {card.PlayerName}: {ex.Message}";
                    }

                    UploadProgress++;
                }

                ExportMessage = $"Uploaded {UploadProgress} images.";
            }
            finally
            {
                IsUploading = false;
            }
        }

        private void ApplyFilters()
        {
            var filtered = _allCards.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLower();
                filtered = filtered.Where(c =>
                    c.PlayerName.ToLower().Contains(search) ||
                    (c.Brand ?? "").ToLower().Contains(search) ||
                    (c.Team ?? "").ToLower().Contains(search) ||
                    (c.Manufacturer ?? "").ToLower().Contains(search));
            }

            if (SelectedSport != "All" && Enum.TryParse<Sport>(SelectedSport, out var sport))
                filtered = filtered.Where(c => c.Sport == sport);

            if (SelectedStatus != "All" && Enum.TryParse<CardStatus>(SelectedStatus, out var status))
                filtered = filtered.Where(c => c.Status == status);

            FilteredCards = new ObservableCollection<SelectableCard>(
                filtered.Select(c => new SelectableCard(c)));
            SelectedCount = 0;
        }

        private void UpdateSummary()
        {
            var settings = _settingsService.Load();
            var threshold = DateTime.UtcNow.AddDays(-settings.PriceStalenessThresholdDays);

            TotalCount = _allCards.Count;
            PricedCount = _allCards.Count(c => c.ListingPrice.HasValue && c.ListingPrice > 0);
            NeedsPricingCount = _allCards.Count(c => c.Status == CardStatus.Draft);
            TotalValue = _allCards.Where(c => c.ListingPrice.HasValue).Sum(c => c.ListingPrice!.Value);
            StaleCardCount = _allCards.Count(c =>
                c.Status != CardStatus.Sold &&
                c.Status != CardStatus.Draft &&
                c.PriceDate.HasValue &&
                c.PriceDate.Value < threshold);
        }
    }
}
