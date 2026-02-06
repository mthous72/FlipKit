using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using System.Net.Http;
using CardLister.Data;
using CardLister.Services;
using CardLister.ViewModels;
using CardLister.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CardLister
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Global error logging
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                LogError("Unhandled", e.ExceptionObject as Exception);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;

                DisableAvaloniaDataAnnotationValidation();

                var services = new ServiceCollection();

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

                // ViewModels
                services.AddSingleton<MainWindowViewModel>();
                services.AddTransient<ScanViewModel>();
                services.AddTransient<InventoryViewModel>();
                services.AddTransient<PricingViewModel>();
                services.AddTransient<ExportViewModel>();
                services.AddTransient<ReportsViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<SetupWizardViewModel>();
                services.AddTransient<RepriceViewModel>();

                Services = services.BuildServiceProvider();

                // Ensure database is created and seeded
                using (var scope = Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<CardListerDbContext>();
                    db.Database.EnsureCreated();
                    SchemaUpdater.EnsureVerificationTablesAsync(db).GetAwaiter().GetResult();
                    DatabaseSeeder.SeedIfEmptyAsync(db).GetAwaiter().GetResult();
                    ChecklistSeeder.SeedIfEmptyAsync(db).GetAwaiter().GetResult();
                }

                desktop.MainWindow = new MainWindow
                {
                    DataContext = Services.GetRequiredService<MainWindowViewModel>()
                };

                desktop.ShutdownRequested += (_, _) =>
                {
                    if (Services is IDisposable disposable)
                        disposable.Dispose();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void LogError(string source, Exception? ex)
        {
            if (ex == null) return;
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CardLister", "logs");
                Directory.CreateDirectory(logDir);
                var logFile = Path.Combine(logDir, $"error-{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(logFile,
                    $"[{DateTime.Now:HH:mm:ss}] [{source}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n");
            }
            catch
            {
                // Ignore logging failures
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
