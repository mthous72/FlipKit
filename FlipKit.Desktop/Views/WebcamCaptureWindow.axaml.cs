using System;
using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using FlipKit.Core.Services;
using FlipKit.Desktop.ViewModels;

namespace FlipKit.Desktop.Views
{
    public partial class WebcamCaptureWindow : Window
    {
        private WriteableBitmap? _previewBitmap;
        private Image? _previewImage;
        private Image? _capturedImage;

        public WebcamCaptureWindow()
        {
            InitializeComponent();

            _previewImage = this.FindControl<Image>("PreviewImage");
            _capturedImage = this.FindControl<Image>("CapturedImage");

            Opened += OnOpened;
            Closed += OnClosed;
        }

        public WebcamCaptureWindow(WebcamCaptureViewModel viewModel) : this()
        {
            DataContext = viewModel;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private async void OnOpened(object? sender, EventArgs e)
        {
            if (DataContext is WebcamCaptureViewModel vm)
                await vm.InitializeAsync();
        }

        private async void OnClosed(object? sender, EventArgs e)
        {
            if (DataContext is WebcamCaptureViewModel vm)
            {
                vm.PropertyChanged -= OnViewModelPropertyChanged;
                await vm.DisposeAsync();
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not WebcamCaptureViewModel vm) return;

            if (e.PropertyName == nameof(WebcamCaptureViewModel.PreviewFrame))
            {
                var frame = vm.PreviewFrame;
                if (frame is null) return;
                Dispatcher.UIThread.Post(() => RenderPreviewFrame(frame));
            }
            else if (e.PropertyName == nameof(WebcamCaptureViewModel.CapturedImagePath))
            {
                var path = vm.CapturedImagePath;
                Dispatcher.UIThread.Post(() => LoadCapturedImage(path));
            }
        }

        private void RenderPreviewFrame(CapturedFrame frame)
        {
            if (_previewImage is null) return;

            // Recreate the bitmap when dimensions change.
            if (_previewBitmap is null
                || _previewBitmap.PixelSize.Width != frame.Width
                || _previewBitmap.PixelSize.Height != frame.Height)
            {
                _previewBitmap?.Dispose();
                _previewBitmap = new WriteableBitmap(
                    new PixelSize(frame.Width, frame.Height),
                    new Vector(96, 96),
                    PixelFormat.Rgba8888,
                    AlphaFormat.Opaque);
                _previewImage.Source = _previewBitmap;
            }

            // Copy RGB → RGBA into the bitmap's framebuffer.
            using (var fb = _previewBitmap.Lock())
            {
                unsafe
                {
                    byte* dst = (byte*)fb.Address;
                    int dstStride = fb.RowBytes;
                    int srcStride = frame.Stride;
                    for (int y = 0; y < frame.Height; y++)
                    {
                        byte* dstRow = dst + (y * dstStride);
                        int srcRowStart = y * srcStride;
                        for (int x = 0; x < frame.Width; x++)
                        {
                            int srcIdx = srcRowStart + (x * 3);
                            int dstIdx = x * 4;
                            dstRow[dstIdx + 0] = frame.Rgb[srcIdx + 0]; // R
                            dstRow[dstIdx + 1] = frame.Rgb[srcIdx + 1]; // G
                            dstRow[dstIdx + 2] = frame.Rgb[srcIdx + 2]; // B
                            dstRow[dstIdx + 3] = 0xFF;                  // A
                        }
                    }
                }
            }

            _previewImage.InvalidateVisual();
        }

        private void LoadCapturedImage(string? path)
        {
            if (_capturedImage is null) return;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _capturedImage.Source = null;
                return;
            }

            try
            {
                using var stream = File.OpenRead(path);
                _capturedImage.Source = new Bitmap(stream);
            }
            catch
            {
                _capturedImage.Source = null;
            }
        }

        private void OnUseThisClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is WebcamCaptureViewModel vm && !string.IsNullOrEmpty(vm.CapturedImagePath))
                Close(vm.CapturedImagePath);
            else
                Close(null);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
