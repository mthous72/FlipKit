using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using System.Net.Http;
using CardLister.Core.Data;
using CardLister.Core.Services;
using CardLister.Desktop.ViewModels;
using CardLister.Desktop.Views;
using CardLister.Desktop.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace CardLister.Desktop
{
    public partial class App : Application
    {
        private IServiceProvider? _services;

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
                "CardLister", "logs");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    Path.Combine(logDir, "cardlister-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    Path.Combine(fallbackLogDir, "cardlister-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            // Global error logging
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception");
                Log.CloseAndFlush();
            };

            Log.Information("CardLister starting up");

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

                // Database
                services.AddDbContext<CardListerDbContext>(options =>
                    options.UseSqlite($"Data Source={CardListerDbContext.GetDbPath()}"));

                // Services
                services.AddSingleton<HttpClient>();
                services.AddSingleton<ISettingsService, JsonSettingsService>();
                services.AddSingleton<IBrowserService, SystemBrowserService>();
                services.AddTransient<ICardRepository, CardRepository>();
                services.AddSingleton<IScannerService, OpenRouterScannerService>();
                services.AddSingleton<IFileDialogService, AvaloniaFileDialogService>();
                services.AddTransient<IPricerService, PricerService>();
                services.AddSingleton<IImageUploadService, ImgBBUploadService>();
                services.AddTransient<IExportService, CsvExportService>();
                services.AddTransient<IVariationVerifier, VariationVerifierService>();
                services.AddSingleton<IChecklistLearningService, ChecklistLearningService>();
                services.AddSingleton<ISoldPriceService, Point130SoldPriceService>();
                services.AddSingleton<IBulkScanErrorLogger, BulkScanErrorLogger>();
                services.AddSingleton<ISyncService, TailscaleSyncService>();

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

                // Ensure database is created and seeded
                try
                {
                    using var scope = _services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<CardListerDbContext>();
                    Log.Information("Initializing database at {DbPath}", CardListerDbContext.GetDbPath());
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

                desktop.MainWindow = new MainWindow
                {
                    DataContext = _services.GetRequiredService<MainWindowViewModel>()
                };

                // Auto-sync on startup (if enabled)
                Task.Run(async () =>
                {
                    try
                    {
                        var settingsService = _services.GetRequiredService<ISettingsService>();
                        var syncService = _services.GetRequiredService<ISyncService>();
                        var settings = settingsService.Load();

                        if (settings.EnableSync && settings.AutoSyncOnStartup)
                        {
                            Log.Information("Auto-sync on startup enabled, syncing...");
                            var result = await syncService.SyncAsync();
                            if (result.Success)
                            {
                                Log.Information("Startup sync complete: pushed {Pushed}, pulled {Pulled}",
                                    result.CardsPushed, result.CardsPulled);
                            }
                            else
                            {
                                Log.Warning("Startup sync failed: {Error}", result.ErrorMessage);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Startup sync failed");
                    }
                });

                desktop.ShutdownRequested += async (_, e) =>
                {
                    try
                    {
                        var settingsService = _services.GetRequiredService<ISettingsService>();
                        var syncService = _services.GetRequiredService<ISyncService>();
                        var settings = settingsService.Load();

                        if (settings.EnableSync && settings.AutoSyncOnExit)
                        {
                            Log.Information("Auto-sync on exit enabled, syncing...");

                            // Defer shutdown to allow sync to complete
                            var deferral = e.GetDeferral();
                            try
                            {
                                var result = await syncService.SyncAsync();
                                if (result.Success)
                                {
                                    Log.Information("Exit sync complete: pushed {Pushed}, pulled {Pulled}",
                                        result.CardsPushed, result.CardsPulled);
                                }
                                else
                                {
                                    Log.Warning("Exit sync failed: {Error}", result.ErrorMessage);
                                }
                            }
                            finally
                            {
                                deferral.Complete();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exit sync failed");
                    }

                    Log.Information("CardLister shutting down");
                    Log.CloseAndFlush();
                    if (_services is IDisposable disposable)
                        disposable.Dispose();
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
