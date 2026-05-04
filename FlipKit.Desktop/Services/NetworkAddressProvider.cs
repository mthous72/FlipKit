using System;
using System.IO;
using Avalonia.Media.Imaging;
using FlipKit.Core.Services;
using QRCoder;

namespace FlipKit.Desktop.Services
{
    /// <summary>
    /// Snapshot of the current network state — what's available, the URLs to advertise,
    /// the QR codes that encode them, and the user-facing status strings. Returned by
    /// <see cref="INetworkAddressProvider.GetCurrent"/> as one immutable value so callers
    /// can apply it to their bindable state in a single pass without partial-update flicker.
    /// </summary>
    public sealed record NetworkAddressInfo(
        string? LocalNetworkIp,
        string? TailscaleIp,
        bool IsLocalNetworkAvailable,
        bool IsTailscaleAvailable,
        string LocalNetworkStatus,
        string TailscaleStatus,
        string LocalNetworkUrl,
        string TailscaleUrl,
        Bitmap? LocalQrCodeBitmap,
        Bitmap? TailscaleQrCodeBitmap,
        // Legacy single-bitmap properties — preserved for backward compatibility with the
        // older SettingsView XAML that didn't yet split local-vs-Tailscale presentation.
        string LegacyLocalIpAddresses,
        Bitmap? LegacyQrCodeBitmap);

    public interface INetworkAddressProvider
    {
        /// <summary>
        /// Computes the current network address info. URL building requires the actual
        /// web server port + whether the server is running (URLs are only meaningful when
        /// the embedded Web server is up).
        /// </summary>
        NetworkAddressInfo GetCurrent(int actualWebPort, bool isWebRunning);
    }

    /// <summary>
    /// Phase 5c extraction — owns the IP/QR-code logic that was inlined in
    /// <c>SettingsViewModel.UpdateLocalIpAddresses</c>. Keeping this in a service
    /// (rather than the VM) means it can be unit-tested via a stubbed
    /// <see cref="INetworkInfoProvider"/>, and the VM stops carrying the
    /// untestable static <c>NetworkHelper.GetNetworkInfo</c> dependency.
    /// </summary>
    public sealed class NetworkAddressProvider : INetworkAddressProvider
    {
        private readonly INetworkInfoProvider _networkInfo;

        public NetworkAddressProvider(INetworkInfoProvider networkInfo)
        {
            _networkInfo = networkInfo;
        }

        public NetworkAddressInfo GetCurrent(int actualWebPort, bool isWebRunning)
        {
            try
            {
                var info = _networkInfo.GetNetworkInfo();
                return Build(info, actualWebPort, isWebRunning);
            }
            catch (Exception ex)
            {
                // Network enumeration can throw on locked-down systems — return a usable
                // "no network" snapshot rather than letting the exception bubble.
                return Empty($"Error detecting network: {ex.Message}");
            }
        }

        private static NetworkAddressInfo Build(Core.Helpers.NetworkInfo info, int actualWebPort, bool isWebRunning)
        {
            var localIp = info.LocalIpAddress;
            var tailscaleIp = info.TailscaleIpAddress;
            var isLocal = info.IsLocalNetworkAvailable;
            var isTailscale = info.IsTailscaleAvailable;
            var localStatus = isLocal ? "Available" : "No network";
            var tailscaleStatus = isTailscale ? "Connected" : "Not configured";

            string localUrl = string.Empty;
            string tailscaleUrl = string.Empty;
            Bitmap? localQr = null;
            Bitmap? tailscaleQr = null;

            if (isWebRunning)
            {
                if (isLocal && localIp != null)
                {
                    localUrl = $"http://{localIp}:{actualWebPort}";
                    localQr = GenerateQrCodeBitmap(localUrl);
                }
                if (isTailscale && tailscaleIp != null)
                {
                    tailscaleUrl = $"http://{tailscaleIp}:{actualWebPort}";
                    tailscaleQr = GenerateQrCodeBitmap(tailscaleUrl);
                }
            }

            // Legacy single-string + single-bitmap fields — older XAML still binds to these.
            string legacyText;
            Bitmap? legacyQr;
            if (isWebRunning)
            {
                if (isTailscale)
                {
                    legacyText = $"🌐 Tailscale: {tailscaleUrl}";
                    if (isLocal) legacyText += $"\n📱 Local: {localUrl}";
                    legacyQr = tailscaleQr;
                }
                else if (isLocal)
                {
                    legacyText = $"📱 Local Network: {localUrl}";
                    legacyQr = localQr;
                }
                else
                {
                    legacyText = "No network connection";
                    legacyQr = null;
                }
            }
            else
            {
                legacyQr = null;
                if (isTailscale)
                    legacyText = $"🌐 Tailscale IP: {tailscaleIp}\n(Web server not running)";
                else if (isLocal)
                    legacyText = $"📱 Local IP: {localIp}\n(Web server not running)";
                else
                    legacyText = "No network connection";
            }

            return new NetworkAddressInfo(
                LocalNetworkIp: localIp,
                TailscaleIp: tailscaleIp,
                IsLocalNetworkAvailable: isLocal,
                IsTailscaleAvailable: isTailscale,
                LocalNetworkStatus: localStatus,
                TailscaleStatus: tailscaleStatus,
                LocalNetworkUrl: localUrl,
                TailscaleUrl: tailscaleUrl,
                LocalQrCodeBitmap: localQr,
                TailscaleQrCodeBitmap: tailscaleQr,
                LegacyLocalIpAddresses: legacyText,
                LegacyQrCodeBitmap: legacyQr);
        }

        private static NetworkAddressInfo Empty(string statusMessage) => new(
            LocalNetworkIp: null,
            TailscaleIp: null,
            IsLocalNetworkAvailable: false,
            IsTailscaleAvailable: false,
            LocalNetworkStatus: "No network",
            TailscaleStatus: "Not configured",
            LocalNetworkUrl: string.Empty,
            TailscaleUrl: string.Empty,
            LocalQrCodeBitmap: null,
            TailscaleQrCodeBitmap: null,
            LegacyLocalIpAddresses: statusMessage,
            LegacyQrCodeBitmap: null);

        private static Bitmap? GenerateQrCodeBitmap(string url)
        {
            try
            {
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new PngByteQRCode(qrCodeData);
                var qrCodeImage = qrCode.GetGraphic(20); // 20 pixels per module
                using var stream = new MemoryStream(qrCodeImage);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }
    }
}
