using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardLister.Models;
using CardLister.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CardLister.ViewModels
{
    public partial class BulkScanViewModel : ViewModelBase
    {
        private readonly IScannerService _scannerService;
        private readonly ICardRepository _cardRepository;
        private readonly IFileDialogService _fileDialogService;
        private readonly ISettingsService _settingsService;

        private CancellationTokenSource? _scanCts;

        [ObservableProperty] private bool _imagesArePairs = true;
        [ObservableProperty] private bool _isScanning;
        [ObservableProperty] private bool _isSaving;
        [ObservableProperty] private int _scanProgress;
        [ObservableProperty] private int _scanTotal;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private string? _successMessage;
        [ObservableProperty] private BulkScanItem? _selectedItem;

        public ObservableCollection<BulkScanItem> Items { get; } = new();

        public CardDetailViewModel? SelectedCard => SelectedItem?.CardDetail;

        partial void OnSelectedItemChanged(BulkScanItem? value)
        {
            OnPropertyChanged(nameof(SelectedCard));
        }

        public BulkScanViewModel(
            IScannerService scannerService,
            ICardRepository cardRepository,
            IFileDialogService fileDialogService,
            ISettingsService settingsService)
        {
            _scannerService = scannerService;
            _cardRepository = cardRepository;
            _fileDialogService = fileDialogService;
            _settingsService = settingsService;
        }

        [RelayCommand]
        private async Task SelectImagesAsync()
        {
            var paths = await _fileDialogService.OpenImageFilesAsync();
            if (paths.Count == 0)
                return;

            ErrorMessage = null;
            SuccessMessage = null;

            paths.Sort(StringComparer.OrdinalIgnoreCase);

            if (ImagesArePairs)
            {
                // Pair consecutive images as front/back
                for (int i = 0; i < paths.Count; i += 2)
                {
                    var item = new BulkScanItem
                    {
                        Index = Items.Count + 1,
                        FrontImagePath = paths[i],
                        BackImagePath = i + 1 < paths.Count ? paths[i + 1] : null,
                        DisplayName = $"Card {Items.Count + 1}"
                    };
                    Items.Add(item);
                }
            }
            else
            {
                // Each image is a separate card (front only)
                foreach (var path in paths)
                {
                    var item = new BulkScanItem
                    {
                        Index = Items.Count + 1,
                        FrontImagePath = path,
                        DisplayName = $"Card {Items.Count + 1}"
                    };
                    Items.Add(item);
                }
            }
        }

        [RelayCommand]
        private async Task ScanAllAsync()
        {
            if (Items.Count == 0)
                return;

            var pending = Items.Where(i => i.Status == BulkScanStatus.Pending).ToList();
            if (pending.Count == 0)
                return;

            IsScanning = true;
            ErrorMessage = null;
            SuccessMessage = null;
            ScanProgress = 0;
            ScanTotal = pending.Count;
            _scanCts = new CancellationTokenSource();

            var settings = _settingsService.Load();

            foreach (var item in pending)
            {
                if (_scanCts.Token.IsCancellationRequested)
                    break;

                item.Status = BulkScanStatus.Scanning;

                try
                {
                    var scanResult = await _scannerService.ScanCardAsync(
                        item.FrontImagePath, item.BackImagePath, settings.DefaultModel);

                    scanResult.Card.ImagePathFront = item.FrontImagePath;
                    if (!string.IsNullOrEmpty(item.BackImagePath))
                        scanResult.Card.ImagePathBack = item.BackImagePath;

                    item.CardDetail = CardDetailViewModel.FromCard(scanResult.Card);
                    item.DisplayName = !string.IsNullOrEmpty(scanResult.Card.PlayerName)
                        ? scanResult.Card.PlayerName
                        : $"Card {item.Index}";
                    item.Status = BulkScanStatus.Scanned;
                }
                catch (Exception ex)
                {
                    item.Status = BulkScanStatus.Error;
                    item.ErrorMessage = ex.Message;
                }

                ScanProgress++;
            }

            IsScanning = false;
            _scanCts = null;

            var scanned = Items.Count(i => i.Status == BulkScanStatus.Scanned);
            var errors = Items.Count(i => i.Status == BulkScanStatus.Error);

            if (errors > 0)
                ErrorMessage = $"Scanned {scanned} cards, {errors} failed";
            else
                SuccessMessage = $"Scanned {scanned} cards successfully";
        }

        [RelayCommand]
        private void CancelScan()
        {
            _scanCts?.Cancel();
        }

        [RelayCommand]
        private async Task SaveAllAsync()
        {
            var ready = Items.Where(i => i.Status == BulkScanStatus.Scanned && i.CardDetail != null).ToList();
            if (ready.Count == 0)
                return;

            IsSaving = true;
            ErrorMessage = null;
            SuccessMessage = null;
            int saved = 0;

            foreach (var item in ready)
            {
                try
                {
                    var card = item.CardDetail!.ToCard();
                    card.ImagePathFront = item.FrontImagePath;
                    card.ImagePathBack = item.BackImagePath;
                    card.Status = Models.Enums.CardStatus.Draft;
                    await _cardRepository.InsertCardAsync(card);

                    item.Status = BulkScanStatus.Saved;
                    saved++;
                }
                catch (Exception ex)
                {
                    item.Status = BulkScanStatus.Error;
                    item.ErrorMessage = ex.Message;
                }
            }

            IsSaving = false;
            SuccessMessage = $"Saved {saved} cards to My Cards!";
        }

        [RelayCommand]
        private void RemoveSelected()
        {
            if (SelectedItem == null)
                return;

            Items.Remove(SelectedItem);
            SelectedItem = null;

            // Re-index
            for (int i = 0; i < Items.Count; i++)
                Items[i].Index = i + 1;
        }

        [RelayCommand]
        private void ClearAll()
        {
            Items.Clear();
            SelectedItem = null;
            ErrorMessage = null;
            SuccessMessage = null;
            ScanProgress = 0;
            ScanTotal = 0;
        }
    }

    public enum BulkScanStatus
    {
        Pending,
        Scanning,
        Scanned,
        Saved,
        Error
    }

    public partial class BulkScanItem : ObservableObject
    {
        [ObservableProperty] private int _index;
        [ObservableProperty] private string _displayName = "Pending";
        [ObservableProperty] private BulkScanStatus _status = BulkScanStatus.Pending;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private CardDetailViewModel? _cardDetail;

        public string FrontImagePath { get; set; } = string.Empty;
        public string? BackImagePath { get; set; }
    }
}
