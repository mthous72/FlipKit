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

    private static SettingsController Create(ISettingsService? settings = null)
    {
        var controller = new SettingsController(
            settings ?? Substitute.For<ISettingsService>(),
            NullLogger<SettingsController>.Instance);
        TempDataHelper.Attach(controller);
        return controller;
    }

    [Fact]
    public void Should_RedirectToScan_When_NotInDockerEnvironment()
    {
        Environment.SetEnvironmentVariable("FLIPKIT_DB_PATH", null);
        var controller = Create();

        var result = controller.Index();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Scan", redirect.ControllerName);
        Assert.True(controller.TempData.ContainsKey("InfoMessage"));
    }

    [Fact]
    public void Should_ReturnSettingsView_When_InDockerEnvironment()
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

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SettingsViewModel>(view.Model);
        Assert.True(model.IsDockerEnvironment);
        // API keys masked: shown as ••••••••<last4>.
        Assert.StartsWith("••••••••", model.OpenRouterApiKey);
        Assert.True(model.HasOpenRouterKey);
    }

    [Fact]
    public void Should_NotShowKey_When_ApiKeyIsEmpty()
    {
        Environment.SetEnvironmentVariable("FLIPKIT_DB_PATH", "/data/cards.db");
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { OpenRouterApiKey = "", ImgBBApiKey = "" });
        var controller = Create(settings: settings);

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SettingsViewModel>(view.Model);
        Assert.Equal("", model.OpenRouterApiKey);
        Assert.False(model.HasOpenRouterKey);
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
