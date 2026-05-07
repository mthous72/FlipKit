using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
using FlipKit.Core.Services.Interfaces;
using FlipKit.Desktop.ViewModels;
using FlipKit.Desktop.Views;
using FlipKit.Desktop.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
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
                // Keep app alive while the splash is visible and init is in progress.
                desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;

                DisableAvaloniaDataAnnotationValidation();

                var splash = new SplashWindow();
                splash.Show();

                // Kick off async init so the splash can actually render before blocking work starts.
                _ = InitializeAsync(desktop, splash);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private async Task InitializeAsync(
            IClassicDesktopStyleApplicationLifetime desktop,
            SplashWindow splash)
        {
            try
            {
                // Yield once so Avalonia can process the splash window's layout/render pass.
                await Task.Yield();

                var services = new ServiceCollection();

                // Logging.
                // dispose: false — the temp service provider built below for settings
                // detection is also built from this ServiceCollection, and its disposal
                // must NOT call Log.CloseAndFlush(). Serilog is flushed explicitly in
                // ShutdownRequested. With dispose: true, the temp provider's disposal
                // silently killed all ILogger<T> calls for the rest of the process
                // (scan service, BulkScanViewModel, model catalog response, etc.).
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddSerilog(dispose: false);
                });

                // Secrets encryption — DPAPI on Windows, file-protected AES key ring on
                // Linux/macOS. Key directory shared with config so both Desktop and Web
                // can decrypt values written by either app.
                var keyDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FlipKit", "DataProtection-Keys");
                services.AddDataProtection()
                    .PersistKeysToFileSystem(new System.IO.DirectoryInfo(keyDir))
                    .SetApplicationName("FlipKit");
                services.AddSingleton<ISecretEncryption, FlipKit.Core.Services.Implementations.DataProtectionSecretEncryption>();

                // Services (order matters - settings service needed first)
                // 3-minute timeout: free models (Gemma 4 31B) can take 90-120s to respond.
                // The default 100s kills legitimate slow-but-valid responses.
                services.AddSingleton<HttpClient>(_ => new HttpClient
                {
                    Timeout = TimeSpan.FromMinutes(3),
                });
                services.AddSingleton<ISettingsService, JsonSettingsService>();
                services.AddSingleton<IBrowserService, SystemBrowserService>();
                services.AddSingleton<IServerManagementService, ServerManagementService>();
                // Phase 5c extraction — network/QR logic split out of SettingsViewModel.
                services.AddSingleton<INetworkInfoProvider, NetworkInfoProvider>();
                services.AddSingleton<INetworkAddressProvider, NetworkAddressProvider>();

                // Smart mode detection - choose between local database or API.
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
                    services.AddTransient<ISkuGenerator, FlipKit.Core.Services.Export.SkuGenerator>();
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
                // Scanner services - Ximilar checked first, then falls back to OpenRouter LLM
                services.AddSingleton<IXimilarService, XimilarService>();
                services.AddSingleton<OpenRouterScannerService>();
                services.AddSingleton<IScannerService, CompositeScannerService>();
                // Live model catalog from OpenRouter — single instance, app-lifetime cache
                services.AddSingleton<IOpenRouterModelCatalog, FlipKit.Core.Services.Scanning.OpenRouterModelCatalog>();
                services.AddSingleton<IPaidModelConsentService, FlipKit.Desktop.Services.AvaloniaPaidModelConsentService>();
                services.AddSingleton<IAiScanConsentService, FlipKit.Desktop.Services.AvaloniaAiScanConsentService>();
                services.AddSingleton<IFileDialogService, AvaloniaFileDialogService>();
                // Webcam capture (Roadmap #2 — Docs/27-WEBCAM-CAPTURE-PLAN.md). The
                // ICameraService probes/opens cameras via OpenCvSharp4; the dialog
                // service owns the modal window so ViewModels stay Avalonia-free.
                services.AddSingleton<ICameraService, OpenCvCameraService>();
                services.AddSingleton<IWebcamCaptureDialogService, WebcamCaptureDialogService>();
                services.AddTransient<IPricerService, PricerService>();
                services.AddSingleton<IImageUploadService, ImgBBUploadService>();
                // Export pipeline — registered unconditionally (no DbContext dependency).
                services.AddSingleton<FlipKit.Core.Services.Export.WhatnotValuesProvider>();
                services.AddSingleton<FlipKit.Core.Services.Export.ShippingProfileNormalizer>();
                services.AddSingleton<FlipKit.Core.Services.Export.WhatnotExporter>();
                services.AddSingleton<FlipKit.Core.Services.Export.ExportValidator>();
                services.AddTransient<IExportService, CsvExportService>();
                services.AddSingleton<TitleTemplateService>();
                services.AddSingleton<IEbayPublishingService, EbayPublishingService>();
                // VariationVerifier takes FlipKitDbContext; must be Scoped so it doesn't
                // capture the first-resolved DbContext for the process lifetime. See
                // AUDIT-2026-05 §4 (D1) for the captive-dependency bug.
                services.AddScoped<IVariationVerifier, VariationVerifierService>();
                services.AddSingleton<IChecklistLearningService, ChecklistLearningService>();
                // Phase 1 of the Checklist Insider import work — parser + service that turns
                // a user-supplied .xlsx into a SetChecklist row. Singletons because nothing
                // here owns DbContext directly; the service resolves a scoped DbContext per
                // commit via IServiceProvider.
                services.AddSingleton<IChecklistFileMetadataExtractor, ChecklistFileMetadataExtractor>();
                services.AddSingleton<IExcelChecklistImporter, ExcelChecklistImporter>();
                services.AddSingleton<IChecklistImportService, ChecklistImportService>();
                // Phase 2 of Checklist Insider — tier-aware verification matcher and
                // bundled parallel-family catalog. Matcher is singleton (uses
                // IServiceProvider for scoped DbContext); parallel service caches the
                // embedded JSON at construction so only one instance is needed.
                services.AddSingleton<IChecklistVerificationMatcher, ChecklistVerificationMatcher>();
                services.AddSingleton<IParallelFamilyService, ParallelFamilyService>();
                services.AddSingleton<IBulkScanErrorLogger, BulkScanErrorLogger>();
                // eBay Seller Hub CSV import (Roadmap #3 — see Docs/17-FUTURE-ROADMAP.md item
                // "eBay Listings Import"). Enricher uses OpenRouter for the title LLM pass;
                // import service composes the rule parser + enricher + repo upsert.
                services.AddTransient<IEbayTitleEnricher, FlipKit.Core.Services.Implementations.OpenRouterEbayTitleEnricher>();
                services.AddTransient<IEbayListingImportService, FlipKit.Core.Services.Implementations.EbayListingImportService>();
                // Surprise Set repository — Transient (owns FlipKitDbContext which is Transient on Desktop).
                services.AddTransient<ISurpriseSetRepository, FlipKit.Core.Services.Implementations.SurpriseSetRepository>();
                // Validator is pure (no DB, no state) — Singleton is safe and efficient.
                services.AddSingleton<ISurpriseSetValidator, FlipKit.Core.Services.Implementations.SurpriseSets.SurpriseSetValidator>();
                // Description generator is pure template logic — no LLM, no DB, no state.
                services.AddSingleton<ISurpriseSetDescriptionGenerator, FlipKit.Core.Services.Implementations.SurpriseSets.SurpriseSetDescriptionGenerator>();
                // CSV exporter depends on the Transient repository, so it must be Transient too.
                services.AddTransient<ISurpriseSetCsvExporter, FlipKit.Core.Services.Implementations.SurpriseSets.SurpriseSetCsvExporter>();
                // Completion service depends on the repository — Transient.
                services.AddSingleton<IRevenueAllocationService, FlipKit.Core.Services.Implementations.SurpriseSets.RevenueAllocationService>();
                services.AddTransient<ISurpriseSetCompletionService, FlipKit.Core.Services.Implementations.SurpriseSets.SurpriseSetCompletionService>();

                // ViewModels
                services.AddSingleton<MainWindowViewModel>();
                services.AddTransient<ScanViewModel>();
                // Singleton so in-flight scans survive tab navigation (see IKeepAliveViewModel).
                services.AddSingleton<BulkScanViewModel>();
                services.AddTransient<InventoryViewModel>();
                services.AddTransient<PricingViewModel>();
                services.AddTransient<ExportViewModel>();
                services.AddTransient<ReportsViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<SetupWizardViewModel>();
                services.AddTransient<RepriceViewModel>();
                services.AddTransient<ChecklistManagerViewModel>();
                services.AddTransient<ImportChecklistViewModel>();
                services.AddTransient<ImportEbayListingsViewModel>();
                services.AddTransient<EditCardViewModel>();
                services.AddTransient<EbayPublishViewModel>();
                services.AddTransient<SurpriseSetListViewModel>();
                services.AddTransient<SurpriseSetDetailViewModel>();

                // Navigation Service (must be after MainWindowViewModel)
                services.AddSingleton<INavigationService, AvaloniaNavigationService>();
                // Notification service — initialized with TopLevel after window opens (see below)
                services.AddSingleton<IAppNotificationService, AvaloniaAppNotificationService>();

                _services = services.BuildServiceProvider();

                // Ensure database is created and seeded (only in local mode).
                // Run on a background thread so the splash stays responsive.
                if (dataMode == DataAccessMode.Local)
                {
                    splash.SetStatus("Initializing database…");
                    try
                    {
                        await Task.Run(() =>
                        {
                            using var scope = _services.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
                            Log.Information("Initializing database at {DbPath}", FlipKitDbContext.GetDbPath());
                            db.Database.EnsureCreated();
                            Log.Debug("Running schema updates");
                            SchemaUpdater.EnsureVerificationTablesAsync(db).GetAwaiter().GetResult();
                            Log.Debug("Running checklist seeder");
                            ChecklistSeeder.SeedIfEmptyAsync(db).GetAwaiter().GetResult();
                            Log.Information("Database initialization complete");
                        });
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

                splash.SetStatus("Loading…");

                var mainViewModel = _services.GetRequiredService<MainWindowViewModel>();
                var mainWindow = new MainWindow { DataContext = mainViewModel };
                desktop.MainWindow = mainWindow;

                // System Tray Icon
                var trayIcon = new TrayIcon
                {
                    IsVisible = true,
                    ToolTipText = mainViewModel.TrayTooltip
                };

                // Load app icon for tray
                try
                {
                    var assets = AssetLoader.Open(new Uri("avares://FlipKit.Desktop/Assets/flipkit.ico"));
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

                // Switch to normal shutdown mode now that MainWindow is fully set up,
                // then show it and dismiss the splash.
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.Show();

                // Initialize in-app notification manager now that TopLevel is live.
                var notificationService = _services.GetRequiredService<IAppNotificationService>();
                notificationService.Initialize(TopLevel.GetTopLevel(mainWindow)!);

                splash.Close();

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

                var serverManagement = _services.GetRequiredService<IServerManagementService>();

                // Always prompt on close — let the user choose between full shutdown and tray.
                // Use a local flag to allow the close to proceed once the user confirms it.
                bool confirmedClose = false;
                mainWindow.Closing += (s, e) =>
                {
                    if (confirmedClose) return; // user already chose Close Everything — let it through
                    e.Cancel = true;
                    _ = HandleMainWindowCloseAsync(mainWindow, mainViewModel, serverManagement, _services, () => confirmedClose = true);
                };

                // Auto-start servers if configured (FlipKit Hub)
                var hubSettings = _services.GetRequiredService<ISettingsService>().Load();

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
                        // Cancel any active scans FIRST so in-flight HTTP requests abort
                        // before the HttpClient/server processes are torn down below.
                        var bulkScanVm = _services?.GetService<BulkScanViewModel>();
                        if (bulkScanVm != null)
                        {
                            Log.Information("Cancelling any active bulk scan...");
                            bulkScanVm.Dispose();
                        }

                        // Stop any running servers
                        var shutdownServerManagement = _services?.GetService<IServerManagementService>();
                        if (shutdownServerManagement != null)
                        {
                            Log.Information("Stopping servers...");
                            await shutdownServerManagement.StopWebServerAsync();
                            await shutdownServerManagement.StopApiServerAsync();
                        }

                        // Unregister global exception handler
                        if (_exceptionHandler != null)
                        {
                            AppDomain.CurrentDomain.UnhandledException -= _exceptionHandler;
                        }

                        // Dispose the service provider (closes DbContext, HttpClient, etc.)
                        // Singletons (including BulkScanViewModel) are disposed here too, but
                        // BulkScanViewModel was already explicitly disposed above for ordering.
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
            catch (Exception ex)
            {
                Log.Fatal(ex, "Startup initialization failed");
                splash.SetStatus($"Startup failed — see logs for details.");
                // Leave splash open briefly so the user can read the message.
                await Task.Delay(4000);
                splash.Close();
            }
        }

        private static async Task HandleMainWindowCloseAsync(
            Window mainWindow,
            MainWindowViewModel mainViewModel,
            IServerManagementService serverManagement,
            IServiceProvider? services,
            Action confirmClose)
        {
            var dialog = new CloseOrMinimizeDialog();
            await dialog.ShowDialog(mainWindow);

            if (dialog.Choice == CloseDialogChoice.CloseAll)
            {
                // Cancel any active scans FIRST so in-flight HTTP requests abort before
                // the server processes are torn down and the HttpClient is disposed.
                var bulkScanVm = services?.GetService<BulkScanViewModel>();
                bulkScanVm?.Dispose();

                // Stop servers here, before the window closes. ShutdownRequested fires
                // as an async void event handler that Avalonia doesn't await, so the
                // process can exit before Stop*Async completes if we wait until then.
                await serverManagement.StopWebServerAsync();
                await serverManagement.StopApiServerAsync();
                confirmClose();
                mainWindow.Close();
            }
            else
            {
                mainViewModel.IsWindowVisible = false;
            }
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
