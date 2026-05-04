using FlipKit.Core.Helpers;
using FlipKit.Core.Services;
using FlipKit.Desktop.Services;
using NSubstitute;

namespace FlipKit.Desktop.Tests.Services;

/// <summary>
/// Phase 5c — verifies the network address logic that was previously inlined in
/// SettingsViewModel.UpdateLocalIpAddresses. The provider takes an INetworkInfoProvider
/// stub so these tests don't touch real network adapters.
/// </summary>
public class NetworkAddressProviderTests
{
    private static NetworkAddressProvider Create(NetworkInfo info)
    {
        var stub = Substitute.For<INetworkInfoProvider>();
        stub.GetNetworkInfo().Returns(info);
        return new NetworkAddressProvider(stub);
    }

    private static NetworkInfo Both(string local = "192.168.1.10", string tailscale = "100.64.1.5") =>
        new() { LocalIpAddress = local, TailscaleIpAddress = tailscale };

    [Fact]
    public void Should_BuildUrlsWithProvidedPort_When_WebRunningAndBothNetworksAvailable()
    {
        var sut = Create(Both());

        var info = sut.GetCurrent(actualWebPort: 7777, isWebRunning: true);

        Assert.Equal("http://192.168.1.10:7777", info.LocalNetworkUrl);
        Assert.Equal("http://100.64.1.5:7777", info.TailscaleUrl);
        Assert.True(info.IsLocalNetworkAvailable);
        Assert.True(info.IsTailscaleAvailable);
        // NB: QR bitmaps are built via Avalonia.Bitmap which requires AppBuilder to be
        // initialized — tests run without it, so the catch in GenerateQrCodeBitmap
        // swallows the exception and returns null. Asserted in the headless smoke
        // tests if/when that test layer gets richer.
    }

    [Fact]
    public void Should_LeaveUrlsEmptyAndShowOfflineMessage_When_WebNotRunning()
    {
        var sut = Create(Both());

        var info = sut.GetCurrent(actualWebPort: 5000, isWebRunning: false);

        Assert.Equal(string.Empty, info.LocalNetworkUrl);
        Assert.Equal(string.Empty, info.TailscaleUrl);
        Assert.Null(info.LocalQrCodeBitmap);
        Assert.Null(info.TailscaleQrCodeBitmap);
        // Tailscale is preferred in the legacy single-string field.
        Assert.Contains("Tailscale IP", info.LegacyLocalIpAddresses);
        Assert.Contains("Web server not running", info.LegacyLocalIpAddresses);
    }

    [Fact]
    public void Should_ReportNoNetwork_When_NeitherInterfaceDetected()
    {
        var sut = Create(new NetworkInfo()); // both null

        var info = sut.GetCurrent(actualWebPort: 5000, isWebRunning: true);

        Assert.False(info.IsLocalNetworkAvailable);
        Assert.False(info.IsTailscaleAvailable);
        Assert.Equal("No network", info.LocalNetworkStatus);
        Assert.Equal("Not configured", info.TailscaleStatus);
        Assert.Equal("No network connection", info.LegacyLocalIpAddresses);
        Assert.Null(info.LegacyQrCodeBitmap);
    }

    [Fact]
    public void Should_PreferTailscaleInLegacyText_When_BothNetworksRunning()
    {
        // Older XAML binds to the single LocalIpAddresses string; we standardize on
        // showing Tailscale (remote access = higher-value default) when both are up.
        var sut = Create(Both());

        var info = sut.GetCurrent(actualWebPort: 5000, isWebRunning: true);

        Assert.Contains("Tailscale", info.LegacyLocalIpAddresses);
    }

    [Fact]
    public void Should_OnlyShowLocalUrl_When_TailscaleUnavailable()
    {
        var sut = Create(new NetworkInfo { LocalIpAddress = "192.168.1.10" });

        var info = sut.GetCurrent(actualWebPort: 5000, isWebRunning: true);

        Assert.True(info.IsLocalNetworkAvailable);
        Assert.False(info.IsTailscaleAvailable);
        Assert.Equal("http://192.168.1.10:5000", info.LocalNetworkUrl);
        Assert.Equal(string.Empty, info.TailscaleUrl);
        Assert.Contains("Local Network", info.LegacyLocalIpAddresses);
    }

    [Fact]
    public void Should_ReturnErrorSnapshot_When_ProviderThrows()
    {
        var stub = Substitute.For<INetworkInfoProvider>();
        stub.GetNetworkInfo().Returns<NetworkInfo>(_ => throw new Exception("network down"));
        var sut = new NetworkAddressProvider(stub);

        var info = sut.GetCurrent(actualWebPort: 5000, isWebRunning: true);

        Assert.Contains("network down", info.LegacyLocalIpAddresses);
        Assert.False(info.IsLocalNetworkAvailable);
        Assert.False(info.IsTailscaleAvailable);
    }
}
