using FlipKit.Core.Data;
using FlipKit.Core.Helpers;
using FlipKit.Core.Services;
using FlipKit.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add HttpClient and HttpContextAccessor
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

// Add logging
builder.Services.AddLogging();

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
builder.Services.AddSingleton<IScannerService, OpenRouterScannerService>();
builder.Services.AddScoped<IPricerService, PricerService>(); // Depends on DbContext via repositories
builder.Services.AddScoped<IExportService, CsvExportService>(); // Depends on DbContext
builder.Services.AddSingleton<IImageUploadService, ImgBBUploadService>();
builder.Services.AddScoped<IVariationVerifier, VariationVerifierService>(); // Depends on DbContext
builder.Services.AddSingleton<IChecklistLearningService, ChecklistLearningService>(); // Uses IServiceProvider to create scopes
builder.Services.AddScoped<ISoldPriceService, Point130SoldPriceService>(); // Depends on DbContext
// Note: IEbayBrowseService not yet implemented, will add when ebay-browse-api feature merges

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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
