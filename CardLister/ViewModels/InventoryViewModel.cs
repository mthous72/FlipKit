using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Helpers;
using FlipKit.Core.Services;
using FlipKit.Desktop.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace FlipKit.Desktop.ViewModels
{
    public partial class InventoryViewModel : ViewModelBase
    {
        private readonly ICardRepository _cardRepository;
        private readonly ISettingsService _settingsService;
        private readonly IExportService _exportService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IImageUploadService _imageUploadService;
        private readonly IBrowserService _browserService;
        private readonly INavigationService _navigationService;
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
        [ObservableProperty] private int _deleteCount;
        [ObservableProperty] private string _deleteConfirmMessage = string.Empty;

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

        // Edit Panel (side panel for quick editing)
        [ObservableProperty] private bool _isEditPanelOpen;
        [ObservableProperty] private CardDetailViewModel? _editingCard;
        [ObservableProperty] private string? _editSuccessMessage;
        [ObservableProperty] private string? _editErrorMessage;

        public Card? SelectedCard => SelectedItem?.Card ?? FilteredCards.FirstOrDefault(c => c.IsSelected)?.Card;
        public bool HasSelectedItem => SelectedItem != null || FilteredCards.Any(c => c.IsSelected);

        public List<string> SportOptions { get; } = new() { "All", "Football", "Baseball", "Basketball" };
        public List<string> StatusOptions { get; } = new() { "All", "Draft", "Priced", "Ready", "Listed", "Sold" };

        public InventoryViewModel(
            ICardRepository cardRepository,
            ISettingsService settingsService,
            IExportService exportService,
            IFileDialogService fileDialogService,
            IImageUploadService imageUploadService,
            IBrowserService browserService,
            INavigationService navigationService,
            ILogger<InventoryViewModel> logger)
        {
            _cardRepository = cardRepository;
            _settingsService = settingsService;
            _exportService = exportService;
            _navigationService = navigationService;
            _fileDialogService = fileDialogService;
            _imageUploadService = imageUploadService;
            _browserService = browserService;
            _logger = logger;

            LoadCardsAsync();
        }

        partial void OnSelectedItemChanged(SelectableCard? value)
        {
            OnPropertyChanged(nameof(SelectedCard));
            OnPropertyChanged(nameof(HasSelectedItem));
            EditSelectedCommand.NotifyCanExecuteChanged();
            RequestDeleteSelectedCommand.NotifyCanExecuteChanged();
            OpenSoldDialogCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(HasSelectedItem))]
        private void EditSelected()
        {
            if (SelectedCard == null) return;

            // Open edit panel instead of navigating away
            EditingCard = CardDetailViewModel.FromCard(SelectedCard);
            MergeCustomGradingCompanies(EditingCard);
            IsEditPanelOpen = true;
            EditSuccessMessage = null;
            EditErrorMessage = null;
        }

        [RelayCommand]
        private async Task SaveEditAsync()
        {
            if (EditingCard == null || SelectedCard == null) return;

            var savedCardId = SelectedCard.Id;

            try
            {
                var card = EditingCard.ToCard();
                card.Id = SelectedCard.Id; // Preserve ID
                card.ImagePathFront = SelectedCard.ImagePathFront;
                card.ImagePathBack = SelectedCard.ImagePathBack;
                card.CreatedAt = SelectedCard.CreatedAt;
                card.UpdatedAt = DateTime.UtcNow;

                await _cardRepository.UpdateCardAsync(card);

                // Reload cards to update the display (maintains selection)
                LoadCardsAsync();

                // Restore selection to the saved card (preserves scroll position)
                // Give LoadCardsAsync a moment to complete
                await Task.Delay(100);
                var updatedCard = FilteredCards.FirstOrDefault(sc => sc.Card.Id == savedCardId);
                if (updatedCard != null)
                {
                    SelectedItem = updatedCard;
                }

                EditSuccessMessage = $"✓ Saved {card.PlayerName}";
                EditErrorMessage = null;

                // Keep panel open for rapid editing, but clear success message after delay
                _ = Task.Delay(2000).ContinueWith(_ => EditSuccessMessage = null);

                _logger.LogInformation("Updated card {CardId} from edit panel", card.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save card edit");
                EditErrorMessage = $"Save failed: {ex.Message}";
                EditSuccessMessage = null;
            }
        }

        [RelayCommand]
        private void CloseEditPanel()
        {
            IsEditPanelOpen = false;
            EditingCard = null;
            EditSuccessMessage = null;
            EditErrorMessage = null;
        }

        [RelayCommand]
        private async Task OpenFullEditAsync()
        {
            if (SelectedCard == null) return;

            // Close quick edit panel
            IsEditPanelOpen = false;

            // Navigate to full edit view
            await _navigationService.NavigateToEditCardAsync(SelectedCard.Id);
        }

        private void MergeCustomGradingCompanies(CardDetailViewModel vm)
        {
            var settings = _settingsService.Load();
            foreach (var custom in settings.CustomGradingCompanies)
            {
                if (!vm.GradingCompanyOptions.Contains(custom, StringComparer.OrdinalIgnoreCase))
                    vm.GradingCompanyOptions.Add(custom);
            }
        }

        [RelayCommand]
        private async Task NavigateToReprice()
        {
            await _navigationService.NavigateToRepriceAsync();
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

        [RelayCommand(CanExecute = nameof(HasSelectedItem))]
        private void RequestDeleteSelected()
        {
            var cardsToDelete = GetCardsToDelete();
            if (cardsToDelete.Count == 0) return;
            DeleteCount = cardsToDelete.Count;

            if (DeleteCount == 1)
                DeleteConfirmMessage = $"Are you sure you want to delete {SelectedCard?.PlayerName ?? "this card"}? This cannot be undone.";
            else
                DeleteConfirmMessage = $"Are you sure you want to delete {DeleteCount} cards? This cannot be undone.";

            ShowDeleteConfirmDialog = true;
        }

        private List<SelectableCard> GetCardsToDelete()
        {
            // If row is selected, delete just that one
            if (SelectedItem != null)
                return new List<SelectableCard> { SelectedItem };

            // Otherwise delete all checked cards
            return FilteredCards.Where(c => c.IsSelected).ToList();
        }

        [RelayCommand]
        private void CancelDelete()
        {
            ShowDeleteConfirmDialog = false;
        }

        [RelayCommand]
        private async Task ConfirmDeleteAsync()
        {
            var itemsToDelete = GetCardsToDelete();
            if (itemsToDelete.Count == 0) return;

            var deletedCount = 0;
            var failedCount = 0;

            try
            {
                foreach (var item in itemsToDelete.ToList())
                {
                    try
                    {
                        await _cardRepository.DeleteCardAsync(item.Card.Id);

                        var cardToRemove = _allCards.FirstOrDefault(c => c.Id == item.Card.Id);
                        if (cardToRemove != null)
                            _allCards.Remove(cardToRemove);

                        FilteredCards.Remove(item);
                        deletedCount++;
                        _logger.LogInformation("Deleted card {CardId}: {PlayerName}", item.Card.Id, item.Card.PlayerName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete card {CardId}", item.Card.Id);
                        failedCount++;
                    }
                }

                SelectedItem = null;
                ShowDeleteConfirmDialog = false;
                UpdateSummary();

                if (failedCount > 0)
                    ExportError = $"Deleted {deletedCount} cards, {failedCount} failed";
                else
                    ExportMessage = $"Deleted {deletedCount} card(s) successfully";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete cards");
                ShowDeleteConfirmDialog = false;
                ExportError = $"Failed to delete cards: {ex.Message}";
            }
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

        [RelayCommand(CanExecute = nameof(HasSelectedItem))]
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

        [RelayCommand(CanExecute = nameof(HasSelectedItem))]
        private async Task RepriceSelectedAsync()
        {
            if (SelectedCard == null) return;

            try
            {
                // Reset card to Draft status for repricing
                SelectedCard.Status = CardStatus.Draft;

                // Clear pricing data to force fresh lookup
                SelectedCard.EstimatedValue = null;
                SelectedCard.ListingPrice = null;
                SelectedCard.PriceSource = null;
                SelectedCard.PriceDate = null;

                await _cardRepository.UpdateCardAsync(SelectedCard);

                _logger.LogInformation("Card {CardId} ({Player}) moved to Draft for repricing",
                    SelectedCard.Id, SelectedCard.PlayerName);

                ExportMessage = $"Card moved to pricing queue. Go to Pricing tab to reprice.";

                // Refresh the view
                ApplyFilters();
                UpdateSummary();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reprice card {CardId}", SelectedCard.Id);
                ExportError = $"Failed to move card to pricing: {ex.Message}";
            }
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
            OnPropertyChanged(nameof(SelectedCard));
            OnPropertyChanged(nameof(HasSelectedItem));
            EditSelectedCommand.NotifyCanExecuteChanged();
            RequestDeleteSelectedCommand.NotifyCanExecuteChanged();
            OpenSoldDialogCommand.NotifyCanExecuteChanged();
            RepriceSelectedCommand.NotifyCanExecuteChanged();
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
