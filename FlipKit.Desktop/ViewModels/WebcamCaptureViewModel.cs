using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlipKit.Core.Services;

namespace FlipKit.Desktop.ViewModels
{
    /// <summary>
    /// Drives <see cref="Views.WebcamCaptureWindow"/>. Dialog-only — inherits
    /// <see cref="ObservableObject"/> directly to stay out of the ViewLocator's
    /// "every page needs a matching view" smoke test (same pattern as
    /// <see cref="ImportChecklistViewModel"/>).
    /// </summary>
    public partial class WebcamCaptureViewModel : ObservableObject, IAsyncDisposable
    {
        private readonly ICameraService _cameraService;
        private readonly string _captureDir;

        private ICameraSession? _session;
        private CancellationTokenSource? _previewCts;
        private Task? _previewTask;

        [ObservableProperty] private ObservableCollection<CameraDevice> _devices = new();
        [ObservableProperty] private CameraDevice? _selectedDevice;
        [ObservableProperty] private CapturedFrame? _previewFrame;
        [ObservableProperty] private string? _capturedImagePath;
        [ObservableProperty] private bool _isPreviewing;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string? _statusMessage;
        [ObservableProperty] private string? _errorMessage;

        public WebcamCaptureViewModel(ICameraService cameraService, string captureDir)
        {
            _cameraService = cameraService;
            _captureDir = captureDir;
        }

        public bool HasCapturedImage => !string.IsNullOrEmpty(CapturedImagePath);

        partial void OnCapturedImagePathChanged(string? value) => OnPropertyChanged(nameof(HasCapturedImage));

        partial void OnSelectedDeviceChanged(CameraDevice? value)
        {
            if (value is null) return;
            _ = OpenSelectedDeviceAsync();
        }

        [RelayCommand]
        public async Task InitializeAsync()
        {
            ErrorMessage = null;
            StatusMessage = "Looking for cameras…";
            IsBusy = true;
            try
            {
                var devs = await _cameraService.ListDevicesAsync().ConfigureAwait(true);
                Devices = new ObservableCollection<CameraDevice>(devs);

                if (Devices.Count == 0)
                {
                    StatusMessage = null;
                    ErrorMessage = "No camera found. Connect a webcam and reopen this window.";
                    return;
                }

                StatusMessage = null;
                SelectedDevice = Devices[0];
            }
            catch (Exception ex)
            {
                StatusMessage = null;
                ErrorMessage = $"Failed to enumerate cameras: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task OpenSelectedDeviceAsync()
        {
            await StopPreviewAsync().ConfigureAwait(true);

            if (SelectedDevice is null) return;

            ErrorMessage = null;
            StatusMessage = $"Opening {SelectedDevice.Name}…";
            IsBusy = true;
            try
            {
                _session = await _cameraService.OpenAsync(SelectedDevice.Index).ConfigureAwait(true);
                StartPreviewLoop();
                StatusMessage = null;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Could not open {SelectedDevice.Name}: {ex.Message}";
                StatusMessage = null;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void StartPreviewLoop()
        {
            if (_session is null) return;

            _previewCts = new CancellationTokenSource();
            IsPreviewing = true;
            CapturedImagePath = null;

            var ct = _previewCts.Token;
            var session = _session;

            _previewTask = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var frame = await session.ReadFrameAsync(ct).ConfigureAwait(false);
                        if (frame is null)
                        {
                            await Task.Delay(50, ct).ConfigureAwait(false);
                            continue;
                        }

                        Avalonia.Threading.Dispatcher.UIThread.Post(() => PreviewFrame = frame);
                        // Cap preview around ~20fps to keep UI thread happy.
                        await Task.Delay(50, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            ErrorMessage = $"Preview error: {ex.Message}";
                            IsPreviewing = false;
                        });
                        break;
                    }
                }
            }, ct);
        }

        private async Task StopPreviewAsync()
        {
            IsPreviewing = false;

            var cts = _previewCts;
            var task = _previewTask;
            _previewCts = null;
            _previewTask = null;

            if (cts is not null)
            {
                try { cts.Cancel(); } catch { }
            }

            if (task is not null)
            {
                try { await task.ConfigureAwait(true); } catch { }
            }

            cts?.Dispose();

            if (_session is not null)
            {
                var s = _session;
                _session = null;
                try { await s.DisposeAsync().ConfigureAwait(true); } catch { }
            }
        }

        [RelayCommand]
        public async Task CaptureAsync()
        {
            if (_session is null) return;

            ErrorMessage = null;
            IsBusy = true;
            try
            {
                // Pause preview so the still grab gets the device exclusively.
                _previewCts?.Cancel();
                if (_previewTask is not null)
                {
                    try { await _previewTask.ConfigureAwait(true); } catch { }
                }
                IsPreviewing = false;

                Directory.CreateDirectory(_captureDir);
                CapturedImagePath = await _session.CaptureStillAsync(_captureDir).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Capture failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void Retake()
        {
            CapturedImagePath = null;
            ErrorMessage = null;
            if (_session is not null)
                StartPreviewLoop();
        }

        public async ValueTask DisposeAsync()
        {
            await StopPreviewAsync().ConfigureAwait(false);
        }
    }
}
