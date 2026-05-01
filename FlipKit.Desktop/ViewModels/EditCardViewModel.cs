using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace FlipKit.Desktop.ViewModels
{
    public partial class EditCardViewModel : ViewModelBase
    {
        private readonly ICardRepository _cardRepository;
        private readonly INavigationService _navigationService;
        private readonly IFileDialogService _fileDialogService;
        private readonly ILogger<EditCardViewModel> _logger;

        private Card? _originalCard;

        [ObservableProperty] private CardDetailViewModel? _cardDetail;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private string? _successMessage;
        [ObservableProperty] private bool _isLoading;

        // Image previews from the original card
        [ObservableProperty] private string? _imagePathFront;
        [ObservableProperty] private string? _imagePathBack;
        [ObservableProperty] private string? _imageUrl1;
        [ObservableProperty] private string? _imageUrl2;

        // Prefer ImgBB URLs (used for Whatnot) over local paths
        public string? DisplayImageFront => !string.IsNullOrEmpty(ImageUrl1) ? ImageUrl1 : ImagePathFront;
        public string? DisplayImageBack => !string.IsNullOrEmpty(ImageUrl2) ? ImageUrl2 : ImagePathBack;

        // Additional photos (slots 3-8) — uploaded to ImgBB at export time but never sent to the LLM.
        public ObservableCollection<PhotoSlot> AdditionalPhotos { get; } = new();
        public const int MaxAdditionalPhotos = 6;

        public EditCardViewModel(
            ICardRepository cardRepository,
            INavigationService navigationService,
            IFileDialogService fileDialogService,
            ILogger<EditCardViewModel> logger)
        {
            _cardRepository = cardRepository;
            _navigationService = navigationService;
            _fileDialogService = fileDialogService;
            _logger = logger;
        }

        public async Task LoadCardAsync(int cardId)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                _originalCard = await _cardRepository.GetCardAsync(cardId);
                if (_originalCard == null)
                {
                    ErrorMessage = "Card not found.";
                    return;
                }

                CardDetail = CardDetailViewModel.FromCard(_originalCard);
                ImagePathFront = _originalCard.ImagePathFront;
                ImagePathBack = _originalCard.ImagePathBack;
                ImageUrl1 = _originalCard.ImageUrl1;
                ImageUrl2 = _originalCard.ImageUrl2;

                AdditionalPhotos.Clear();
                AddSlotIfAny(_originalCard.ImagePath3, _originalCard.ImageUrl3);
                AddSlotIfAny(_originalCard.ImagePath4, _originalCard.ImageUrl4);
                AddSlotIfAny(_originalCard.ImagePath5, _originalCard.ImageUrl5);
                AddSlotIfAny(_originalCard.ImagePath6, _originalCard.ImageUrl6);
                AddSlotIfAny(_originalCard.ImagePath7, _originalCard.ImageUrl7);
                AddSlotIfAny(_originalCard.ImagePath8, _originalCard.ImageUrl8);

                // Notify that display properties changed
                OnPropertyChanged(nameof(DisplayImageFront));
                OnPropertyChanged(nameof(DisplayImageBack));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load card {CardId} for editing", cardId);
                ErrorMessage = $"Failed to load card: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task AddAdditionalPhotoAsync()
        {
            if (AdditionalPhotos.Count >= MaxAdditionalPhotos)
                return;

            var path = await _fileDialogService.OpenImageFileAsync();
            if (!string.IsNullOrEmpty(path))
                AdditionalPhotos.Add(new PhotoSlot(path));
        }

        [RelayCommand]
        private void RemoveAdditionalPhoto(PhotoSlot? slot)
        {
            if (slot != null)
                AdditionalPhotos.Remove(slot);
        }

        private void AddSlotIfAny(string? path, string? url)
        {
            if (!string.IsNullOrEmpty(path) || !string.IsNullOrEmpty(url))
                AdditionalPhotos.Add(new PhotoSlot(path, url));
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (CardDetail == null || _originalCard == null)
                return;

            try
            {
                ErrorMessage = null;
                SuccessMessage = null;

                // Update the tracked entity's properties instead of creating a new instance
                _originalCard.PlayerName = CardDetail.PlayerName ?? string.Empty;
                _originalCard.Sport = CardDetail.Sport;
                _originalCard.Brand = CardDetail.Brand;
                _originalCard.Manufacturer = CardDetail.Manufacturer;
                _originalCard.Year = CardDetail.Year;
                _originalCard.CardNumber = CardDetail.CardNumber;
                _originalCard.Team = CardDetail.Team;
                _originalCard.SetName = CardDetail.SetName;
                _originalCard.VariationType = CardDetail.VariationType;
                _originalCard.ParallelName = CardDetail.ParallelName;
                _originalCard.SerialNumbered = CardDetail.SerialNumbered;
                _originalCard.IsShortPrint = CardDetail.IsShortPrint;
                _originalCard.IsSSP = CardDetail.IsSSP;
                _originalCard.IsRookie = CardDetail.IsRookie;
                _originalCard.IsAuto = CardDetail.IsAuto;
                _originalCard.IsRelic = CardDetail.IsRelic;
                _originalCard.Condition = CardDetail.Condition;
                _originalCard.IsGraded = CardDetail.IsGraded;
                _originalCard.GradeCompany = CardDetail.GradeCompany;
                _originalCard.GradeValue = CardDetail.GradeValue;
                _originalCard.CertNumber = CardDetail.CertNumber;
                _originalCard.AutoGrade = CardDetail.AutoGrade;
                _originalCard.CostBasis = CardDetail.CostBasis;
                _originalCard.CostSource = CardDetail.CostSource;
                _originalCard.CostDate = CardDetail.CostDate;
                _originalCard.CostNotes = CardDetail.CostNotes;
                _originalCard.Quantity = CardDetail.Quantity;
                _originalCard.ListingType = CardDetail.ListingType;
                _originalCard.Offerable = CardDetail.Offerable;
                _originalCard.ShippingProfile = CardDetail.ShippingProfile;
                _originalCard.WhatnotCategory = CardDetail.WhatnotCategory;
                _originalCard.WhatnotSubcategory = CardDetail.WhatnotSubcategory;
                _originalCard.Notes = CardDetail.Notes;
                _originalCard.UpdatedAt = DateTime.UtcNow;

                // Sync slots 3-8 from the AdditionalPhotos collection. Slots beyond the
                // current collection size are cleared (handles user removing a photo).
                ApplyAdditionalPhotosToCard(_originalCard);

                await _cardRepository.UpdateCardAsync(_originalCard);

                _logger.LogInformation("Card {CardId} updated: {PlayerName}", _originalCard.Id, _originalCard.PlayerName);

                // Navigate back to Inventory
                await _navigationService.NavigateToInventoryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save card {CardId}", _originalCard?.Id);
                ErrorMessage = $"Failed to save: {ex.Message}";
            }
        }

        private void ApplyAdditionalPhotosToCard(Card card)
        {
            for (int slotIdx = 0; slotIdx < MaxAdditionalPhotos; slotIdx++)
            {
                var path = slotIdx < AdditionalPhotos.Count ? AdditionalPhotos[slotIdx].Path : null;
                var url = slotIdx < AdditionalPhotos.Count ? AdditionalPhotos[slotIdx].Url : null;

                switch (slotIdx + 3)
                {
                    case 3: card.ImagePath3 = path; card.ImageUrl3 = url; break;
                    case 4: card.ImagePath4 = path; card.ImageUrl4 = url; break;
                    case 5: card.ImagePath5 = path; card.ImageUrl5 = url; break;
                    case 6: card.ImagePath6 = path; card.ImageUrl6 = url; break;
                    case 7: card.ImagePath7 = path; card.ImageUrl7 = url; break;
                    case 8: card.ImagePath8 = path; card.ImageUrl8 = url; break;
                }
            }
        }

        [RelayCommand]
        private async Task Cancel()
        {
            await _navigationService.NavigateToInventoryAsync();
        }
    }
}
