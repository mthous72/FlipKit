using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FlipKit.Desktop.ViewModels
{
    public partial class ReportsViewModel : ViewModelBase
    {
        private readonly ICardRepository _cardRepository;
        private readonly IExportService _exportService;
        private readonly IFileDialogService _fileDialogService;

        [ObservableProperty] private DateTimeOffset? _startDate = new DateTimeOffset(new DateTime(DateTime.UtcNow.Year, 1, 1));
        [ObservableProperty] private DateTimeOffset? _endDate = new DateTimeOffset(DateTime.UtcNow);

        // Summary
        [ObservableProperty] private int _cardsSold;
        [ObservableProperty] private decimal _totalRevenue;
        [ObservableProperty] private decimal _totalCostBasis;
        [ObservableProperty] private decimal _totalFees;
        [ObservableProperty] private decimal _totalShipping;
        [ObservableProperty] private decimal _netProfit;

        [ObservableProperty] private ObservableCollection<MonthlyBreakdown> _monthlyData = new();
        [ObservableProperty] private ObservableCollection<TopSeller> _topSellers = new();
        [ObservableProperty] private string? _statusMessage;

        private List<Card> _soldCards = new();

        public ReportsViewModel(ICardRepository cardRepository, IExportService exportService, IFileDialogService fileDialogService)
        {
            _cardRepository = cardRepository;
            _exportService = exportService;
            _fileDialogService = fileDialogService;

            _ = LoadReportAsync();
        }

        [RelayCommand]
        private async Task LoadReportAsync()
        {
            try
            {
                var allCards = await _cardRepository.GetAllCardsAsync(CardStatus.Sold);
                var start = StartDate?.DateTime ?? new DateTime(DateTime.UtcNow.Year, 1, 1);
                var end = EndDate?.DateTime.AddDays(1) ?? DateTime.UtcNow.AddDays(1);
                _soldCards = allCards.Where(c =>
                    c.SaleDate.HasValue &&
                    c.SaleDate.Value >= start &&
                    c.SaleDate.Value <= end)
                    .ToList();

                CardsSold = _soldCards.Count;
                TotalRevenue = _soldCards.Sum(c => c.SalePrice ?? 0);
                TotalCostBasis = _soldCards.Sum(c => c.CostBasis ?? 0);
                TotalFees = _soldCards.Sum(c => c.FeesPaid ?? 0);
                TotalShipping = _soldCards.Sum(c => c.ShippingCost ?? 0);
                NetProfit = _soldCards.Sum(c => c.NetProfit ?? 0);

                // Monthly breakdown
                var monthly = _soldCards
                    .Where(c => c.SaleDate.HasValue)
                    .GroupBy(c => new { c.SaleDate!.Value.Year, c.SaleDate!.Value.Month })
                    .Select(g => new MonthlyBreakdown
                    {
                        MonthName = $"{g.Key.Year}-{g.Key.Month:D2}",
                        CardsSold = g.Count(),
                        Revenue = g.Sum(c => c.SalePrice ?? 0),
                        Profit = g.Sum(c => c.NetProfit ?? 0)
                    })
                    .OrderBy(m => m.MonthName);

                MonthlyData = new ObservableCollection<MonthlyBreakdown>(monthly);

                // Top sellers
                var top = _soldCards
                    .OrderByDescending(c => c.NetProfit ?? 0)
                    .Take(10)
                    .Select(c => new TopSeller
                    {
                        Description = $"{c.Year} {c.Brand} {c.PlayerName}",
                        SalePrice = c.SalePrice ?? 0,
                        Profit = c.NetProfit ?? 0
                    });

                TopSellers = new ObservableCollection<TopSeller>(top);
                StatusMessage = null;
            }
            catch
            {
                StatusMessage = "Failed to load report data.";
            }
        }

        [RelayCommand]
        private async Task ExportTaxCsvAsync()
        {
            if (_soldCards.Count == 0)
            {
                StatusMessage = "No sold cards to export.";
                return;
            }

            var path = await _fileDialogService.SaveCsvFileAsync($"tax-report-{DateTime.Now:yyyy-MM-dd}.csv");
            if (path == null) return;

            await _exportService.ExportTaxCsvAsync(_soldCards, path);
            StatusMessage = $"Exported {_soldCards.Count} records to {path}";
        }
    }

    public class MonthlyBreakdown
    {
        public string MonthName { get; set; } = string.Empty;
        public int CardsSold { get; set; }
        public decimal Revenue { get; set; }
        public decimal Profit { get; set; }
    }

    public class TopSeller
    {
        public string Description { get; set; } = string.Empty;
        public decimal SalePrice { get; set; }
        public decimal Profit { get; set; }
    }
}
