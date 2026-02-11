using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Models;
using FlipKit.Models.Enums;
using FlipKit.Services;

namespace ScreenshotTool
{
    // Mock card repository with pre-loaded sample data
    public class MockCardRepository : ICardRepository
    {
        private readonly List<Card> _cards;
        private int _nextId = 1;

        public MockCardRepository()
        {
            _cards = GenerateSampleCards();
        }

        private List<Card> GenerateSampleCards()
        {
            return new List<Card>
            {
                new Card
                {
                    Id = _nextId++,
                    PlayerName = "CJ Stroud",
                    Year = 2023,
                    Sport = Sport.Football,
                    Manufacturer = "Panini",
                    Brand = "Prizm",
                    SetName = "Prizm",
                    CardNumber = "301",
                    ParallelName = "Silver Prizm",
                    Team = "Houston Texans",
                    IsRookie = true,
                    Status = CardStatus.Draft,
                    ImagePathFront = "/sample/cj-stroud-front.jpg",
                    CreatedAt = DateTime.UtcNow
                },
                new Card
                {
                    Id = _nextId++,
                    PlayerName = "Patrick Mahomes",
                    Year = 2017,
                    Sport = Sport.Football,
                    Manufacturer = "Panini",
                    Brand = "Prizm",
                    SetName = "Prizm",
                    CardNumber = "127",
                    Team = "Kansas City Chiefs",
                    IsRookie = true,
                    IsGraded = true,
                    GradeCompany = "PSA",
                    GradeValue = "10",
                    Status = CardStatus.Priced,
                    EstimatedValue = 850.00m,
                    ListingPrice = 999.00m,
                    PriceSource = "eBay Active (12 comps)",
                    PriceDate = DateTime.UtcNow.AddDays(-5),
                    ImagePathFront = "/sample/mahomes-front.jpg",
                    CreatedAt = DateTime.UtcNow.AddDays(-30)
                },
                new Card
                {
                    Id = _nextId++,
                    PlayerName = "Shohei Ohtani",
                    Year = 2024,
                    Sport = Sport.Baseball,
                    Manufacturer = "Topps",
                    Brand = "Chrome",
                    SetName = "Chrome",
                    CardNumber = "50",
                    ParallelName = "Refractor",
                    SerialNumbered = "/499",
                    Team = "Los Angeles Dodgers",
                    Status = CardStatus.Ready,
                    EstimatedValue = 45.00m,
                    ListingPrice = 54.99m,
                    ImagePathFront = "/sample/ohtani-front.jpg",
                    ImageUrl1 = "https://example.com/ohtani.jpg",
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                },
                new Card
                {
                    Id = _nextId++,
                    PlayerName = "Victor Wembanyama",
                    Year = 2023,
                    Sport = Sport.Basketball,
                    Manufacturer = "Panini",
                    Brand = "Prizm",
                    SetName = "Prizm",
                    CardNumber = "236",
                    Team = "San Antonio Spurs",
                    IsRookie = true,
                    Status = CardStatus.Sold,
                    EstimatedValue = 120.00m,
                    ListingPrice = 139.99m,
                    SalePrice = 135.00m,
                    SalePlatform = "Whatnot",
                    SaleDate = DateTime.UtcNow.AddDays(-2),
                    NetProfit = 105.15m,
                    ImagePathFront = "/sample/wemby-front.jpg",
                    CreatedAt = DateTime.UtcNow.AddDays(-20)
                }
            };
        }

        public Task<List<Card>> GetAllCardsAsync() => Task.FromResult(_cards);

        public Task<List<Card>> GetAllCardsAsync(CardStatus status) =>
            Task.FromResult(_cards.Where(c => c.Status == status).ToList());

        public Task<Card?> GetCardByIdAsync(int id) =>
            Task.FromResult(_cards.FirstOrDefault(c => c.Id == id));

        public Task<Card> AddCardAsync(Card card)
        {
            card.Id = _nextId++;
            _cards.Add(card);
            return Task.FromResult(card);
        }

        public Task UpdateCardAsync(Card card) => Task.CompletedTask;

        public Task DeleteCardAsync(int id)
        {
            var card = _cards.FirstOrDefault(c => c.Id == id);
            if (card != null) _cards.Remove(card);
            return Task.CompletedTask;
        }

        public Task AddPriceHistoryAsync(PriceHistory history) => Task.CompletedTask;

        public Task<List<PriceHistory>> GetPriceHistoryAsync(int cardId) =>
            Task.FromResult(new List<PriceHistory>());
    }

    // Mock settings service
    public class MockSettingsService : ISettingsService
    {
        private readonly AppSettings _settings = new()
        {
            OpenRouterApiKey = "demo-key-not-real",
            ImgBBApiKey = "demo-key-not-real",
            EbayClientId = "demo-client-id",
            EbayClientSecret = "demo-secret",
            IsEbaySeller = true,
            WhatnotFeePercent = 11.0m,
            EbayFeePercent = 13.25m,
            DefaultShippingCostPwe = 1.00m,
            DefaultShippingCostBmwt = 4.50m,
            PriceStalenessThresholdDays = 30,
            DefaultModel = "nvidia/nemotron-nano-12b-v2-vl:free"
        };

