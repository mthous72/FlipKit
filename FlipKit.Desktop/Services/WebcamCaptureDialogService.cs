using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using FlipKit.Core.Services;
using FlipKit.Desktop.ViewModels;
using FlipKit.Desktop.Views;

namespace FlipKit.Desktop.Services
{
    /// <summary>
    /// Avalonia implementation of <see cref="IWebcamCaptureDialogService"/> — opens
    /// the modal <see cref="WebcamCaptureWindow"/> and returns the JPG path the user
    /// accepted, or null when they cancelled / there was no main window to own the
    /// dialog. Captures land in <c>%LocalAppData%/FlipKit/webcam-captures</c> so they
    /// survive past the dialog close (Save uploads them via ImgBB later).
    /// </summary>
    public class WebcamCaptureDialogService : IWebcamCaptureDialogService
    {
        private readonly ICameraService _cameraService;
        private readonly ISettingsService _settingsService;

        public WebcamCaptureDialogService(ICameraService cameraService, ISettingsService settingsService)
        {
            _cameraService = cameraService;
            _settingsService = settingsService;
        }

        public async Task<string?> CaptureAsync()
        {
            try
            {
                return await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var owner = GetMainWindow();
                    if (owner == null) return null;

                    var settings = _settingsService.Load();
                    var captureDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "FlipKit", "webcam-captures");

                    var vm = new WebcamCaptureViewModel(
                        _cameraService,
                        captureDir,
                        settings.PreferredCameraIndex,
                        settings.PreferredCameraName);
                    var dialog = new WebcamCaptureWindow(vm);
                    var result = await dialog.ShowDialog<string?>(owner);

                    // Persist the device the user actually used so the next open
                    // defaults to it. Only update when something is selected.
                    if (vm.SelectedDevice is { } chosen
                        && (chosen.Index != settings.PreferredCameraIndex
                            || chosen.Name != settings.PreferredCameraName))
                    {
                        settings.PreferredCameraIndex = chosen.Index;
                        settings.PreferredCameraName = chosen.Name;
                        _settingsService.Save(settings);
                    }

                    return result;
                });
            }
            catch
            {
                return null;
            }
        }

        private static Window? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;
            return null;
        }
    }
}
