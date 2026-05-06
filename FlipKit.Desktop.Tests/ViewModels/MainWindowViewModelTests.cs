using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Desktop.Tests.ViewModels;

internal static class NSubstituteExtensions
{
    /// <summary>One-line setup-and-return for substitute configuration.</summary>
    public static T Tap<T>(this T target, Action<T> configure)
    {
        configure(target);
        return target;
    }
}

public class MainWindowViewModelTests
{

    /// <summary>
    /// Test-only IDisposable ViewModel used to verify page-disposal lifecycle.
    /// Not used as the constructor's resolved page (which requires real ScanViewModel
    /// or SetupWizardViewModel types — see <see cref="BuildScanVm"/> / <see cref="BuildWizardVm"/>).
    /// </summary>
    private sealed class StubViewModel : ViewModelBase, IDisposable
    {
        public bool WasDisposed { get; private set; }
        public void Dispose() => WasDisposed = true;
    }

    /// <summary>
    /// Builds a real ScanViewModel with all-mock deps. MainWindowViewModel's
    /// GetRequiredService&lt;ScanViewModel&gt;() does a hard cast that fails on a
    /// generic ViewModelBase stub — we need the real type. The deps are mocked so the
    /// VM has no functional behavior; we only care that the type satisfies the cast.
    /// </summary>
    private static ScanViewModel BuildScanVm() =>
        new(Substitute.For<IScannerService>(),
            Substitute.For<ICardRepository>(),
            Substitute.For<IFileDialogService>(),
            Substitute.For<ISettingsService>().Tap(s => s.Load().Returns(new AppSettings())),
            Substitute.For<IVariationVerifier>(),
            Substitute.For<IChecklistLearningService>(),
            Substitute.For<IChecklistVerificationMatcher>(),
            Substitute.For<IOpenRouterModelCatalog>().Tap(m => m.GetAsync(default).ReturnsForAnyArgs(Task.FromResult(
                new ModelCatalog(Array.Empty<OpenRouterModel>(), Array.Empty<OpenRouterModel>(), DateTime.UtcNow)))),
            Substitute.For<IPaidModelConsentService>(),
            Substitute.For<IAiScanConsentService>(),
            Substitute.For<IImageUploadService>(),
            Substitute.For<IBrowserService>(),
            Substitute.For<IWebcamCaptureDialogService>(),
            NullLogger<ScanViewModel>.Instance);

    private static SetupWizardViewModel BuildWizardVm() =>
        new(Substitute.For<ISettingsService>(), Substitute.For<IBrowserService>());

    private sealed record TestContext(
        MainWindowViewModel Vm,
        ISettingsService Settings,
        IServerManagementService ServerMgmt,
        IServiceProvider Services,
        IBrowserService Browser,
        INavigationService Navigation);

    /// <summary>
    /// Builds a MainWindowViewModel with NSubstitute mocks for every collaborator.
    /// IServiceProvider is wired so resolutions for SetupWizardViewModel / ScanViewModel
    /// return fresh StubViewModel instances — avoids constructing the real VMs (which
    /// have their own deep dependency chains we don't care about for these tests).
    /// </summary>
    private static TestContext Create(
        bool hasValidConfig = true,
        ServerStatus? serverStatus = null,
        IBrowserService? browser = null,
        INavigationService? navigation = null)
    {
        var settings = Substitute.For<ISettingsService>();
        settings.HasValidConfig().Returns(hasValidConfig);
        settings.Load().Returns(new AppSettings { WebServerPort = 5000, ApiServerPort = 5001 });

        var serverMgmt = Substitute.For<IServerManagementService>();
        serverMgmt.GetServerStatus().Returns(serverStatus ?? new ServerStatus
        {
            IsWebRunning = false, IsApiRunning = false, WebPort = 5000, ApiPort = 5001,
        });

        browser ??= Substitute.For<IBrowserService>();
        navigation ??= Substitute.For<INavigationService>();

        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(SetupWizardViewModel)).Returns(_ => BuildWizardVm());
        services.GetService(typeof(ScanViewModel)).Returns(_ => BuildScanVm());
        services.GetService(typeof(INavigationService)).Returns(navigation);
        services.GetService(typeof(IBrowserService)).Returns(browser);

