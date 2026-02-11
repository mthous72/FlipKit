using System;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedIfEmptyAsync(FlipKitDbContext db)
        {
            if (db.Cards.Any())
                return;

            var now = DateTime.UtcNow;

            var cards = new[]
            {
                new Card
                {
                    PlayerName = "Justin Jefferson",
                    CardNumber = "88",
                    Year = 2023,
                    Sport = Sport.Football,
                    Manufacturer = "Panini",
                    Brand = "Prizm",
                    Team = "Minnesota Vikings",
                    VariationType = "Parallel",
                    ParallelName = "Silver",
                    IsRookie = false,
                    Condition = "Near Mint",
                    EstimatedValue = 15.00m,
                    ListingPrice = 12.99m,
                    PriceSource = "Terapeak",
                    PriceDate = now.AddDays(-5),
                    CostBasis = 5.00m,
                    CostSource = CostSource.LCS,
                    CostDate = now.AddDays(-30),
                    Status = CardStatus.Priced,
                    WhatnotSubcategory = "Football Cards",
                    CreatedAt = now.AddDays(-10),
                    UpdatedAt = now.AddDays(-5)
                },
                new Card
                {
                    PlayerName = "CJ Stroud",
                    CardNumber = "301",
                    Year = 2023,
                    Sport = Sport.Football,
                    Manufacturer = "Panini",
                    Brand = "Donruss",
                    Team = "Houston Texans",
                    VariationType = "Base",
                    IsRookie = true,
                    Condition = "Near Mint",
                    CostBasis = 3.50m,
                    CostSource = CostSource.Online,
                    CostDate = now.AddDays(-25),
                    Status = CardStatus.Draft,
                    WhatnotSubcategory = "Football Cards",
                    CreatedAt = now.AddDays(-8),
                    UpdatedAt = now.AddDays(-8)
                },
                new Card
                {
                    PlayerName = "Victor Wembanyama",
                    CardNumber = "280",
                    Year = 2023,
                    Sport = Sport.Basketball,
                    Manufacturer = "Panini",
                    Brand = "Prizm",
                    Team = "San Antonio Spurs",
                    VariationType = "Parallel",
                    ParallelName = "Silver",
                    IsRookie = true,
                    Condition = "Near Mint",
                    EstimatedValue = 30.00m,
                    ListingPrice = 24.99m,
                    PriceSource = "Terapeak",
                    PriceDate = now.AddDays(-10),
                    CostBasis = 12.00m,
                    CostSource = CostSource.Break,
                    CostDate = now.AddDays(-40),
                    Status = CardStatus.Ready,
                    WhatnotSubcategory = "Basketball Cards",
                    CreatedAt = now.AddDays(-15),
                    UpdatedAt = now.AddDays(-10)
                },
                new Card
                {
                    PlayerName = "Shohei Ohtani",
                    CardNumber = "1",
                    Year = 2024,
                    Sport = Sport.Baseball,
                    Manufacturer = "Topps",
                    Brand = "Chrome",
                    Team = "Los Angeles Dodgers",
                    VariationType = "Parallel",
                    ParallelName = "Refractor",
                    Condition = "Near Mint",
                    EstimatedValue = 10.00m,
                    ListingPrice = 8.99m,
                    PriceSource = "eBay comps",
                    PriceDate = now.AddDays(-45),
                    CostBasis = 8.00m,
                    CostSource = CostSource.Online,
                    CostDate = now.AddDays(-60),
                    Status = CardStatus.Priced,
                    WhatnotSubcategory = "Baseball Cards",
                    CreatedAt = now.AddDays(-50),
                    UpdatedAt = now.AddDays(-45)
                },
                new Card
                {
                    PlayerName = "Patrick Mahomes",
                    CardNumber = "1",
                    Year = 2022,
                    Sport = Sport.Football,
                    Manufacturer = "Panini",
                    Brand = "Mosaic",
                    Team = "Kansas City Chiefs",
                    VariationType = "Parallel",
                    ParallelName = "Gold",
                    SerialNumbered = "/10",
                    Condition = "Near Mint",
                    EstimatedValue = 85.00m,
                    ListingPrice = 79.99m,
                    PriceSource = "Terapeak",
                    PriceDate = now.AddDays(-3),
                    CostBasis = 45.00m,
                    CostSource = CostSource.CardShow,
                    CostDate = now.AddDays(-90),
                    ImageUrl1 = "https://example.com/mahomes-front.jpg",
                    Status = CardStatus.Ready,
                    WhatnotSubcategory = "Football Cards",
                    CreatedAt = now.AddDays(-20),
                    UpdatedAt = now.AddDays(-3)
                }
            };

            db.Cards.AddRange(cards);
            await db.SaveChangesAsync();
        }
    }
}
