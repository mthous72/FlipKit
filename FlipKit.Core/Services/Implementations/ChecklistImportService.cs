using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Data;
using FlipKit.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Two-step import: <see cref="Parse"/> returns a preview the user can edit, then
    /// <see cref="CommitAsync"/> writes the SetChecklist row. Replacing an existing
    /// checklist for the same (Manufacturer, Brand, Year, Sport) tuple is the default —
    /// the same legal-imported file should win over a learned-or-seeded older copy.
    /// </summary>
    public class ChecklistImportService : IChecklistImportService
    {
        private readonly IExcelChecklistImporter _importer;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ChecklistImportService>? _logger;

        public ChecklistImportService(
            IExcelChecklistImporter importer,
            IServiceProvider serviceProvider,
            ILogger<ChecklistImportService>? logger = null)
        {
            _importer = importer;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public ChecklistImportPreview Parse(Stream xlsxStream, string fileName)
            => _importer.Parse(xlsxStream, fileName);

        public async Task<ChecklistImportCommitResult> CommitAsync(ChecklistImportPreview preview, bool replaceExisting = true)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));

            if (!preview.IsValid)
            {
                return new ChecklistImportCommitResult
                {
                    Success = false,
                    ErrorMessage = "Preview is incomplete. Year, Brand, and at least one card are required.",
                };
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();

                var manufacturer = preview.Metadata.Manufacturer ?? string.Empty;
                var brand = preview.Metadata.Brand ?? string.Empty;
                var year = preview.Metadata.Year!.Value;
                var sport = preview.Metadata.Sport;

                var existing = await db.SetChecklists.FirstOrDefaultAsync(s =>
                    s.Manufacturer == manufacturer &&
                    s.Brand == brand &&
                    s.Year == year &&
                    s.Sport == sport);

                bool replaced = false;
                SetChecklist target;

                if (existing != null)
                {
                    if (!replaceExisting)
                    {
                        return new ChecklistImportCommitResult
                        {
                            Success = false,
                            ErrorMessage = "A checklist already exists for this set. Confirm replacement to overwrite.",
                            ChecklistId = existing.Id,
                        };
                    }

                    existing.Cards = preview.Cards.ToList();
                    existing.KnownVariations = preview.SubsetNames
                        .Where(n => !string.Equals(n, "Base", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    existing.TotalBaseCards = preview.Cards.Count(c =>
                        string.Equals(c.Subset, "Base", StringComparison.OrdinalIgnoreCase));
                    existing.DataSource = "checklist-insider";
                    existing.ImportedAt = DateTime.UtcNow;
                    existing.LastEnrichedAt = DateTime.UtcNow;
                    existing.CachedAt = DateTime.UtcNow;
                    target = existing;
                    replaced = true;
                }
                else
                {
                    target = new SetChecklist
                    {
                        Manufacturer = manufacturer,
                        Brand = brand,
                        Year = year,
                        Sport = sport,
                        Cards = preview.Cards.ToList(),
                        KnownVariations = preview.SubsetNames
                            .Where(n => !string.Equals(n, "Base", StringComparison.OrdinalIgnoreCase))
                            .ToList(),
                        TotalBaseCards = preview.Cards.Count(c =>
                            string.Equals(c.Subset, "Base", StringComparison.OrdinalIgnoreCase)),
                        DataSource = "checklist-insider",
                        ImportedAt = DateTime.UtcNow,
                        LastEnrichedAt = DateTime.UtcNow,
                        CachedAt = DateTime.UtcNow,
                    };
                    db.SetChecklists.Add(target);
                }

                // Drop any stale "missing checklist" row covering this set — the gap is closed.
                var missing = await db.MissingChecklists.FirstOrDefaultAsync(m =>
                    m.Manufacturer == manufacturer &&
                    m.Brand == brand &&
                    m.Year == year &&
                    m.Sport == sport);
                if (missing != null) db.MissingChecklists.Remove(missing);

                await db.SaveChangesAsync();

                _logger?.LogInformation(
                    "Imported {CardCount} cards into {Year} {Brand} {Sport} ({SubsetCount} subsets) — replaced={Replaced}",
                    preview.Cards.Count, year, brand, sport, preview.SubsetCount, replaced);

                return new ChecklistImportCommitResult
                {
                    Success = true,
                    CardsImported = preview.Cards.Count,
                    SubsetCount = preview.SubsetCount,
                    ReplacedExisting = replaced,
                    ChecklistId = target.Id,
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Checklist import failed for {File}", preview.Metadata.SourceFileName);
                return new ChecklistImportCommitResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                };
            }
        }
    }
}
