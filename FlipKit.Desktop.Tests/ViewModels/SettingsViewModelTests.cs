using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Scanning;
using FlipKit.Desktop.Services;
using FlipKit.Desktop.ViewModels;
using NSubstitute;

namespace FlipKit.Desktop.Tests.ViewModels;

public class SettingsViewModelTests
{
    private static SettingsViewModel Create(
        ISettingsService? settings = null,
        IBrowserService? browser = null,
        IServiceProvider? services = null,
        IServerManagementService? serverMgmt = null,
        ServerStatus? initialStatus = null,
        INetworkAddressProvider? networkAddresses = null)
    {
        if (settings == null)
        {
            // Only set up the default Load() return when caller didn't supply their own
            // settings — otherwise we'd clobber whatever they configured.
            settings = Substitute.For<ISettingsService>();
            settings.Load().Returns(new AppSettings
            {
                DefaultModel = ModelOption.AutoValue,
                CustomGradingCompanies = new List<string>(),
            });
        }

        if (serverMgmt == null)
        {
            // Same pattern as settings — only configure when caller didn't supply.
            serverMgmt = Substitute.For<IServerManagementService>();
            serverMgmt.GetServerStatus().Returns(initialStatus ?? new ServerStatus
            {
                IsWebRunning = false, IsApiRunning = false, WebPort = 5000, ApiPort = 5001,
            });
            serverMgmt.GetWebServerLogs().Returns(Array.Empty<string>());
            serverMgmt.GetApiServerLogs().Returns(Array.Empty<string>());
        }

        services ??= Substitute.For<IServiceProvider>();
        // Don't register IOpenRouterModelCatalog — VM tolerates it being absent.

        // Phase 5c — INetworkAddressProvider injected. Default mock returns an empty
        // snapshot so VM construction's UpdateLocalIpAddresses doesn't blow up.
        networkAddresses ??= Substitute.For<INetworkAddressProvider>().Tap(p =>
            p.GetCurrent(Arg.Any<int>(), Arg.Any<bool>()).Returns(new NetworkAddressInfo(
                LocalNetworkIp: null, TailscaleIp: null,
                IsLocalNetworkAvailable: false, IsTailscaleAvailable: false,
                LocalNetworkStatus: "No network", TailscaleStatus: "Not configured",
                LocalNetworkUrl: string.Empty, TailscaleUrl: string.Empty,
                LocalQrCodeBitmap: null, TailscaleQrCodeBitmap: null,
                LegacyLocalIpAddresses: "No network connection", LegacyQrCodeBitmap: null)));

        return new SettingsViewModel(
            settings, browser ?? Substitute.For<IBrowserService>(), services, serverMgmt, networkAddresses);
    }

    // === LoadSettings populates fields from ISettingsService ===

