using FlipKit.Core.Helpers;
using FlipKit.Core.Models;

namespace FlipKit.Core.Tests.Helpers;

public class DataAccessModeDetectorTests
{
    // DetectMode: empty/local URLs → Local; anything else → ApiRemote.

    [Fact]
    public void Should_ReturnLocal_When_SyncServerUrlIsNull()
    {
        var settings = new AppSettings { SyncServerUrl = null };
        Assert.Equal(DataAccessMode.Local, DataAccessModeDetector.DetectMode(settings));
    }

    [Fact]
    public void Should_ReturnLocal_When_SyncServerUrlIsWhitespace()
    {
        var settings = new AppSettings { SyncServerUrl = "   " };
        Assert.Equal(DataAccessMode.Local, DataAccessModeDetector.DetectMode(settings));
    }

    [Fact]
    public void Should_ReturnLocal_When_SyncServerUrlPointsAtLocalhost()
    {
        // Localhost URLs are treated as local even if a sync server is technically configured —
        // avoids round-tripping through HTTP for same-machine access.
        var settings = new AppSettings { SyncServerUrl = "http://localhost:5001" };
        Assert.Equal(DataAccessMode.Local, DataAccessModeDetector.DetectMode(settings));
    }

    [Fact]
    public void Should_ReturnLocal_When_SyncServerUrlPointsAtLoopbackIp()
    {
        var settings = new AppSettings { SyncServerUrl = "http://127.0.0.1:5001" };
        Assert.Equal(DataAccessMode.Local, DataAccessModeDetector.DetectMode(settings));
    }

    [Fact]
    public void Should_ReturnApiRemote_When_SyncServerUrlIsTailscaleIp()
    {
        // Typical Tailscale IPs are 100.x.x.x; any non-localhost URL is treated as remote.
        var settings = new AppSettings { SyncServerUrl = "http://100.64.1.5:5001" };
        Assert.Equal(DataAccessMode.ApiRemote, DataAccessModeDetector.DetectMode(settings));
    }

    [Fact]
    public void Should_ReturnApiRemote_When_SyncServerUrlIsCaseMixed()
    {
        // URL is lowercased before substring check, so casing shouldn't matter.
        var settings = new AppSettings { SyncServerUrl = "HTTP://100.64.1.5:5001" };
        Assert.Equal(DataAccessMode.ApiRemote, DataAccessModeDetector.DetectMode(settings));
    }

    // GetModeDescription / IsRemoteMode / IsLocalMode — thin wrappers but worth covering.

    [Fact]
    public void Should_ReturnHumanReadableDescription_When_GivenAMode()
    {
        Assert.Contains("Local", DataAccessModeDetector.GetModeDescription(DataAccessMode.Local));
        Assert.Contains("Remote", DataAccessModeDetector.GetModeDescription(DataAccessMode.ApiRemote));
    }

    [Fact]
    public void Should_ReturnConsistentResults_When_CheckingIsRemoteAndIsLocal()
    {
        var local = new AppSettings { SyncServerUrl = null };
        Assert.True(DataAccessModeDetector.IsLocalMode(local));
        Assert.False(DataAccessModeDetector.IsRemoteMode(local));

        var remote = new AppSettings { SyncServerUrl = "http://100.64.1.5:5001" };
        Assert.False(DataAccessModeDetector.IsLocalMode(remote));
        Assert.True(DataAccessModeDetector.IsRemoteMode(remote));
    }
}
