using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Web.Controllers;
using FlipKit.Web.Models;
using FlipKit.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Web.Tests.Controllers;

public class SettingsControllerTests : IDisposable
{
    // SettingsController branches on FLIPKIT_DB_PATH env var — `/data/...` = Docker
    // mode, anything else = Desktop mode. We set the env var per-test and reset on
    // dispose so concurrent test runs (xUnit parallelizes by class) stay isolated.
    private readonly string? _originalDbPath;

    public SettingsControllerTests()
    {
        _originalDbPath = Environment.GetEnvironmentVariable("FLIPKIT_DB_PATH");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("FLIPKIT_DB_PATH", _originalDbPath);
    }

    private static SettingsController Create(
        ISettingsService? settings = null,
        IOpenRouterModelCatalog? catalog = null,
        IOpenRouterKeyInfoService? keyInfoService = null,
        ICardsightSubscriptionService? cardsightSubscriptionService = null)
    {
        var defaultCatalog = catalog ?? Substitute.For<IOpenRouterModelCatalog>();
        defaultCatalog.GetAsync(default).ReturnsForAnyArgs(
            new ModelCatalog(Array.Empty<OpenRouterModel>(), Array.Empty<OpenRouterModel>(), DateTime.UtcNow));
        var controller = new SettingsController(
            settings ?? Substitute.For<ISettingsService>(),
            defaultCatalog,
            keyInfoService ?? Substitute.For<IOpenRouterKeyInfoService>(),
            cardsightSubscriptionService ?? Substitute.For<ICardsightSubscriptionService>(),
            NullLogger<SettingsController>.Instance);
        TempDataHelper.Attach(controller);
        return controller;
    }

    [Fact]
    public async Task Should_RedirectToScan_When_NotInDockerEnvironment()
    {
        Environment.SetEnvironmentVariable("FLIPKIT_DB_PATH", null);
        var controller = Create();

        var result = await controller.Index();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Scan", redirect.ControllerName);
        Assert.True(controller.TempData.ContainsKey("InfoMessage"));
    }

