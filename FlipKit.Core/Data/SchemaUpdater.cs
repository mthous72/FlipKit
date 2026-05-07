using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace FlipKit.Core.Data
{
    public static class SchemaUpdater
    {
        public static async Task EnsureVerificationTablesAsync(FlipKitDbContext db)
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS set_checklists (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Manufacturer TEXT NOT NULL,
                    Brand TEXT NOT NULL,
                    Year INTEGER NOT NULL,
                    Sport TEXT,
                    Cards TEXT NOT NULL DEFAULT '[]',
                    KnownVariations TEXT NOT NULL DEFAULT '[]',
                    TotalBaseCards INTEGER NOT NULL DEFAULT 0,
                    CachedAt TEXT NOT NULL DEFAULT '0001-01-01T00:00:00'
                );");

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_set_checklists_Manufacturer_Brand_Year_Sport
                ON set_checklists (Manufacturer, Brand, Year, Sport);");

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS missing_checklists (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Manufacturer TEXT NOT NULL,
                    Brand TEXT NOT NULL,
                    Year INTEGER NOT NULL,
                    Sport TEXT,
                    HitCount INTEGER NOT NULL DEFAULT 1,
                    FirstSeen TEXT NOT NULL DEFAULT '0001-01-01T00:00:00',
                    LastSeen TEXT NOT NULL DEFAULT '0001-01-01T00:00:00'
                );");

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_missing_checklists_Manufacturer_Brand_Year_Sport
                ON missing_checklists (Manufacturer, Brand, Year, Sport);");

            await EnsureAutoGradeColumnAsync(db);
            await EnsureChecklistLearningColumnsAsync(db);
            await EnsureExportColumnsAsync(db);
            await EnsureCardVerificationColumnsAsync(db);
            await EnsureEbayImportColumnsAsync(db);
            await EnsureSurpriseSetTablesAsync(db);
            await EnsureSurpriseSetCardColumnsAsync(db);
            await EnsureCardDataSourceColumnAsync(db);
        }

        public static async Task EnsureSurpriseSetTablesAsync(FlipKitDbContext db)
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS surprise_sets (
                    Id                          INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name                        TEXT    NOT NULL DEFAULT '',
                    ShowName                    TEXT,
                    Notes                       TEXT,
                    State                       TEXT    NOT NULL DEFAULT 'Draft',
                    CreatedAt                   TEXT    NOT NULL,
                    UpdatedAt                   TEXT    NOT NULL,
                    ExportedAt                  TEXT,
                    LiveAt                      TEXT,
                    CompletedAt                 TEXT,
                    CancelledAt                 TEXT,
                    Title                       TEXT    NOT NULL DEFAULT '',
                    SharedListingType           TEXT    NOT NULL DEFAULT 'Buy it Now',
                    SpotPrice                   REAL    NOT NULL DEFAULT 0,
                    SharedCondition             TEXT    NOT NULL DEFAULT '',
                    SharedShippingProfile       TEXT    NOT NULL DEFAULT '',
                    SharedWhatnotCategory       TEXT    NOT NULL DEFAULT 'Sports Trading Cards',
                    SharedWhatnotSubcategory    TEXT,
                    Offerable                   INTEGER NOT NULL DEFAULT 0,
                    SharedImageUrl1             TEXT,
                    SharedImageUrl2             TEXT,
                    SharedImageUrl3             TEXT,
                    SharedImageUrl4             TEXT,
                    SharedImageUrl5             TEXT,
                    SharedImageUrl6             TEXT,
                    SharedImageUrl7             TEXT,
                    SharedImageUrl8             TEXT,
                    AllocationMethod            TEXT    NOT NULL DEFAULT 'EqualSplit',
                    LotCostBasis                REAL,
                    SpotsSold                   INTEGER,
                    GrossRevenue                REAL,
                    TotalFees                   REAL,
                    TotalShipping               REAL
                );");
        }

        public static async Task EnsureSurpriseSetCardColumnsAsync(FlipKitDbContext db)
        {
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info(cards)";
                using var reader = await cmd.ExecuteReaderAsync();
                var columns = new System.Collections.Generic.List<string>();
                while (await reader.ReadAsync())
                    columns.Add(reader.GetString(1));

                if (!columns.Contains("SurpriseSetId"))
                    await db.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE cards ADD COLUMN SurpriseSetId INTEGER REFERENCES surprise_sets(Id)");

                if (!columns.Contains("SurpriseSetSlot"))
                    await db.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE cards ADD COLUMN SurpriseSetSlot INTEGER");
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        // eBay Seller Hub CSV import — EbayItemId is the upsert key on re-import,
        // ListedAt captures the eBay "Start date" so reports can age listings.
        // Partial unique index mirrors the Sku pattern: enforce uniqueness only
        // on populated values so the column stays nullable for non-imported cards.
        public static async Task EnsureEbayImportColumnsAsync(FlipKitDbContext db)
        {
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info(cards)";
                using var reader = await cmd.ExecuteReaderAsync();
                var columns = new System.Collections.Generic.List<string>();
                while (await reader.ReadAsync())
                    columns.Add(reader.GetString(1));

                if (!columns.Contains("EbayItemId"))
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE cards ADD COLUMN EbayItemId TEXT");

                if (!columns.Contains("ListedAt"))
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE cards ADD COLUMN ListedAt TEXT");
            }
            finally
            {
                await conn.CloseAsync();
            }

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_cards_EbayItemId
                ON cards (EbayItemId)
                WHERE EbayItemId IS NOT NULL AND EbayItemId <> ''");
        }

        // Phase 2 of the Checklist Insider import work — Card carries the tier outcome
        // and a re-find key pointing back at the matched ChecklistCard inside its
        // SetChecklist's JSON blob.
        public static async Task EnsureCardVerificationColumnsAsync(FlipKitDbContext db)
        {
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info(cards)";
                using var reader = await cmd.ExecuteReaderAsync();
                var columns = new System.Collections.Generic.List<string>();
                while (await reader.ReadAsync())
                    columns.Add(reader.GetString(1));

                if (!columns.Contains("VerificationStatus"))
                    await db.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE cards ADD COLUMN VerificationStatus TEXT NOT NULL DEFAULT 'NotChecked'");

                if (!columns.Contains("MatchedChecklistKey"))
                    await db.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE cards ADD COLUMN MatchedChecklistKey TEXT");
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        private static async Task EnsureExportColumnsAsync(FlipKitDbContext db)
        {
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info(cards)";
                using var reader = await cmd.ExecuteReaderAsync();
                var columns = new System.Collections.Generic.List<string>();
                while (await reader.ReadAsync())
                    columns.Add(reader.GetString(1));
                await reader.CloseAsync();

                if (!columns.Contains("Sku"))
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE cards ADD COLUMN Sku TEXT");

                for (int i = 3; i <= 8; i++)
                {
                    var urlCol = "ImageUrl" + i;
                    if (!columns.Contains(urlCol))
                        await db.Database.ExecuteSqlRawAsync("ALTER TABLE cards ADD COLUMN " + urlCol + " TEXT");

                    var pathCol = "ImagePath" + i;
                    if (!columns.Contains(pathCol))
                        await db.Database.ExecuteSqlRawAsync("ALTER TABLE cards ADD COLUMN " + pathCol + " TEXT");
                }
            }
            finally
            {
                await conn.CloseAsync();
            }

            // Partial unique index: enforce uniqueness only on non-null/non-empty SKUs
            // so the column can stay nullable for cards that haven't been assigned one yet.
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_cards_Sku
                ON cards (Sku)
                WHERE Sku IS NOT NULL AND Sku <> ''");
        }

        private static async Task EnsureAutoGradeColumnAsync(FlipKitDbContext db)
        {
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info(cards)";
                using var reader = await cmd.ExecuteReaderAsync();
                var columns = new System.Collections.Generic.List<string>();
                while (await reader.ReadAsync())
                    columns.Add(reader.GetString(1));

                if (!columns.Contains("AutoGrade"))
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE cards ADD COLUMN AutoGrade TEXT");
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        private static async Task EnsureCardDataSourceColumnAsync(FlipKitDbContext db)
        {
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info(cards)";
                using var reader = await cmd.ExecuteReaderAsync();
                var columns = new System.Collections.Generic.List<string>();
                while (await reader.ReadAsync())
                    columns.Add(reader.GetString(1));

                if (!columns.Contains("DataSource"))
                    await db.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE cards ADD COLUMN DataSource TEXT NOT NULL DEFAULT 'None'");
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        public static async Task EnsureChecklistLearningColumnsAsync(FlipKitDbContext db)
        {
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info(set_checklists)";
                using var reader = await cmd.ExecuteReaderAsync();
                var columns = new System.Collections.Generic.List<string>();
                while (await reader.ReadAsync())
                    columns.Add(reader.GetString(1));

                if (!columns.Contains("DataSource"))
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE set_checklists ADD COLUMN DataSource TEXT NOT NULL DEFAULT 'seed'");

                if (!columns.Contains("LastEnrichedAt"))
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE set_checklists ADD COLUMN LastEnrichedAt TEXT NOT NULL DEFAULT '0001-01-01T00:00:00'");

                if (!columns.Contains("ImportedAt"))
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE set_checklists ADD COLUMN ImportedAt TEXT");
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

    }
}
