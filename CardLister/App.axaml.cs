using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using System.Net.Http;
using FlipKit.Core.Data;
using FlipKit.Core.Helpers;
using FlipKit.Core.Services;
using FlipKit.Desktop.ViewModels;
using FlipKit.Desktop.Views;
using FlipKit.Desktop.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace FlipKit.Desktop
{
    public partial class App : Application
    {
        private IServiceProvider? _services;
        private UnhandledExceptionEventHandler? _exceptionHandler;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Configure Serilog — writes to Docs/debug/ in the project directory
            var logDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Docs", "debug");
            // Also write to a predictable location for published builds
            var fallbackLogDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlipKit", "logs");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    Path.Combine(logDir, "flipkit-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    Path.Combine(fallbackLogDir, "flipkit-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            // Global error logging
            _exceptionHandler = (_, e) =>
            {
                Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception");
                Log.CloseAndFlush();
            };
            AppDomain.CurrentDomain.UnhandledException += _exceptionHandler;

            Log.Information("FlipKit starting up");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;

                DisableAvaloniaDataAnnotationValidation();

                var services = new ServiceCollection();

                // Logging
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddSerilog(dispose: true);
                });

                // Services (order matters - settings service needed first)
                services.AddSingleton<HttpClient>();
                services.AddSingleton<ISettingsService, JsonSettingsService>();
                services.AddSingleton<IBrowserService, SystemBrowserService>();

                // Smart mode detection - choose between local database or API
                using var tempProvider = services.BuildServiceProvider();
                var settingsService = tempProvider.GetRequiredService<ISettingsService>();
                var settings = settingsService.Load();
                var dataMode = DataAccessModeDetector.DetectMode(settings);

                if (dataMode == DataAccessMode.Local)
                {
                    // Local mode - direct database access (fast)
                    Log.Information("Data access mode: LOCAL (Direct SQLite)");
                    services.AddDbContext<FlipKitDbContext>(options =>
                        options.UseSqlite($"Data Source={FlipKitDbContext.GetDbPath()}"));
                    services.AddTransient<ICardRepository, CardRepository>();
                }
                else
                {
                    // Remote mode - API calls via Tailscale
                    Log.Information("Data access mode: REMOTE API ({ApiUrl})", settings.SyncServerUrl);
                    // No DbContext needed in remote mode
                    services.AddSingleton<ICardRepository>(sp =>
                    {
                        var httpClient = sp.GetRequiredService<HttpClient>();
                        var logger = sp.GetRequiredService<ILogger<ApiCardRepository>>();
                        return new ApiCardRepository(httpClient, settings.SyncServerUrl!, logger);
                    });
                }
                services.AddSingleton<IScannerService, OpenRouterScannerService>();
                services.AddSingleton<IFileDialogService, AvaloniaFileDialogService>();
                services.AddTransient<IPricerService, PricerService>();
                services.AddSingleton<IImageUploadService, ImgBBUploadService>();
                services.AddTransient<IExportService, CsvExportService>();
                services.AddTransient<IVariationVerifier, VariationVerifierService>();
                services.AddSingleton<IChecklistLearningService, ChecklistLearningService>();
                services.AddSingleton<ISoldPriceService, Point130SoldPriceService>();
                services.AddSingleton<IBulkScanErrorLogger, BulkScanErrorLogger>();

                // ViewModels
                services.AddSingleton<MainWindowViewModel>();
                services.AddTransient<ScanViewModel>();
                services.AddTransient<BulkScanViewModel>();
                services.AddTransient<InventoryViewModel>();
                services.AddTransient<PricingViewModel>();
                services.AddTransient<ExportViewModel>();
                services.AddTransient<ReportsViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<SetupWizardViewModel>();
                services.AddTransient<RepriceViewModel>();
                services.AddTransient<ChecklistManagerViewModel>();
                services.AddTransient<EditCardViewModel>();

                // Navigation Service (must be after MainWindowViewModel)
                services.AddSingleton<INavigationService, AvaloniaNavigationService>();

                _services = services.BuildServiceProvider();

                // Ensure database is created and seeded (only in local mode)
                if (dataMode == DataAccessMode.Local)
                {
                    try
                    {
                        using var scope = _services.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
                        Log.Information("Initializing database at {DbPath}", FlipKitDbContext.GetDbPath());
                        db.Database.EnsureCreated();
                    Log.Debug("Running schema updates");
                    SchemaUpdater.EnsureVerificationTablesAsync(db).GetAwaiter().GetResult();
                    // Disabled sample card seeding - users don't want auto-generated cards
                    // Log.Debug("Running database seeder");
                    // DatabaseSeeder.SeedIfEmptyAsync(db).GetAwaiter().GetResult();
                    Log.Debug("Running checklist seeder");
                    ChecklistSeeder.SeedIfEmptyAsync(db).GetAwaiter().GetResult();
                    Log.Information("Database initialization complete");
                }
                    catch (Exception ex)
                    {
                        Log.Fatal(ex, "Database initialization failed");
                        throw;
                    }
                }
                else
                {
                    Log.Information("Skipping local database initialization (using remote API mode)");
                }

                desktop.MainWindow = new MainWindow
                {
                    DataContext = _services.GetRequiredService<MainWindowViewModel>()
                };

                desktop.ShutdownRequested += async (_, e) =>
                {
                    Log.Information("FlipKit shutting down");

                    try
                    {
                        // Unregister global exception handler
                        if (_exceptionHandler != null)
                        {
                            AppDomain.CurrentDomain.UnhandledException -= _exceptionHandler;
                        }

                        // Dispose ViewModels that implement IDisposable (e.g., BulkScanViewModel)
                        if (desktop.MainWindow?.DataContext is MainWindowViewModel mainViewModel)
                        {
                            // Cancel any pending operations in BulkScanViewModel
                            if (mainViewModel.CurrentPage is IDisposable disposableViewModel)
                            {
                                disposableViewModel.Dispose();
                            }
                        }

                        // Dispose the service provider (closes DbContext, HttpClient, etc.)
                        if (_services is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }

                        // Small delay to ensure async operations complete
                        await System.Threading.Tasks.Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error during shutdown cleanup");
                    }
                    finally
                    {
                        Log.Information("Shutdown complete");
                        Log.CloseAndFlush();
                    }
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}