        public AppSettings Load() => _settings;
        public void Save(AppSettings settings) { }
        public Task<bool> TestOpenRouterConnectionAsync(string apiKey) => Task.FromResult(true);
        public Task<bool> TestImgBBConnectionAsync(string apiKey) => Task.FromResult(true);
    }

    // Mock scanner service
    public class MockScannerService : IScannerService
    {
        public Task<ScanResult> ScanCardAsync(string imagePath, string? backImagePath, string? modelOverride) =>
            Task.FromResult(new ScanResult
            {
                Success = true,
                PlayerName = "Demo Player",
                Year = 2023,
                Sport = Sport.Football,
                Brand = "Prizm",
                Manufacturer = "Panini"
            });
    }

    // Mock pricer service
    public class MockPricerService : IPricerService
    {
        public string BuildTerapeakUrl(Card card) => "https://terapeak.ebay.com/demo";
        public string BuildEbaySoldUrl(Card card) => "https://ebay.com/sch/demo";
        public decimal SuggestPrice(decimal marketValue, Card card) => marketValue * 1.15m;
    }

    // Mock export service
    public class MockExportService : IExportService
    {
        public Task ExportCsvAsync(List<Card> cards, string filePath) => Task.CompletedTask;
        public List<string> ValidateCardForExport(Card card) => new();
    }

    // Mock browser service
    public class MockBrowserService : IBrowserService
    {
        public void OpenUrl(string url) { }
    }

    // Mock file dialog service
    public class MockFileDialogService : IFileDialogService
    {
        public Task<string?> OpenImageFileAsync() => Task.FromResult<string?>("/demo/image.jpg");
        public Task<List<string>> OpenMultipleImageFilesAsync() => Task.FromResult(new List<string>());
        public Task<string?> SaveCsvFileAsync(string defaultFileName) => Task.FromResult<string?>("/demo/export.csv");
    }

    // Mock image upload service
    public class MockImageUploadService : IImageUploadService
    {
        public Task<(string? url1, string? url2)> UploadCardImagesAsync(string frontPath, string? backPath) =>
            Task.FromResult<(string?, string?)>(("https://demo.com/front.jpg", null));
    }

    // Mock variation verifier
    public class MockVariationVerifier : IVariationVerifier
    {
        public Task<VerificationResult> VerifyCardAsync(Card card) =>
            Task.FromResult(new VerificationResult { HasSuggestion = false });
    }

    // Mock checklist learning service
    public class MockChecklistLearningService : IChecklistLearningService
    {
        public Task LearnFromCardAsync(Card card) => Task.CompletedTask;
    }

    // Mock sold price service
    public class MockSoldPriceService : ISoldPriceService
    {
        public Task<List<SoldPriceRecord>> FindMatchingRecordsAsync(Card card) =>
            Task.FromResult(new List<SoldPriceRecord>());

        public Task<ScrapedResult> ScrapeSoldPricesAsync(Card card, int maxResults = 20) =>
            Task.FromResult(new ScrapedResult { Success = false });

        public PriceLookupResult CalculateMarketValue(List<SoldPriceRecord> records, Card card) =>
            new() { Success = false };

        public Task<bool> HasRecentDataAsync(Card card, int daysOld = 30) => Task.FromResult(false);
    }

    // Mock eBay browse service
    public class MockEbayBrowseService : IEbayBrowseService
    {
        public Task<ActiveListingResult> FetchActiveListingsAsync(Card card, int maxResults = 20) =>
            Task.FromResult(new ActiveListingResult
            {
                Success = true,
                RecordsFound = 8,
                RecordsSaved = 8
            });

        public Task<List<ActiveListingRecord>> FindMatchingListingsAsync(Card card, int maxAgeDays = 7) =>
            Task.FromResult(new List<ActiveListingRecord>
            {
                new() { PlayerName = card.PlayerName, ListingPrice = 45.00m },
                new() { PlayerName = card.PlayerName, ListingPrice = 48.99m },
                new() { PlayerName = card.PlayerName, ListingPrice = 52.50m }
            });

        public ActiveListingStats CalculateStats(List<ActiveListingRecord> listings, Card card) =>
            new()
            {
                Success = true,
                SampleSize = listings.Count,
                MedianPrice = 48.99m,
                AveragePrice = 48.83m,
                LowPrice = 45.00m,
                HighPrice = 52.50m,
                Confidence = ListingConfidence.Medium,
                Source = "eBay Active (3 listings)"
            };

        public Task<bool> HasRecentDataAsync(Card card, int maxAgeDays = 7) => Task.FromResult(false);

        public Task<EbayOAuthToken> GetAccessTokenAsync() =>
            Task.FromResult(new EbayOAuthToken
            {
                AccessToken = "demo-token",
                ExpiresInSeconds = 7200
            });
    }
}
