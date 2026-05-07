using FlipKit.Core.Data;
using FlipKit.Core.Helpers;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations;
using FlipKit.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add session support (used by ScanController for scan mode)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add HttpClient and HttpContextAccessor
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

// Add logging
builder.Services.AddLogging();

// Secrets encryption — same key directory as Desktop so both apps can decrypt
// values written by the other. DPAPI on Windows; file-protected key ring on Linux/macOS.
var keyDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "FlipKit", "DataProtection-Keys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyDir))
    .SetApplicationName("FlipKit");
builder.Services.AddSingleton<ISecretEncryption, DataProtectionSecretEncryption>();

// Settings service needed first for mode detection
builder.Services.AddSingleton<ISettingsService, JsonSettingsService>();

// Smart mode detection - choose between local database or API
var tempProvider = builder.Services.BuildServiceProvider();
var settingsService = tempProvider.GetRequiredService<ISettingsService>();
var settings = settingsService.Load();
var dataMode = DataAccessModeDetector.DetectMode(settings);

if (dataMode == DataAccessMode.Local)
{
    // Local mode - direct database access (fast)
    Console.WriteLine($"Data access mode: LOCAL (Direct SQLite)");

    // Use the same database path as Desktop app
    var dbPath = FlipKitDbContext.GetDbPath();
    Console.WriteLine($"Database path: {dbPath}");

    builder.Services.AddDbContext<FlipKitDbContext>(options =>
        options.UseSqlite($"Data Source={dbPath}"));
    builder.Services.AddScoped<ICardRepository, CardRepository>();
    builder.Services.AddScoped<ISkuGenerator, FlipKit.Core.Services.Export.SkuGenerator>();
}
else
{
    // Remote mode - API calls via Tailscale
    Console.WriteLine($"Data access mode: REMOTE API ({settings.SyncServerUrl})");
    builder.Services.AddSingleton<ICardRepository>(sp =>
    {
        var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
        var logger = sp.GetRequiredService<ILogger<ApiCardRepository>>();
        return new ApiCardRepository(httpClient, settings.SyncServerUrl!, logger);
    });
}
// Scanner services - Ximilar checked first, then falls back to OpenRouter LLM
builder.Services.AddSingleton<IXimilarService, XimilarService>();
builder.Services.AddSingleton<OpenRouterScannerService>();
builder.Services.AddSingleton<IScannerService, CompositeScannerService>();
// Live model catalog from OpenRouter — single instance, app-lifetime cache
builder.Services.AddSingleton<IOpenRouterModelCatalog, FlipKit.Core.Services.Scanning.OpenRouterModelCatalog>();
builder.Services.AddScoped<IPricerService, PricerService>(); // Depends on DbContext via repositories
// Export pipeline — registered unconditionally (no DbContext dependency).
builder.Services.AddSingleton<FlipKit.Core.Services.Export.WhatnotValuesProvider>();
builder.Services.AddSingleton<FlipKit.Core.Services.Export.ShippingProfileNormalizer>();
builder.Services.AddSingleton<FlipKit.Core.Services.Export.WhatnotExporter>();
builder.Services.AddSingleton<FlipKit.Core.Services.Export.ExportValidator>();
builder.Services.AddScoped<IExportService, CsvExportService>(); // Depends on DbContext
builder.Services.AddSingleton<IImageUploadService, ImgBBUploadService>();
builder.Services.AddScoped<IVariationVerifier, VariationVerifierService>(); // Depends on DbContext
builder.Services.AddSingleton<IChecklistLearningService, ChecklistLearningService>(); // Uses IServiceProvider to create scopes
// Directory for OCR / Enhance fuzzy lookups against imported checklists +
// reference seed. Singleton so the cache is hot across requests; refresh on
// app start and after every checklist import.
builder.Services.AddSingleton<IPlayerNameDirectory, PlayerNameDirectory>();
// Phase 1 of the Checklist Insider import work — parser + service that turn user-supplied
// .xlsx files into SetChecklist rows. Singleton because nothing here owns DbContext directly;
// the service resolves a scoped DbContext per commit via IServiceProvider.
builder.Services.AddSingleton<IChecklistFileMetadataExtractor, ChecklistFileMetadataExtractor>();
builder.Services.AddSingleton<IExcelChecklistImporter, ExcelChecklistImporter>();
builder.Services.AddSingleton<IChecklistImportService, ChecklistImportService>();
// Phase 2 of Checklist Insider — tier-aware verification matcher and bundled
// parallel-family catalog. Matcher uses IServiceProvider for scoped DbContext.
builder.Services.AddSingleton<IChecklistVerificationMatcher, ChecklistVerificationMatcher>();
builder.Services.AddSingleton<IParallelFamilyService, ParallelFamilyService>();
// eBay Seller Hub CSV import (Roadmap #3). Enricher uses OpenRouter for the LLM
// title pass; import service composes the rule parser + enricher + repo upsert.
// Scoped because the import service holds an ICardRepository.
builder.Services.AddScoped<IEbayTitleEnricher, FlipKit.Core.Services.Implementations.OpenRouterEbayTitleEnricher>();
builder.Services.AddScoped<IEbayListingImportService, FlipKit.Core.Services.Implementations.EbayListingImportService>();
// Surprise Set repository — Scoped (consistent with other DbContext-dependent services on Web).
builder.Services.AddScoped<ISurpriseSetRepository, FlipKit.Core.Services.Implementations.SurpriseSetRepository>();
// Validator is pure (no DB, no state) — Singleton is safe and efficient.
builder.Services.AddSingleton<ISurpriseSetValidator, FlipKit.Core.Services.Implementations.SurpriseSets.SurpriseSetValidator>();
// Description generator is pure template logic — no LLM, no DB, no state.
builder.Services.AddSingleton<ISurpriseSetDescriptionGenerator, FlipKit.Core.Services.Implementations.SurpriseSets.SurpriseSetDescriptionGenerator>();
// CSV exporter depends on the Scoped repository, so it must be Scoped too.
builder.Services.AddScoped<ISurpriseSetCsvExporter, FlipKit.Core.Services.Implementations.SurpriseSets.SurpriseSetCsvExporter>();
// Completion service depends on the repository — Scoped.
builder.Services.AddSingleton<IRevenueAllocationService, FlipKit.Core.Services.Implementations.SurpriseSets.RevenueAllocationService>();
builder.Services.AddScoped<ISurpriseSetCompletionService, FlipKit.Core.Services.Implementations.SurpriseSets.SurpriseSetCompletionService>();
// IMemoryCache backs the 2-step Web import preview — parse + LLM enrich on
// upload, stash by GUID token, render the review page, commit reads back by
// token. Avoids re-running the LLM on every commit.
builder.Services.AddMemoryCache();

// Register web-specific services
builder.Services.AddSingleton<IFileDialogService, WebFileUploadService>();
builder.Services.AddSingleton<IBrowserService, JavaScriptBrowserService>();
builder.Services.AddSingleton<INavigationService, MvcNavigationService>();

var app = builder.Build();

// Initialize database (only in local mode)
if (dataMode == DataAccessMode.Local)
{
    var dbPath = FlipKitDbContext.GetDbPath();

    // Enable WAL mode for shared database
    using (var connection = new SqliteConnection($"Data Source={dbPath}"))
    {
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL;";
        command.ExecuteNonQuery();
    }

    // Initialize database (create tables, seed data)
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
        db.Database.EnsureCreated();
        await SchemaUpdater.EnsureVerificationTablesAsync(db);
        await ChecklistSeeder.SeedIfEmptyAsync(db);
    }
    Console.WriteLine("Local database initialization complete");
}
else
{
    Console.WriteLine("Skipping local database initialization (using remote API mode)");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Health check endpoint for server management
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "FlipKit.Web",
    version = "3.1.0",
    timestamp = DateTime.UtcNow
}));

app.Run();
