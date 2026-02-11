using System;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using FlipKit.ViewModels;
using FlipKit.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ScreenshotTool
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("FlipKit Screenshot Tool - Demo Mode");
            Console.WriteLine("======================================\n");

            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "screenshots");
            Directory.CreateDirectory(outputDir);
            Console.WriteLine($"Output directory: {outputDir}\n");

            try
            {
                // Build Avalonia app
                var appBuilder = BuildAvaloniaApp();

                // Start headless (non-blocking)
                using var lifetime = appBuilder.SetupWithLifetime(new ClassicDesktopStyleApplicationLifetime
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                });

                lifetime.Start(Array.Empty<string>());

                Console.WriteLine("Avalonia initialized in headless mode\n");

                // Set up DI container with mock services
                var services = SetupServices();

                // Capture screenshots
                CaptureSettingsView(services, Path.Combine(outputDir, "01-settings-view.png"));
                CaptureScanView(services, Path.Combine(outputDir, "02-scan-view.png"));
                CapturePricingView(services, Path.Combine(outputDir, "03-pricing-view.png"));
                CaptureInventoryView(services, Path.Combine(outputDir, "04-inventory-view.png"));
                CaptureExportView(services, Path.Combine(outputDir, "05-export-view.png"));

                Console.WriteLine("\n✅ All screenshots captured successfully!");
                Console.WriteLine($"📁 Location: {outputDir}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        static ServiceProvider SetupServices()
        {
            var services = new ServiceCollection();

            // Logging
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

            // Mock services
            services.AddSingleton<FlipKit.Services.ICardRepository, MockCardRepository>();
            services.AddSingleton<FlipKit.Services.ISettingsService, MockSettingsService>();
            services.AddSingleton<FlipKit.Services.IScannerService, MockScannerService>();
            services.AddSingleton<FlipKit.Services.IPricerService, MockPricerService>();
            services.AddSingleton<FlipKit.Services.IExportService, MockExportService>();
            services.AddSingleton<FlipKit.Services.IBrowserService, MockBrowserService>();
            services.AddSingleton<FlipKit.Services.IFileDialogService, MockFileDialogService>();
            services.AddSingleton<FlipKit.Services.IImageUploadService, MockImageUploadService>();
            services.AddSingleton<FlipKit.Services.IVariationVerifier, MockVariationVerifier>();
            services.AddSingleton<FlipKit.Services.IChecklistLearningService, MockChecklistLearningService>();
            services.AddSingleton<FlipKit.Services.ISoldPriceService, MockSoldPriceService>();
            services.AddSingleton<FlipKit.Services.IEbayBrowseService, MockEbayBrowseService>();

            // ViewModels
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<ScanViewModel>();
            services.AddTransient<PricingViewModel>();
            services.AddTransient<InventoryViewModel>();
            services.AddTransient<ExportViewModel>();

            return services.BuildServiceProvider();
        }

        static void CaptureSettingsView(ServiceProvider services, string outputPath)
        {
            Console.WriteLine("📸 Capturing SettingsView...");
            var vm = services.GetRequiredService<SettingsViewModel>();
            var view = new SettingsView { DataContext = vm };
            RenderAndSave(view, outputPath, 900, 1000);
        }

        static void CaptureScanView(ServiceProvider services, string outputPath)
        {
            Console.WriteLine("📸 Capturing ScanView...");
            var vm = services.GetRequiredService<ScanViewModel>();
            var view = new ScanView { DataContext = vm };
            RenderAndSave(view, outputPath, 1400, 900);
        }

        static void CapturePricingView(ServiceProvider services, string outputPath)
        {
            Console.WriteLine("📸 Capturing PricingView...");
            var vm = services.GetRequiredService<PricingViewModel>();

            // Wait a moment for async initialization
            Thread.Sleep(500);

            var view = new PricingView { DataContext = vm };
            RenderAndSave(view, outputPath, 1000, 1000);
        }

        static void CaptureInventoryView(ServiceProvider services, string outputPath)
        {
            Console.WriteLine("📸 Capturing InventoryView...");
            var vm = services.GetRequiredService<InventoryViewModel>();

            // Wait for async load
            Thread.Sleep(500);

            var view = new InventoryView { DataContext = vm };
            RenderAndSave(view, outputPath, 1400, 800);
        }

        static void CaptureExportView(ServiceProvider services, string outputPath)
        {
            Console.WriteLine("📸 Capturing ExportView...");
            var vm = services.GetRequiredService<ExportViewModel>();
            var view = new ExportView { DataContext = vm };
            RenderAndSave(view, outputPath, 1000, 800);
        }

        static void RenderAndSave(Control view, string outputPath, int width, int height)
        {
            try
            {
                // Execute on UI thread
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Set size
                    view.Width = width;
                    view.Height = height;

                    // Measure and arrange
                    view.Measure(new Size(width, height));
                    view.Arrange(new Rect(0, 0, width, height));

                    // Force layout pass
                    view.UpdateLayout();

                    // Render to bitmap
                    var pixelSize = new PixelSize(width, height);
                    var dpi = new Vector(96, 96);

                    using var bitmap = new RenderTargetBitmap(pixelSize, dpi);
                    bitmap.Render(view);

                    // Save
                    bitmap.Save(outputPath);

                    Console.WriteLine($"  ✓ Saved: {Path.GetFileName(outputPath)} ({width}x{height})");
                }).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Failed: {ex.Message}");
            }
        }

        static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<Application>()
                .UsePlatformDetect()
                .With(new FluentTheme())
                .LogToTrace()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = true
                });
        }
    }
}
