using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FlipKit.Desktop.ViewModels
{
    public partial class RepriceViewModel : ViewModelBase
    {
        private readonly ICardRepository _cardRepository;
        private readonly IPricerService _pricerService;
        private readonly IBrowserService _browserService;
        private readonly ISettingsService _settingsService;

        private List<Card> _staleCards = new();
        private int _currentIndex;

        [ObservableProperty] private Card? _currentCard;
        [ObservableProperty] private int _currentPosition;
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private bool _hasCards;

        [ObservableProperty] private decimal? _currentPrice;
        [ObservableProperty] private DateTime? _lastPriceDate;
        [ObservableProperty] private int _daysSincePriced;

        [ObservableProperty] private decimal? _newMarketValue;
        [ObservableProperty] private decimal? _suggestedPrice;
        [ObservableProperty] private decimal? _newListingPrice;
        [ObservableProperty] private decimal? _netAfterFees;
        [ObservableProperty] private string? _statusMessage;

        public RepriceViewModel(
            ICardRepository cardRepository,
            IPricerService pricerService,
            IBrowserService browserService,
            ISettingsService settingsService)
        {
            _cardRepository = cardRepository;
            _pricerService = pricerService;
            _browserService = browserService;
            _settingsService = settingsService;

            LoadStaleCardsAsync();
        }

        partial void OnNewMarketValueChanged(decimal? value)
        {
            if (value.HasValue && CurrentCard != null)
            {
                SuggestedPrice = _pricerService.SuggestPrice(value.Value, CurrentCard);
                NewListingPrice = SuggestedPrice;
            }
        }

        partial void OnNewListingPriceChanged(decimal? value)
        {
            if (value.HasValue)
            {
                var settings = _settingsService.Load();
                NetAfterFees = PriceCalculator.CalculateNet(value.Value, settings.WhatnotFeePercent);
            }
        }

        private async void LoadStaleCardsAsync()
        {
            try
            {
                var settings = _settingsService.Load();
                _staleCards = await _cardRepository.GetStaleCardsAsync(settings.PriceStalenessThresholdDays);
                TotalCount = _staleCards.Count;
                _currentIndex = 0;
                HasCards = _staleCards.Count > 0;

                if (HasCards)
                    ShowCurrentCard();
                else
                    StatusMessage = "No stale cards found. All prices are fresh!";
            }
            catch
            {
                StatusMessage = "Failed to load stale cards.";
            }
        }

        private void ShowCurrentCard()
        {
            if (_currentIndex < 0 || _currentIndex >= _staleCards.Count) return;

            CurrentCard = _staleCards[_currentIndex];
            CurrentPosition = _currentIndex + 1;
            CurrentPrice = CurrentCard.ListingPrice;
            LastPriceDate = CurrentCard.PriceDate;
            DaysSincePriced = CurrentCard.PriceDate.HasValue
                ? (int)(DateTime.UtcNow - CurrentCard.PriceDate.Value).TotalDays
                : 0;
            NewMarketValue = null;
            NewListingPrice = null;
            SuggestedPrice = null;
            NetAfterFees = null;
            StatusMessage = null;
        }

        [RelayCommand]
        private void OpenTerapeak()
        {
            if (CurrentCard != null)
                _browserService.OpenUrl(_pricerService.BuildTerapeakUrl(CurrentCard));
        }

        [RelayCommand]
        private void OpenEbaySold()
        {
            if (CurrentCard != null)
                _browserService.OpenUrl(_pricerService.BuildEbaySoldUrl(CurrentCard));
        }

        [RelayCommand]
        private async Task KeepCurrentPriceAsync()
        {
            if (CurrentCard == null) return;

            // Reset the price date to now (keeps the price, resets the clock)
            CurrentCard.PriceDate = DateTime.UtcNow;
            CurrentCard.PriceCheckCount++;
            await _cardRepository.UpdateCardAsync(CurrentCard);

            await _cardRepository.AddPriceHistoryAsync(new PriceHistory
            {
                CardId = CurrentCard.Id,
                EstimatedValue = CurrentCard.EstimatedValue,
                ListingPrice = CurrentCard.ListingPrice,
                PriceSource = "Kept current",
                Notes = "Price unchanged during reprice"
            });

            AdvanceToNext();
        }

        [RelayCommand]
        private async Task SaveNewPriceAsync()
        {
            if (CurrentCard == null || !NewListingPrice.HasValue) return;

            CurrentCard.EstimatedValue = NewMarketValue;
            CurrentCard.ListingPrice = NewListingPrice;
            CurrentCard.PriceDate = DateTime.UtcNow;
            CurrentCard.PriceCheckCount++;
            CurrentCard.PriceSource = "Terapeak";
            await _cardRepository.UpdateCardAsync(CurrentCard);

            await _cardRepository.AddPriceHistoryAsync(new PriceHistory
            {
                CardId = CurrentCard.Id,
                EstimatedValue = NewMarketValue,
                ListingPrice = NewListingPrice,
                PriceSource = "Terapeak"
            });

            AdvanceToNext();
        }

        [RelayCommand]
        private void Skip()
        {
            if (_staleCards.Count == 0) return;
            _currentIndex = (_currentIndex + 1) % _staleCards.Count;
            ShowCurrentCard();
        }

        private void AdvanceToNext()
        {
            _staleCards.RemoveAt(_currentIndex);
            TotalCount = _staleCards.Count;

            if (_staleCards.Count == 0)
            {
                HasCards = false;
                CurrentCard = null;
                StatusMessage = "All stale cards repriced!";
                return;
            }

            if (_currentIndex >= _staleCards.Count)
                _currentIndex = 0;

            ShowCurrentCard();
        }
    }
}
