using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Services;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace FlipKit.Desktop.Services
{
    public class OpenCvCameraService : ICameraService
    {
        private const int MaxProbeIndex = 5;

        private readonly ILogger<OpenCvCameraService>? _logger;

        public OpenCvCameraService(ILogger<OpenCvCameraService>? logger = null)
        {
            _logger = logger;
        }

        public Task<IReadOnlyList<CameraDevice>> ListDevicesAsync(CancellationToken ct = default)
        {
            var devices = new List<CameraDevice>();

            for (int i = 0; i < MaxProbeIndex; i++)
            {
                ct.ThrowIfCancellationRequested();

                VideoCapture? probe = null;
                try
                {
                    probe = new VideoCapture(i);
                    if (!probe.IsOpened())
                        continue;

                    int w = (int)probe.Get(VideoCaptureProperties.FrameWidth);
                    int h = (int)probe.Get(VideoCaptureProperties.FrameHeight);
                    if (w <= 0 || h <= 0)
                    {
                        w = 1280;
                        h = 720;
                    }

                    devices.Add(new CameraDevice(i, $"Camera {i}", w, h));
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Probe failed for device index {Index}", i);
                }
                finally
                {
                    probe?.Release();
                    probe?.Dispose();
                }
            }

            return Task.FromResult<IReadOnlyList<CameraDevice>>(devices);
        }

        public Task<ICameraSession> OpenAsync(int deviceIndex, CancellationToken ct = default)
        {
            var capture = new VideoCapture(deviceIndex);
            if (!capture.IsOpened())
            {
                capture.Dispose();
                throw new InvalidOperationException($"Could not open camera at index {deviceIndex}.");
            }

            // Request the highest resolution the device will give us. OpenCV ignores
            // unsupported settings silently and reports the actual size after.
            capture.Set(VideoCaptureProperties.FrameWidth, 4096);
            capture.Set(VideoCaptureProperties.FrameHeight, 2160);

            int width = (int)capture.Get(VideoCaptureProperties.FrameWidth);
            int height = (int)capture.Get(VideoCaptureProperties.FrameHeight);

            return Task.FromResult<ICameraSession>(new OpenCvCameraSession(capture, width, height, _logger));
        }

        private sealed class OpenCvCameraSession : ICameraSession
        {
            private readonly VideoCapture _capture;
            private readonly ILogger? _logger;
            private readonly SemaphoreSlim _gate = new(1, 1);
            private bool _disposed;

            public OpenCvCameraSession(VideoCapture capture, int width, int height, ILogger? logger)
            {
                _capture = capture;
                Width = width;
                Height = height;
                _logger = logger;
            }

            public int Width { get; }

            public int Height { get; }

            public async Task<CapturedFrame?> ReadFrameAsync(CancellationToken ct = default)
            {
                if (_disposed) return null;

                await _gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    if (_disposed) return null;

                    using var mat = new Mat();
                    if (!_capture.Read(mat) || mat.Empty())
                        return null;

                    // OpenCV returns BGR. Convert to RGB for downstream rendering.
                    using var rgb = new Mat();
                    Cv2.CvtColor(mat, rgb, ColorConversionCodes.BGR2RGB);

                    int w = rgb.Width;
                    int h = rgb.Height;
                    int stride = w * 3;
                    var bytes = new byte[stride * h];
                    System.Runtime.InteropServices.Marshal.Copy(rgb.Data, bytes, 0, bytes.Length);

                    return new CapturedFrame(bytes, w, h, stride);
                }
                finally
                {
                    _gate.Release();
                }
            }

            public async Task<string> CaptureStillAsync(string outputDir, CancellationToken ct = default)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(OpenCvCameraSession));

                Directory.CreateDirectory(outputDir);

                await _gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    if (_disposed) throw new ObjectDisposedException(nameof(OpenCvCameraSession));

                    using var mat = new Mat();
                    // Drain a frame or two so we don't get a stale buffered image.
                    for (int i = 0; i < 2; i++)
                    {
                        if (!_capture.Read(mat) || mat.Empty())
                            throw new InvalidOperationException("Camera returned no frame on capture.");
                    }

                    string path = Path.Combine(outputDir, $"webcam-{DateTime.Now:yyyyMMdd-HHmmss-fff}.jpg");
                    var parameters = new[] { (int)ImwriteFlags.JpegQuality, 92 };
                    if (!Cv2.ImWrite(path, mat, parameters))
                        throw new InvalidOperationException($"Failed to write JPG to {path}.");

                    return path;
                }
                finally
                {
                    _gate.Release();
                }
            }

            public async ValueTask DisposeAsync()
            {
                if (_disposed) return;
                _disposed = true;

                await _gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    _capture.Release();
                    _capture.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Error releasing camera capture");
                }
                finally
                {
                    _gate.Release();
                    _gate.Dispose();
                }
            }
        }
    }
}
