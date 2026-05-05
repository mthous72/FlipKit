using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FlipKit.Core.Services
{
    public interface ICameraService
    {
        Task<IReadOnlyList<CameraDevice>> ListDevicesAsync(CancellationToken ct = default);

        Task<ICameraSession> OpenAsync(int deviceIndex, CancellationToken ct = default);
    }

    public sealed record CameraDevice(int Index, string Name, int MaxWidth, int MaxHeight);

    public sealed record CapturedFrame(byte[] Rgb, int Width, int Height, int Stride);

    public interface ICameraSession : IAsyncDisposable
    {
        int Width { get; }

        int Height { get; }

        Task<CapturedFrame?> ReadFrameAsync(CancellationToken ct = default);

        Task<string> CaptureStillAsync(string outputDir, CancellationToken ct = default);
    }
}
