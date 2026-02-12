using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
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
                services.AddSingleton<IServerManagementService, ServerManagementService>();

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
                    // One-time migration from CardLister to FlipKit
                    if (LegacyMigrator.HasCardListerData())
                    {
                        Log.Information("Detected CardLister data, initiating migration...");
                        if (LegacyMigrator.MigrateFromCardLister())
                        {
                            Log.Information("Successfully migrated data from CardLister to FlipKit");
                        }
                        else
                        {
                            Log.Warning("CardLister migration failed or was skipped");
                        }
                    }

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

                var mainViewModel = _services.GetRequiredService<MainWindowViewModel>();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };

                // System Tray Icon
                var trayIcon = new TrayIcon
                {
                    IsVisible = true,
                    ToolTipText = mainViewModel.TrayTooltip
                };

                // Load app icon for tray
                try
                {
                    var assets = AssetLoader.Open(new Uri("avares://FlipKit.Desktop/Assets/avalonia-logo.ico"));
                    trayIcon.Icon = new WindowIcon(assets);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to load tray icon, using default");
                }

                // Update tooltip when it changes
                mainViewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(mainViewModel.TrayTooltip))
                    {
                        trayIcon.ToolTipText = mainViewModel.TrayTooltip;
                    }
                };

                // Tray menu
                var trayMenu = new NativeMenu();

                var showHideItem = new NativeMenuItem
                {
                    Header = "Show/Hide Window",
                    Command = mainViewModel.ToggleWindowCommand
                };
                trayMenu.Add(showHideItem);

                trayMenu.Add(new NativeMenuItemSeparator());

                // Web Server submenu
                var webServerMenu = new NativeMenu();
                webServerMenu.Add(new NativeMenuItem
                {
                    Header = "Start",
                    Command = mainViewModel.StartWebServerFromTrayCommand
                });
                webServerMenu.Add(new NativeMenuItem
                {
                    Header = "Stop",
                    Command = mainViewModel.StopWebServerFromTrayCommand
                });

                var webServerItem = new NativeMenuItem
                {
                    Header = "Web Server",
                    Menu = webServerMenu
                };
                trayMenu.Add(webServerItem);

                // API Server submenu
                var apiServerMenu = new NativeMenu();
                apiServerMenu.Add(new NativeMenuItem
                {
                    Header = "Start",
                    Command = mainViewModel.StartApiServerFromTrayCommand
                });
                apiServerMenu.Add(new NativeMenuItem
                {
                    Header = "Stop",
                    Command = mainViewModel.StopApiServerFromTrayCommand
                });

                var apiServerItem = new NativeMenuItem
                {
                    Header = "API Server",
                    Menu = apiServerMenu
                };
                trayMenu.Add(apiServerItem);

                trayMenu.Add(new NativeMenuItemSeparator());

                var openBrowserItem = new NativeMenuItem
                {
                    Header = "Open Web Browser",
                    Command = mainViewModel.OpenWebBrowserCommand
                };
                trayMenu.Add(openBrowserItem);

                trayMenu.Add(new NativeMenuItemSeparator());

                var exitItem = new NativeMenuItem
                {
                    Header = "Exit",
                    Command = mainViewModel.ExitApplicationCommand
                };
                trayMenu.Add(exitItem);

                trayIcon.Menu = trayMenu;

                // Handle window visibility changes
                mainViewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(mainViewModel.IsWindowVisible))
                    {
                        if (mainViewModel.IsWindowVisible)
                        {
                            desktop.MainWindow.Show();
                            desktop.MainWindow.Activate();
                        }
                        else
                        {
                            desktop.MainWindow.Hide();
                        }
                    }
                };

                // Handle window close to minimize to tray (if configured)
                desktop.MainWindow.Closing += (s, e) =>
                {
                    var settingsService = _services?.GetService<ISettingsService>();
                    var settings = settingsService?.Load();
                    if (settings?.MinimizeToTray == true)
                    {
                        e.Cancel = true;
                        mainViewModel.IsWindowVisible = false;
                        Log.Information("Window minimized to tray");
                    }
                };

                // Auto-start servers if configured (FlipKit Hub)
                var hubSettings = _services.GetRequiredService<ISettingsService>().Load();
                var serverManagement = _services.GetRequiredService<IServerManagementService>();

                if (hubSettings.AutoStartWebServer || hubSettings.AutoStartApiServer)
                {
                    Log.Information("Auto-starting servers (Web: {Web}, API: {Api})",
                        hubSettings.AutoStartWebServer, hubSettings.AutoStartApiServer);

                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            // Start Web server if enabled
                            if (hubSettings.AutoStartWebServer)
                            {
                                Log.Information("Auto-starting Web server on port {Port}", hubSettings.WebServerPort);
                                var webResult = await serverManagement.StartWebServerAsync(hubSettings.WebServerPort);
                                if (webResult.Success)
                                {
                                    Log.Information("Web server started successfully on port {Port}", webResult.ActualPort);

                                    // Auto-open browser if configured
                                    if (hubSettings.AutoOpenBrowser)
                                    {
                                        await System.Threading.Tasks.Task.Delay(2000); // Wait for server to fully initialize
                                        var browserService = _services.GetRequiredService<IBrowserService>();
                                        browserService.OpenUrl($"http://localhost:{webResult.ActualPort}");
                                    }
                                }
                                else
                                {
                                    Log.Warning("Failed to start Web server: {Error}", webResult.ErrorMessage);
                                }
                            }

                            // Start API server if enabled
                            if (hubSettings.AutoStartApiServer)
                            {
                                Log.Information("Auto-starting API server on port {Port}", hubSettings.ApiServerPort);
                                var apiResult = await serverManagement.StartApiServerAsync(hubSettings.ApiServerPort);
                                if (apiResult.Success)
                                {
                                    Log.Information("API server started successfully on port {Port}", apiResult.ActualPort);
                                }
                                else
                                {
                                    Log.Warning("Failed to start API server: {Error}", apiResult.ErrorMessage);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error during server auto-start");
                        }
                    });
                }

                desktop.ShutdownRequested += async (_, e) =>
                {
                    Log.Information("FlipKit shutting down");

                    try
                    {
                        // Stop any running servers first
                        var serverManagement = _services?.GetService<IServerManagementService>();
                        if (serverManagement != null)
                        {
                            Log.Information("Stopping servers...");
                            await serverManagement.StopWebServerAsync();
                            await serverManagement.StopApiServerAsync();
                        }

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

                        // Delay to ensure server processes are fully terminated
                        await System.Threading.Tasks.Task.Delay(500);
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