        var vm = new MainWindowViewModel(services, settings, serverMgmt);
        return new TestContext(vm, settings, serverMgmt, services, browser, navigation);
    }

    // === Constructor branching ===

    [Fact]
    public void Should_LoadSetupWizardAndHideSidebar_When_ConfigIsInvalid()
    {
        var ctx = Create(hasValidConfig: false);

        Assert.False(ctx.Vm.ShowSidebar);
        ctx.Services.Received(1).GetService(typeof(SetupWizardViewModel));
        ctx.Services.DidNotReceive().GetService(typeof(ScanViewModel));
    }

    [Fact]
    public void Should_LoadScanViewModelAndShowSidebar_When_ConfigIsValid()
    {
        var ctx = Create(hasValidConfig: true);

        Assert.True(ctx.Vm.ShowSidebar);
        ctx.Services.Received(1).GetService(typeof(ScanViewModel));
        ctx.Services.DidNotReceive().GetService(typeof(SetupWizardViewModel));
    }

    // === Tray window-visibility commands ===

    [Fact]
    public void Should_SetIsWindowVisibleTrue_When_ShowWindowCommandFires()
    {
        var ctx = Create(); var vm = ctx.Vm;
        vm.IsWindowVisible = false; // start hidden

        vm.ShowWindowCommand.Execute(null);

        Assert.True(vm.IsWindowVisible);
    }

    [Fact]
    public void Should_SetIsWindowVisibleFalse_When_HideWindowCommandFires()
    {
        var ctx = Create(); var vm = ctx.Vm;

        vm.HideWindowCommand.Execute(null);

        Assert.False(vm.IsWindowVisible);
    }

    [Fact]
    public void Should_FlipIsWindowVisible_When_ToggleWindowCommandFires()
    {
        var ctx = Create(); var vm = ctx.Vm;
        var initial = vm.IsWindowVisible;

        vm.ToggleWindowCommand.Execute(null);

        Assert.Equal(!initial, vm.IsWindowVisible);
    }

    // === Tray tooltip reflects server status ===

    [Fact]
    public void Should_RenderBothCirclesAsClosed_When_NoServersRunning()
    {
        var ctx = Create(serverStatus: new ServerStatus
        {
            IsWebRunning = false, IsApiRunning = false, WebPort = 5000, ApiPort = 5001,
        });

        // ○ = closed circle, ● = filled circle
        Assert.Contains("Web: ○", ctx.Vm.TrayTooltip);
        Assert.Contains("API: ○", ctx.Vm.TrayTooltip);
    }

    [Fact]
    public void Should_RenderFilledCircles_When_ServersAreRunning()
    {
        var ctx = Create(serverStatus: new ServerStatus
        {
            IsWebRunning = true, IsApiRunning = true, WebPort = 5000, ApiPort = 5001,
        });

        Assert.Contains("Web: ●", ctx.Vm.TrayTooltip);
        Assert.Contains("API: ●", ctx.Vm.TrayTooltip);
    }

    // === Tray server-management commands delegate to IServerManagementService ===

    [Fact]
    public async Task Should_StartWebServerAndUpdateTooltip_When_StartWebFromTrayCommandFires()
    {
        var ctx = Create();

        await ctx.Vm.StartWebServerFromTrayCommand.ExecuteAsync(null);

        await ctx.ServerMgmt.Received(1).StartWebServerAsync(5000);
        // Constructor + the post-start refresh = 2 GetServerStatus calls.
        ctx.ServerMgmt.Received(2).GetServerStatus();
    }

    [Fact]
    public async Task Should_StopWebServerAndUpdateTooltip_When_StopWebFromTrayCommandFires()
    {
        var ctx = Create();

        await ctx.Vm.StopWebServerFromTrayCommand.ExecuteAsync(null);

        await ctx.ServerMgmt.Received(1).StopWebServerAsync();
    }

    [Fact]
    public async Task Should_StartApiServerOnConfiguredPort_When_StartApiFromTrayFires()
    {
        var ctx = Create();

        await ctx.Vm.StartApiServerFromTrayCommand.ExecuteAsync(null);

        await ctx.ServerMgmt.Received(1).StartApiServerAsync(5001);
    }

    // === OpenWebBrowser only fires when web server is running ===

    [Fact]
    public void Should_OpenLocalhostUrl_When_OpenWebBrowserFiresAndWebRunning()
    {
        var browser = Substitute.For<IBrowserService>();
        var ctx = Create(
            serverStatus: new ServerStatus { IsWebRunning = true, WebPort = 5000, IsApiRunning = false, ApiPort = 5001 },
            browser: browser);

        ctx.Vm.OpenWebBrowserCommand.Execute(null);

        browser.Received(1).OpenUrl("http://localhost:5000");
    }

    [Fact]
    public void Should_DoNothing_When_OpenWebBrowserFiresButWebNotRunning()
    {
        var browser = Substitute.For<IBrowserService>();
        var ctx = Create(
            serverStatus: new ServerStatus { IsWebRunning = false, WebPort = 5000, IsApiRunning = false, ApiPort = 5001 },
            browser: browser);

        ctx.Vm.OpenWebBrowserCommand.Execute(null);

        browser.DidNotReceive().OpenUrl(Arg.Any<string>());
    }

    // === NavigateTo lazy-resolves INavigationService and delegates ===

    [Fact]
    public async Task Should_DelegateToNavigationService_When_NavigateToCommandFires()
    {
        var nav = Substitute.For<INavigationService>();
        var ctx = Create(navigation: nav);

        await ctx.Vm.NavigateToCommand.ExecuteAsync("Inventory");

        await nav.Received(1).NavigateAsync("Inventory");
    }

    // === Page lifecycle: switching pages disposes previous IDisposable page ===

    [Fact]
    public void Should_DisposePreviousPage_When_CurrentPageIsReplacedWithDisposable()
    {
        // Replace the constructor-resolved CurrentPage (a real ScanViewModel) with our
        // disposable stub, then replace the stub. The stub should get disposed.
        var ctx = Create();
        var firstStub = new StubViewModel();
        ctx.Vm.CurrentPage = firstStub;
        var secondStub = new StubViewModel();

        ctx.Vm.CurrentPage = secondStub;

        Assert.True(firstStub.WasDisposed);
        Assert.False(secondStub.WasDisposed);
    }

    [Fact]
    public void Should_DisposeCurrentPage_When_MainWindowVmIsDisposed()
    {
        var ctx = Create();
        var stub = new StubViewModel();
        ctx.Vm.CurrentPage = stub;

        ctx.Vm.Dispose();

        Assert.True(stub.WasDisposed);
    }
}