    [Fact]
    public void Should_LoadAllFieldsFromSettings_When_Constructed()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings
        {
            OpenRouterApiKey = "or-key",
            ImgBBApiKey = "imgbb-key",
            XimilarApiKey = "xim-key",
            WhatnotFeePercent = 13m,
            EbayFeePercent = 14m,
            DefaultModel = ModelOption.AutoValue,
            ActiveExportPlatform = ExportPlatform.eBay,
            CustomGradingCompanies = new List<string>(),
        });
        using var vm = Create(settings: settings);

        Assert.Equal("or-key", vm.OpenRouterApiKey);
        Assert.Equal("imgbb-key", vm.ImgBBApiKey);
        Assert.Equal("xim-key", vm.XimilarApiKey);
        Assert.Equal(13m, vm.WhatnotFeePercent);
        Assert.Equal(ExportPlatform.eBay, vm.ActiveExportPlatform);
    }

    [Fact]
    public void Should_MarkApiKeysConfiguredOrNot_When_LoadingSettings()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings
        {
            OpenRouterApiKey = "set",
            ImgBBApiKey = "",
            XimilarApiKey = null,
            DefaultModel = ModelOption.AutoValue,
            CustomGradingCompanies = new List<string>(),
        });
        using var vm = Create(settings: settings);

        Assert.Contains("Configured", vm.OpenRouterStatus);
        Assert.Contains("Not configured", vm.ImgBBStatus);
        Assert.Contains("Not configured", vm.XimilarStatus);
    }

    // === SaveSettings persists current state ===

    [Fact]
    public void Should_PersistAllCurrentFields_When_SaveSettingsFires()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings
        {
            DefaultModel = ModelOption.AutoValue,
            CustomGradingCompanies = new List<string>(),
            // Need valid templates for save to not block on template validation.
            WhatnotTitleTemplate = "{Year} {Brand} {Player}",
            EbayTitleTemplate = "{Year} {Brand} {Player}",
            ComcTitleTemplate = "{Year} {Brand} {Player}",
            GenericTitleTemplate = "{Year} {Brand} {Player}",
            TerapeakSearchTemplate = "{Year} {Player}",
            EbaySearchTemplate = "{Year} {Player}",
        });
        using var vm = Create(settings: settings);
        vm.OpenRouterApiKey = "new-key";
        vm.WhatnotFeePercent = 12m;

        vm.SaveSettingsCommand.Execute(null);

        settings.Received(1).Save(Arg.Is<AppSettings>(s =>
            s.OpenRouterApiKey == "new-key" && s.WhatnotFeePercent == 12m));
        Assert.Contains("saved", vm.SaveMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Should_BlockSave_When_TitleTemplateIsInvalid()
    {
        using var vm = Create();
        vm.WhatnotTitleTemplate = "{NotAField}"; // unknown placeholder

        vm.SaveSettingsCommand.Execute(null);

        Assert.Contains("template error", vm.SaveMessage);
    }

    // === Connection tests ===

    [Fact]
    public async Task Should_MarkOpenRouterConnected_When_TestSucceeds()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { DefaultModel = ModelOption.AutoValue, CustomGradingCompanies = new() });
        settings.TestOpenRouterConnectionAsync(Arg.Any<string>()).Returns(true);
        using var vm = Create(settings: settings);
        vm.OpenRouterApiKey = "k";

        await vm.TestOpenRouterCommand.ExecuteAsync(null);

        Assert.Contains("Connected", vm.OpenRouterStatus);
        Assert.False(vm.IsTestingOpenRouter);
    }

    [Fact]
    public async Task Should_MarkXimilarFailed_When_TestFails()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { DefaultModel = ModelOption.AutoValue, CustomGradingCompanies = new() });
        settings.TestXimilarConnectionAsync(Arg.Any<string>()).Returns(false);
        using var vm = Create(settings: settings);
        vm.XimilarApiKey = "bad";

        await vm.TestXimilarCommand.ExecuteAsync(null);

        Assert.Contains("failed", vm.XimilarStatus);
    }

    // === Template reset / validation commands ===

    [Fact]
    public void Should_RestoreDefaultTemplates_When_ResetTitleTemplatesFires()
    {
        using var vm = Create();
        vm.WhatnotTitleTemplate = "garbage";

        vm.ResetTitleTemplatesCommand.Execute(null);

        // Default Whatnot template includes {Year} and {Player}.
        Assert.Contains("{Year}", vm.WhatnotTitleTemplate);
        Assert.Contains("{Player}", vm.WhatnotTitleTemplate);
        Assert.Contains("reset", vm.TemplateValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Should_ReportValidWhenTemplateOK_When_ValidateCurrentTemplateFires()
    {
        using var vm = Create();
        vm.ActiveExportPlatform = ExportPlatform.Whatnot;
        vm.WhatnotTitleTemplate = "{Year} {Brand} {Player}";

        vm.ValidateCurrentTemplateCommand.Execute(null);

        Assert.Contains("valid", vm.TemplateValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Should_ReportInvalidPlaceholder_When_ValidateCurrentTemplateFires()
    {
        using var vm = Create();
        vm.ActiveExportPlatform = ExportPlatform.Whatnot;
        vm.WhatnotTitleTemplate = "{Bogus}";

        vm.ValidateCurrentTemplateCommand.Execute(null);

        Assert.Contains("Bogus", vm.TemplateValidationMessage);
    }

    [Fact]
    public void Should_RestoreDefaultSearchTemplates_When_ResetSearchTemplatesFires()
    {
        using var vm = Create();
        vm.TerapeakSearchTemplate = "garbage";

        vm.ResetSearchTemplatesCommand.Execute(null);

        Assert.Contains("{Year}", vm.TerapeakSearchTemplate);
    }

    // === Server management commands ===

    [Fact]
    public async Task Should_DelegateToServerService_When_StartWebFires()
    {
        // NB: not asserting the post-call WebServerStatus message — there's a known
        // production issue where UpdateServerStatus runs after Start and overwrites
        // success/failure messages with "Stopped" if GetServerStatus's IsWebRunning
        // is still false. (Production race; flagged for Phase 5 ViewModel split.)
        // Verify the delegation contract instead.
        var serverMgmt = Substitute.For<IServerManagementService>();
        serverMgmt.GetServerStatus().Returns(new ServerStatus
        {
            IsWebRunning = false, IsApiRunning = false, WebPort = 5000, ApiPort = 5001,
        });
        serverMgmt.StartWebServerAsync(Arg.Any<int>()).Returns(new ServerStartResult
        {
            Success = true, ActualPort = 5000,
        });
        serverMgmt.GetWebServerLogs().Returns(Array.Empty<string>());
        serverMgmt.GetApiServerLogs().Returns(Array.Empty<string>());
        using var vm = Create(serverMgmt: serverMgmt);

        await vm.StartWebServerCommand.ExecuteAsync(null);

        await serverMgmt.Received().StartWebServerAsync(vm.WebServerPort);
        Assert.Equal(5000, vm.ActualWebPort); // ActualWebPort isn't clobbered by UpdateServerStatus
    }

    [Fact]
    public async Task Should_StopWebServerAndUpdateStatus_When_StopWebFires()
    {
        var serverMgmt = Substitute.For<IServerManagementService>();
        serverMgmt.GetServerStatus().Returns(new ServerStatus { WebPort = 5000, ApiPort = 5001 });
        serverMgmt.GetWebServerLogs().Returns(Array.Empty<string>());
        serverMgmt.GetApiServerLogs().Returns(Array.Empty<string>());
        using var vm = Create(serverMgmt: serverMgmt);

        await vm.StopWebServerCommand.ExecuteAsync(null);

        await serverMgmt.Received().StopWebServerAsync();
        Assert.Equal("Stopped", vm.WebServerStatus);
    }

    [Fact]
    public void Should_OpenWebBrowserOnLocalhost_When_OpenBrowserFires()
    {
        var browser = Substitute.For<IBrowserService>();
        using var vm = Create(browser: browser);
        vm.WebServerPort = 7777;

        vm.OpenWebBrowserCommand.Execute(null);

        browser.Received(1).OpenUrl(Arg.Is<string>(u => u.Contains(":7777")));
    }

    [Fact]
    public void Should_RefreshLogs_When_RefreshServerLogsFires()
    {
        // Mock returns DIFFERENT logs after construction to verify the explicit refresh
        // (not just the constructor's implicit one).
        var serverMgmt = Substitute.For<IServerManagementService>();
        serverMgmt.GetServerStatus().Returns(new ServerStatus
        {
            IsWebRunning = true, IsApiRunning = true, WebPort = 5000, ApiPort = 5001,
        });
        serverMgmt.GetWebServerLogs().Returns(new[] { "line1", "line2" });
        serverMgmt.GetApiServerLogs().Returns(new[] { "apiline" });
        using var vm = Create(serverMgmt: serverMgmt);

        vm.RefreshServerLogsCommand.Execute(null);

        Assert.Contains("line1", vm.WebServerLogs);
        Assert.Contains("apiline", vm.ApiServerLogs);
    }

    [Fact]
    public void Should_ClearLogsViaService_When_ClearWebLogsFires()
    {
        var serverMgmt = Substitute.For<IServerManagementService>();
        serverMgmt.GetServerStatus().Returns(new ServerStatus { WebPort = 5000, ApiPort = 5001 });
        serverMgmt.GetWebServerLogs().Returns(Array.Empty<string>());
        serverMgmt.GetApiServerLogs().Returns(Array.Empty<string>());
        using var vm = Create(serverMgmt: serverMgmt);

        vm.ClearWebLogsCommand.Execute(null);

        serverMgmt.Received(1).ClearWebServerLogs();
        Assert.Equal(string.Empty, vm.WebServerLogs);
    }

    // === Dispose stops the timer ===

    [Fact]
    public void Should_NotThrow_When_Disposed()
    {
        var vm = Create();
        vm.Dispose();
    }

    // === Default model selection partial ===

    [Fact]
    public void Should_SyncDefaultModelString_When_SelectedDefaultModelChanged()
    {
        using var vm = Create();
        var option = ModelOption.FromCatalog(
            new OpenRouterModel("test/model:free", "Test", IsFree: true, 0m, 0m, null, ""));

        vm.SelectedDefaultModel = option;

        Assert.Equal("test/model:free", vm.DefaultModel);
    }
}
