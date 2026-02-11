using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FlipKit.Desktop.ViewModels
{
    public partial class PricingViewModel : ViewModelBase
    {
        private readonly ICardRepository _cardRepository;
        private readonly IPricerService _pricerService;
        private readonly IBrowserService _browserService;
        private readonly ISettingsService _settingsService;
        // SHELVED: ISoldPriceService _soldPriceService (kept for potential future use)

        private List<Card> _unpricedCards = new();
        private int _currentIndex;

        [ObservableProperty] private Card? _currentCard;
        [ObservableProperty] private int _currentPosition;
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private decimal? _marketValue;
        [ObservableProperty] private decimal? _suggestedPrice;
        [ObservableProperty] private decimal? _listingPrice;
        [ObservableProperty] private decimal? _netAfterFees;
        [ObservableProperty] private decimal? _costBasis;
        [ObservableProperty] private CostSource? _costSource;
        [ObservableProperty] private string? _costNotes;
        [ObservableProperty] private string? _statusMessage;
        [ObservableProperty] private bool _hasCards;

        // SHELVED: Automated pricing properties (kept for potential future use)
        // [ObservableProperty] private bool _isLookingUpPrice;
        // [ObservableProperty] private PriceLookupResult? _lookupResult;
        // [ObservableProperty] private string _automatedStatusMessage = "";

        public PricingViewModel(
            ICardRepository cardRepository,
            IPricerService pricerService,
            IBrowserService browserService,
            ISettingsService settingsService)
        {
            _cardRepository = cardRepository;
            _pricerService = pricerService;
            _browserService = browserService;
            _settingsService = settingsService;

            LoadUnpricedAsync();
        }

        partial void OnMarketValueChanged(decimal? value)
        {
            if (value.HasValue && CurrentCard != null)
            {
                SuggestedPrice = _pricerService.SuggestPrice(value.Value, CurrentCard);
                ListingPrice = SuggestedPrice;
            }
            else
            {
                SuggestedPrice = null;
                ListingPrice = null;
            }
        }

        partial void OnListingPriceChanged(decimal? value)
        {
            if (value.HasValue)
            {
                var settings = _settingsService.Load();
                NetAfterFees = PriceCalculator.CalculateNet(value.Value, settings.WhatnotFeePercent);
            }
            else
            {
                NetAfterFees = null;
            }
        }

        private async void LoadUnpricedAsync()
        {
            try
            {
                _unpricedCards = await _cardRepository.GetAllCardsAsync(CardStatus.Draft);
                TotalCount = _unpricedCards.Count;
                _currentIndex = 0;
                HasCards = _unpricedCards.Count > 0;

                if (HasCards)
                    ShowCurrentCard();
                else
                    StatusMessage = "No cards need pricing. Scan some cards first!";
            }
            catch
            {
                StatusMessage = "Failed to load cards.";
            }
        }

        private void ShowCurrentCard()
        {
            if (_currentIndex >= 0 && _currentIndex < _unpricedCards.Count)
            {
                CurrentCard = _unpricedCards[_currentIndex];
                CurrentPosition = _currentIndex + 1;
                MarketValue = CurrentCard.EstimatedValue;
                ListingPrice = CurrentCard.ListingPrice;
                CostBasis = CurrentCard.CostBasis;
                CostSource = CurrentCard.CostSource;
                CostNotes = CurrentCard.CostNotes;
                StatusMessage = null;
            }
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

        // SHELVED: Automated pricing via 130point scraping
        // Keeping infrastructure in place for potential future use
        // [RelayCommand]
        // private async Task GetMarketPriceAsync()
        // {
        //     ... (code commented out)
        // }

        [RelayCommand]
        private async Task SaveAndNextAsync()
        {
            if (CurrentCard == null || !ListingPrice.HasValue) return;

            CurrentCard.EstimatedValue = MarketValue;
            CurrentCard.ListingPrice = ListingPrice;
            CurrentCard.PriceSource = "Terapeak/eBay";
            CurrentCard.PriceDate = DateTime.UtcNow;
            CurrentCard.PriceCheckCount++;
            CurrentCard.Status = CardStatus.Priced;
            CurrentCard.CostBasis = CostBasis;
            CurrentCard.CostSource = CostSource;
            CurrentCard.CostNotes = CostNotes;

            await _cardRepository.UpdateCardAsync(CurrentCard);

            await _cardRepository.AddPriceHistoryAsync(new PriceHistory
            {
                CardId = CurrentCard.Id,
                EstimatedValue = MarketValue,
                ListingPrice = ListingPrice,
                PriceSource = "Terapeak/eBay"
            });

            _unpricedCards.RemoveAt(_currentIndex);
            TotalCount = _unpricedCards.Count;

            if (_unpricedCards.Count == 0)
            {
                HasCards = false;
                CurrentCard = null;
                StatusMessage = "All cards priced!";
                return;
            }

            if (_currentIndex >= _unpricedCards.Count)
                _currentIndex = _unpricedCards.Count - 1;

            ShowCurrentCard();
        }

        [RelayCommand]
        private void Skip()
        {
            if (_unpricedCards.Count == 0) return;

            _currentIndex = (_currentIndex + 1) % _unpricedCards.Count;
            ShowCurrentCard();
        }

        [RelayCommand]
        private void Previous()
        {
            if (_unpricedCards.Count == 0) return;

            _currentIndex = (_currentIndex - 1 + _unpricedCards.Count) % _unpricedCards.Count;
            ShowCurrentCard();
        }
    }
}