    [Fact]
    public async Task Should_ReturnSettingsView_When_InDockerEnvironment()
    {
        Environment.SetEnvironmentVariable("FLIPKIT_DB_PATH", "/data/cards.db");
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings
        {
            OpenRouterApiKey = "sk-or-real-key",
            ImgBBApiKey = "imgbb-key",
            WhatnotFeePercent = 11m,
        });
        var controller = Create(settings: settings);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SettingsViewModel>(view.Model);
        Assert.True(model.IsDockerEnvironment);
        // API keys masked: shown as ••••••••<last4>.
        Assert.StartsWith("••••••••", model.OpenRouterApiKey);
        Assert.True(model.HasOpenRouterKey);
    }

    [Fact]
    public async Task Should_NotShowKey_When_ApiKeyIsEmpty()
    {
        Environment.SetEnvironmentVariable("FLIPKIT_DB_PATH", "/data/cards.db");
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { OpenRouterApiKey = "", ImgBBApiKey = "" });
        var controller = Create(settings: settings);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SettingsViewModel>(view.Model);
        Assert.Equal("", model.OpenRouterApiKey);
        Assert.False(model.HasOpenRouterKey);
    }

    // === OpenRouter Usage card (Phase 4 — usage panel) ===

    [Fact]
    public async Task Should_PopulateOpenRouterUsage_When_KeyConfiguredAndServiceSucceeds()
    {
        Environment.SetEnvironmentVariable("FLIPKIT_DB_PATH", "/data/cards.db");
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { OpenRouterApiKey = "sk-or-real-key" });
        var keyService = Substitute.For<IOpenRouterKeyInfoService>();
        var info = new OpenRouterKeyInfo(
            Label: "test-key", Limit: 25m, LimitRemaining: 17.42m, LimitReset: null,
            Usage: 12.58m, UsageDaily: 1.25m, UsageWeekly: 4.10m, UsageMonthly: 7.58m,
            IsFreeTier: false, FetchedAt: DateTimeOffset.UtcNow);
        keyService.GetAsync(default).ReturnsForAnyArgs(info);

        var controller = Create(settings: settings, keyInfoService: keyService);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SettingsViewModel>(view.Model);
        Assert.NotNull(model.OpenRouterUsage);
        Assert.Equal(17.42m, model.OpenRouterUsage!.LimitRemaining);
        Assert.Equal(1.25m, model.OpenRouterUsage.UsageDaily);
        Assert.Null(model.OpenRouterUsageError);
    }

    [Fact]
    public async Task Should_PopulateUsageError_When_KeyServiceThrowsPaymentRequired()
    {
        // 402 = negative balance. Page should still render; the card shows the
        // inline error message instead of stat tiles.
        Environment.SetEnvironmentVariable("FLIPKIT_DB_PATH", "/data/cards.db");
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { OpenRouterApiKey = "sk-or-real-key" });
        var keyService = Substitute.For<IOpenRouterKeyInfoService>();
        keyService.GetAsync(default).ReturnsForAnyArgs<OpenRouterKeyInfo>(_ =>
            throw new OpenRouterPaymentRequiredException("openrouter/key", "balance is -$0.42"));

        var controller = Create(settings: settings, keyInfoService: keyService);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SettingsViewModel>(view.Model);
        Assert.Null(model.OpenRouterUsage);
        Assert.NotNull(model.OpenRouterUsageError);
        Assert.Contains("Payment", model.OpenRouterUsageError!);
    }

    [Fact]
    public async Task Should_SkipKeyInfoFetch_When_NoApiKeyConfigured()
    {
        // No key = nothing to fetch. The card hides itself in the view, but
        // the controller must not crash or call the service.
        Environment.SetEnvironmentVariable("FLIPKIT_DB_PATH", "/data/cards.db");
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { OpenRouterApiKey = "" });
        var keyService = Substitute.For<IOpenRouterKeyInfoService>();

        var controller = Create(settings: settings, keyInfoService: keyService);

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
        await keyService.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Should_RedirectToIndex_When_RefreshUsageCalled()
    {
        // RefreshUsage POST endpoint is just a re-render trigger — no business
        // logic to test beyond the redirect.
        var controller = Create();

        var result = controller.RefreshUsage();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
    }

    // === Save ===

    [Fact]
    public async Task Should_RedirectToScan_When_SaveCalledOutsideDocker()
    {
        Environment.SetEnvironmentVariable("FLIPKIT_DB_PATH", null);
        var controller = Create();

        var result = await controller.Save(new SettingsViewModel());

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Scan", redirect.ControllerName);
    }

    [Fact]
    public async Task Should_PersistNewApiKeyOverwritingOldValue_When_SaveCalledWithRealKey()
    {
        Environment.SetEnvironmentVariable("FLIPKIT_DB_PATH", "/data/cards.db");
        var existing = new AppSettings { OpenRouterApiKey = "old-key" };
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(existing);
        var controller = Create(settings: settings);

        var result = await controller.Save(new SettingsViewModel
        {
            OpenRouterApiKey = "sk-new-actual-key",
            WhatnotFeePercent = 12m,
        });

        Assert.IsType<RedirectToActionResult>(result);
        settings.Received(1).Save(Arg.Is<AppSettings>(s =>
            s.OpenRouterApiKey == "sk-new-actual-key" && s.WhatnotFeePercent == 12m));
    }

    [Fact]
    public async Task Should_NotOverwriteApiKey_When_MaskedPlaceholderSubmitted()
    {
        // The Index view returns API keys as ••••••••<last4>. If the user doesn't
        // change them, the form posts back the masked value — controller must NOT
        // save that as the new key.
        Environment.SetEnvironmentVariable("FLIPKIT_DB_PATH", "/data/cards.db");
        var existing = new AppSettings { OpenRouterApiKey = "real-original-key" };
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(existing);
        var controller = Create(settings: settings);

        await controller.Save(new SettingsViewModel
        {
            OpenRouterApiKey = "••••••••key1",
        });

        settings.Received(1).Save(Arg.Is<AppSettings>(s =>
            s.OpenRouterApiKey == "real-original-key")); // unchanged
    }

    // === TestConnection ===

    [Fact]
    public async Task Should_ReturnNoKeyMessage_When_TestConnectionCalledWithNoKey()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { OpenRouterApiKey = "" });
        var controller = Create(settings: settings);

        var result = await controller.TestConnection("openrouter");

        var json = Assert.IsType<JsonResult>(result);
        Assert.NotNull(json.Value);
        var success = json.Value!.GetType().GetProperty("success")!.GetValue(json.Value);
        Assert.Equal(false, success);
    }

    [Fact]
    public async Task Should_DelegateToOpenRouterTest_When_ServiceIsOpenRouter()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { OpenRouterApiKey = "k" });
        settings.TestOpenRouterConnectionAsync("k").Returns(true);
        var controller = Create(settings: settings);

        var result = await controller.TestConnection("openrouter");

        var json = Assert.IsType<JsonResult>(result);
        var success = json.Value!.GetType().GetProperty("success")!.GetValue(json.Value);
        Assert.Equal(true, success);
    }

    [Fact]
    public async Task Should_ReturnUnknownService_When_ServiceNameIsBogus()
    {
        var controller = Create();

        var result = await controller.TestConnection("nonsense");

        var json = Assert.IsType<JsonResult>(result);
        var msg = json.Value!.GetType().GetProperty("message")!.GetValue(json.Value)?.ToString();
        Assert.Contains("Unknown", msg);
    }
}
