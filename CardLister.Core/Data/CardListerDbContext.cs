using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace FlipKit.Core.Data
{
    public class FlipKitDbContext : DbContext
    {
        public DbSet<Card> Cards => Set<Card>();
        public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
        public DbSet<SetChecklist> SetChecklists => Set<SetChecklist>();
        public DbSet<MissingChecklist> MissingChecklists => Set<MissingChecklist>();
        public DbSet<SoldPriceRecord> SoldPriceRecords => Set<SoldPriceRecord>();

        public FlipKitDbContext(DbContextOptions<FlipKitDbContext> options)
            : base(options)
        {
        }

        public static string GetDbPath()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlipKit");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "cards.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Card configuration
            var card = modelBuilder.Entity<Card>();

            card.ToTable("cards");
            card.HasKey(c => c.Id);

            card.Property(c => c.PlayerName).IsRequired();

            // Enum conversions (stored as strings)
            card.Property(c => c.Status)
                .HasConversion<string>()
                .HasDefaultValue(CardStatus.Draft);

            card.Property(c => c.Sport)
                .HasConversion<string?>();

            card.Property(c => c.CostSource)
                .HasConversion<string?>();

            // Decimal precision
            card.Property(c => c.CostBasis).HasColumnType("decimal(10,2)");
            card.Property(c => c.EstimatedValue).HasColumnType("decimal(10,2)");
            card.Property(c => c.ListingPrice).HasColumnType("decimal(10,2)");
            card.Property(c => c.SalePrice).HasColumnType("decimal(10,2)");
            card.Property(c => c.FeesPaid).HasColumnType("decimal(10,2)");
            card.Property(c => c.ShippingCost).HasColumnType("decimal(10,2)");
            card.Property(c => c.NetProfit).HasColumnType("decimal(10,2)");

            // Indexes
            card.HasIndex(c => c.Status);
            card.HasIndex(c => c.Sport);
            card.HasIndex(c => c.PlayerName);
            card.HasIndex(c => c.Year);

            // PriceHistory configuration
            var priceHistory = modelBuilder.Entity<PriceHistory>();

            priceHistory.ToTable("price_history");
            priceHistory.HasKey(p => p.Id);

            priceHistory.Property(p => p.EstimatedValue).HasColumnType("decimal(10,2)");
            priceHistory.Property(p => p.ListingPrice).HasColumnType("decimal(10,2)");

            priceHistory.HasOne(p => p.Card)
                .WithMany(c => c.PriceHistories)
                .HasForeignKey(p => p.CardId)
                .OnDelete(DeleteBehavior.Cascade);

            // SetChecklist configuration
            var setChecklist = modelBuilder.Entity<SetChecklist>();

            setChecklist.ToTable("set_checklists");
            setChecklist.HasKey(s => s.Id);

            setChecklist.HasIndex(s => new { s.Manufacturer, s.Brand, s.Year, s.Sport })
                .IsUnique();

            setChecklist.Property(s => s.Cards)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<ChecklistCard>>(v, (JsonSerializerOptions?)null) ?? new List<ChecklistCard>());

            setChecklist.Property(s => s.KnownVariations)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

            // MissingChecklist configuration
            var missingChecklist = modelBuilder.Entity<MissingChecklist>();

            missingChecklist.ToTable("missing_checklists");
            missingChecklist.HasKey(m => m.Id);

            missingChecklist.HasIndex(m => new { m.Manufacturer, m.Brand, m.Year, m.Sport })
                .IsUnique();

            // SoldPriceRecord configuration
            var soldPrice = modelBuilder.Entity<SoldPriceRecord>();

            soldPrice.ToTable("sold_price_records");
            soldPrice.HasKey(s => s.Id);

            soldPrice.Property(s => s.PlayerName).IsRequired();

            // Decimal precision
            soldPrice.Property(s => s.SoldPrice).HasColumnType("decimal(10,2)");
            soldPrice.Property(s => s.ShippingCost).HasColumnType("decimal(10,2)");

            // Indexes for efficient lookups
            soldPrice.HasIndex(s => new { s.PlayerName, s.Year, s.Brand, s.Sport })
                .HasDatabaseName("idx_soldprice_lookup");

            soldPrice.HasIndex(s => s.SoldDate)
                .HasDatabaseName("idx_soldprice_date");
        }
    }
}
