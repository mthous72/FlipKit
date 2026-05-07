using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Models.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace FlipKit.Core.Data
{
    public class FlipKitDbContext : DbContext
    {
        public DbSet<Card> Cards => Set<Card>();
        public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
        public DbSet<SetChecklist> SetChecklists => Set<SetChecklist>();
        public DbSet<MissingChecklist> MissingChecklists => Set<MissingChecklist>();
        public DbSet<SurpriseSet> SurpriseSets => Set<SurpriseSet>();

        // Reference data — bootstrap catalog seeded from JSON resources at first
        // run (see ReferenceDataSeeder). Drives OCR sport inference + catalog gates.
        public DbSet<LeagueTeam> LeagueTeams => Set<LeagueTeam>();
        public DbSet<KnownManufacturer> KnownManufacturers => Set<KnownManufacturer>();
        public DbSet<KnownBrand> KnownBrands => Set<KnownBrand>();
        public DbSet<KnownVariation> KnownVariations => Set<KnownVariation>();
        public DbSet<GradingAuthority> GradingAuthorities => Set<GradingAuthority>();
        public DbSet<LeagueAcronym> LeagueAcronyms => Set<LeagueAcronym>();

        public FlipKitDbContext(DbContextOptions<FlipKitDbContext> options)
            : base(options)
        {
        }

        public static string GetDbPath()
        {
            // Support Docker: check for FLIPKIT_DB_PATH environment variable
            var envPath = Environment.GetEnvironmentVariable("FLIPKIT_DB_PATH");
            if (!string.IsNullOrEmpty(envPath))
            {
                var envFolder = Path.GetDirectoryName(envPath);
                if (!string.IsNullOrEmpty(envFolder))
                    Directory.CreateDirectory(envFolder);
                return envPath;
            }

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

            // Checklist verification tier (Phase 2 of Checklist Insider import work).
            card.Property(c => c.VerificationStatus)
                .HasConversion<string>()
                .HasDefaultValue(VerificationStatus.NotChecked);

            card.Property(c => c.DataSource)
                .HasConversion<string>()
                .HasDefaultValue(CardDataSource.None);

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
            card.HasIndex(c => c.Sku).IsUnique();
            card.HasIndex(c => c.EbayItemId).IsUnique();

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

            // ValueComparer required on JSON-converted collection properties — without it,
            // EF's change tracker compares by reference and silently misses Add/Remove
            // mutations to the underlying list. Snapshot via JSON round-trip so the
            // comparer's "current vs original" check sees the difference. See
            // AUDIT-2026-05 §5.10 for the bug history.
            setChecklist.Property(s => s.Cards)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<ChecklistCard>>(v, (JsonSerializerOptions?)null) ?? new List<ChecklistCard>(),
                    new ValueComparer<List<ChecklistCard>>(
                        (l, r) => JsonSerializer.Serialize(l, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(r, (JsonSerializerOptions?)null),
                        l => l == null ? 0 : JsonSerializer.Serialize(l, (JsonSerializerOptions?)null).GetHashCode(),
                        l => JsonSerializer.Deserialize<List<ChecklistCard>>(JsonSerializer.Serialize(l, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null) ?? new List<ChecklistCard>()));

            setChecklist.Property(s => s.KnownVariations)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                    new ValueComparer<List<string>>(
                        (l, r) => l != null && r != null && l.SequenceEqual(r),
                        l => l == null ? 0 : l.Aggregate(0, (h, s) => HashCode.Combine(h, s)),
                        l => l.ToList()));

            // MissingChecklist configuration
            var missingChecklist = modelBuilder.Entity<MissingChecklist>();

            missingChecklist.ToTable("missing_checklists");
            missingChecklist.HasKey(m => m.Id);

            missingChecklist.HasIndex(m => new { m.Manufacturer, m.Brand, m.Year, m.Sport })
                .IsUnique();

            // SurpriseSet configuration
            var surpriseSet = modelBuilder.Entity<SurpriseSet>();

            surpriseSet.ToTable("surprise_sets");
            surpriseSet.HasKey(s => s.Id);

            surpriseSet.Property(s => s.State).HasConversion<string>().HasDefaultValue(SurpriseSetState.Draft);
            surpriseSet.Property(s => s.AllocationMethod).HasConversion<string>().HasDefaultValue(RevenueAllocationMethod.EqualSplit);
            surpriseSet.Property(s => s.SpotPrice).HasColumnType("decimal(10,2)");
            surpriseSet.Property(s => s.LotCostBasis).HasColumnType("decimal(10,2)");
            surpriseSet.Property(s => s.GrossRevenue).HasColumnType("decimal(10,2)");
            surpriseSet.Property(s => s.TotalFees).HasColumnType("decimal(10,2)");
            surpriseSet.Property(s => s.TotalShipping).HasColumnType("decimal(10,2)");

            // Card → SurpriseSet: Restrict so EF doesn't auto-cascade.
            // SurpriseSetRepository.DeleteAsync handles the cascade explicitly
            // in a transaction (cards deleted first, then the set).
            card.HasOne(c => c.SurpriseSet)
                .WithMany(s => s.Cards)
                .HasForeignKey(c => c.SurpriseSetId)
                .OnDelete(DeleteBehavior.Restrict);

            card.HasIndex(c => c.SurpriseSetId);

            // Reference data — LeagueTeam / KnownManufacturer / KnownBrand.
            // Aliases / SportsActive / Sports are List<string> serialized to JSON
            // with the same ValueComparer pattern used for SetChecklist.Cards
            // (otherwise EF compares by reference and misses list mutations).
            var leagueTeam = modelBuilder.Entity<LeagueTeam>();
            leagueTeam.ToTable("league_teams");
            leagueTeam.HasKey(t => t.Id);
            leagueTeam.HasIndex(t => new { t.Sport, t.TeamName }).IsUnique();
            leagueTeam.Property(t => t.Aliases)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                    new ValueComparer<List<string>>(
                        (l, r) => l != null && r != null && l.SequenceEqual(r),
                        l => l == null ? 0 : l.Aggregate(0, (h, s) => HashCode.Combine(h, s)),
                        l => l.ToList()));

            var knownManufacturer = modelBuilder.Entity<KnownManufacturer>();
            knownManufacturer.ToTable("known_manufacturers");
            knownManufacturer.HasKey(m => m.Id);
            knownManufacturer.HasIndex(m => m.Name).IsUnique();
            knownManufacturer.Property(m => m.SportsActive)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                    new ValueComparer<List<string>>(
                        (l, r) => l != null && r != null && l.SequenceEqual(r),
                        l => l == null ? 0 : l.Aggregate(0, (h, s) => HashCode.Combine(h, s)),
                        l => l.ToList()));
            knownManufacturer.Property(m => m.Aliases)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                    new ValueComparer<List<string>>(
                        (l, r) => l != null && r != null && l.SequenceEqual(r),
                        l => l == null ? 0 : l.Aggregate(0, (h, s) => HashCode.Combine(h, s)),
                        l => l.ToList()));

            var knownBrand = modelBuilder.Entity<KnownBrand>();
            knownBrand.ToTable("known_brands");
            knownBrand.HasKey(b => b.Id);
            knownBrand.HasIndex(b => new { b.Manufacturer, b.Name }).IsUnique();
            knownBrand.Property(b => b.Sports)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                    new ValueComparer<List<string>>(
                        (l, r) => l != null && r != null && l.SequenceEqual(r),
                        l => l == null ? 0 : l.Aggregate(0, (h, s) => HashCode.Combine(h, s)),
                        l => l.ToList()));

            var knownVariation = modelBuilder.Entity<KnownVariation>();
            knownVariation.ToTable("known_variations");
            knownVariation.HasKey(v => v.Id);
            knownVariation.HasIndex(v => new { v.Manufacturer, v.Type, v.Name }).IsUnique();
            knownVariation.Property(v => v.Sports)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                    new ValueComparer<List<string>>(
                        (l, r) => l != null && r != null && l.SequenceEqual(r),
                        l => l == null ? 0 : l.Aggregate(0, (h, s) => HashCode.Combine(h, s)),
                        l => l.ToList()));

            var gradingAuthority = modelBuilder.Entity<GradingAuthority>();
            gradingAuthority.ToTable("grading_authorities");
            gradingAuthority.HasKey(g => g.Id);
            gradingAuthority.HasIndex(g => g.Code).IsUnique();
            gradingAuthority.Property(g => g.MinGrade).HasColumnType("decimal(4,1)");
            gradingAuthority.Property(g => g.MaxGrade).HasColumnType("decimal(4,1)");
            gradingAuthority.Property(g => g.GradeIncrement).HasColumnType("decimal(4,1)");

            var leagueAcronym = modelBuilder.Entity<LeagueAcronym>();
            leagueAcronym.ToTable("league_acronyms");
            leagueAcronym.HasKey(l => l.Id);
            leagueAcronym.HasIndex(l => l.Acronym).IsUnique();
        }
    }
}
