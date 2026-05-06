using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FlipKit.Desktop.ViewModels
{
    public partial class EbayPublishViewModel : ViewModelBase
    {
        private readonly ICardRepository _cardRepository;
        private readonly IEbayPublishingService _ebayPublishingService;
        private readonly ISettingsService _settingsService;

        [ObservableProperty] private string? _statusMessage;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private bool _isPublishing;
        [ObservableProperty] private string _progressText = string.Empty;
        [ObservableProperty] private bool _isConnected;

        public ObservableCollection<EbayPublishItem> Items { get; } = new();
        public ObservableCollection<EbayPublishResult_> Results { get; } = new();

        public int SelectedCount => Items.Count(i => i.IsSelected);
        public bool HasSelection => SelectedCount > 0;

        public EbayPublishViewModel(
            ICardRepository cardRepository,
            IEbayPublishingService ebayPublishingService,
            ISettingsService settingsService)
        {
            _cardRepository = cardRepository;
            _ebayPublishingService = ebayPublishingService;
            _settingsService = settingsService;

            _ = LoadCardsAsync();
        }

        private async Task LoadCardsAsync()
        {
            try
            {
                IsConnected = _ebayPublishingService.IsAuthorized;
                if (!IsConnected)
                    ErrorMessage = "eBay account not connected. Go to Settings → eBay API Credentials → Connect eBay Account.";

                var all = await _cardRepository.GetAllCardsAsync();
                Items.Clear();

                foreach (var card in all.Where(IsEligible).OrderBy(c => c.PlayerName))
                    Items.Add(new EbayPublishItem(card));

                StatusMessage = $"{Items.Count} eligible card(s) loaded.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading cards: {ex.Message}";
            }
        }

        private static bool IsEligible(Card c) =>
            c.ListingPrice.HasValue && c.ListingPrice > 0
            && !string.IsNullOrEmpty(c.ImageUrl1)
            && !string.IsNullOrEmpty(c.Sku);

        [RelayCommand]
        private void ToggleSelectAll()
        {
            var anySelected = Items.Any(i => i.IsSelected);
            foreach (var item in Items)
                item.IsSelected = !anySelected;
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
        }

        partial void OnIsPublishingChanged(bool value)
        {
            OnPropertyChanged(nameof(HasSelection));
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            ErrorMessage = null;
            StatusMessage = null;
            await LoadCardsAsync();
        }

        [RelayCommand(CanExecute = nameof(CanPublish))]
        private async Task PublishSelectedAsync()
        {
            var toPublish = Items.Where(i => i.IsSelected).ToList();
            if (toPublish.Count == 0) return;

            IsPublishing = true;
            ErrorMessage = null;
            Results.Clear();

            int done = 0;
            foreach (var item in toPublish)
            {
                done++;
                ProgressText = $"Publishing {done} of {toPublish.Count}…";
                item.Status = "Publishing…";

                var result = await _ebayPublishingService.PublishListingAsync(item.Card);
                item.Status = result.Success ? $"Listed ✓ ({result.ListingId})" : $"Failed: {result.ErrorMessage}";
                item.ListingUrl = result.ListingUrl;

                Results.Add(new EbayPublishResult_
                {
                    CardName = item.DisplayName,
                    Status = item.Status,
                    ListingUrl = result.ListingUrl
                });
            }

            IsPublishing = false;
            ProgressText = string.Empty;
            StatusMessage = $"Done. {toPublish.Count(i => i.Status.StartsWith("Listed"))} listed, {toPublish.Count(i => i.Status.StartsWith("Failed"))} failed.";
        }

        private bool CanPublish() => !IsPublishing && HasSelection;
    }

    public partial class EbayPublishItem : ObservableObject
    {
        public Card Card { get; }
        public string DisplayName { get; }

        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private string _status = "Ready";
        [ObservableProperty] private string? _listingUrl;

        public EbayPublishItem(Card card)
        {
            Card = card;
            DisplayName = $"{card.Year} {card.Brand ?? card.Manufacturer} {card.PlayerName} — {card.Sku}";
            if (!string.IsNullOrEmpty(card.ParallelName)) DisplayName += $" ({card.ParallelName})";
        }
    }

    public class EbayPublishResult_
    {
        public string CardName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ListingUrl { get; set; }
    }
}
