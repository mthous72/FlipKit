using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Desktop.ViewModels;
using NSubstitute;

namespace FlipKit.Desktop.Tests.ViewModels;

public class SetupWizardViewModelTests
{
    private static SetupWizardViewModel Create(
        ISettingsService? settings = null,
        IBrowserService? browser = null) =>
        new(settings ?? Substitute.For<ISettingsService>(),
            browser ?? Substitute.For<IBrowserService>());

    // === Step navigation ===

    [Fact]
    public void Should_StartOnStepOne_When_Constructed()
    {
        var vm = Create();
        Assert.Equal(1, vm.CurrentStep);
        Assert.False(vm.ShowBack);
        Assert.True(vm.ShowNext);
        Assert.False(vm.ShowFinish);
    }

    [Fact]
    public void Should_AdvanceStep_When_NextCommandFires()
    {
        var vm = Create();
        vm.NextCommand.Execute(null);
        Assert.Equal(2, vm.CurrentStep);
    }

    [Fact]
    public void Should_NotAdvancePastStepThree_When_NextCommandFiresOnLastStep()
    {
        var vm = Create();
        vm.NextCommand.Execute(null); // → 2
        vm.NextCommand.Execute(null); // → 3
        vm.NextCommand.Execute(null); // should stay at 3
        Assert.Equal(3, vm.CurrentStep);
        Assert.True(vm.ShowFinish);
        Assert.False(vm.ShowNext);
    }

    [Fact]
    public void Should_GoBackOneStep_When_BackCommandFires()
    {
        var vm = Create();
        vm.NextCommand.Execute(null); // → 2
        vm.BackCommand.Execute(null);
        Assert.Equal(1, vm.CurrentStep);
    }

    [Fact]
    public void Should_NotGoBelowStepOne_When_BackCommandFiresOnFirstStep()
    {
        var vm = Create();
        vm.BackCommand.Execute(null);
        Assert.Equal(1, vm.CurrentStep);
    }

    // === Finish saves settings + invokes callback ===

    [Fact]
    public void Should_SaveSettingsAndInvokeCallback_When_FinishCommandFires()
    {
        var settings = Substitute.For<ISettingsService>();
        var vm = Create(settings: settings);
        vm.OpenRouterApiKey = "sk-or-test";
        vm.ImgBBApiKey = "imgbb-test";
        vm.IsEbaySeller = true;
        vm.DefaultShippingProfile = "1-3 oz";
        var callbackFired = false;
        vm.OnSetupComplete = () => callbackFired = true;

        vm.FinishCommand.Execute(null);

        settings.Received(1).Save(Arg.Is<AppSettings>(s =>
            s.OpenRouterApiKey == "sk-or-test" &&
            s.ImgBBApiKey == "imgbb-test" &&
            s.IsEbaySeller == true &&
            s.DefaultShippingProfile == "1-3 oz"));
        Assert.True(callbackFired);
    }

    // === Connection tests update status flags ===

    [Fact]
    public async Task Should_MarkOpenRouterValid_When_ConnectionTestSucceeds()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.TestOpenRouterConnectionAsync("good-key").Returns(true);
        var vm = Create(settings: settings);
        vm.OpenRouterApiKey = "good-key";

        await vm.TestOpenRouterCommand.ExecuteAsync(null);

        Assert.True(vm.OpenRouterValid);
        Assert.Contains("Connected", vm.OpenRouterStatus);
        Assert.False(vm.IsTestingOpenRouter);
    }

    [Fact]
    public async Task Should_MarkOpenRouterInvalid_When_ConnectionTestFails()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.TestOpenRouterConnectionAsync("bad-key").Returns(false);
        var vm = Create(settings: settings);
        vm.OpenRouterApiKey = "bad-key";

        await vm.TestOpenRouterCommand.ExecuteAsync(null);

        Assert.False(vm.OpenRouterValid);
        Assert.Contains("failed", vm.OpenRouterStatus);
    }

    [Fact]
    public async Task Should_MarkImgBBValid_When_ConnectionTestSucceeds()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.TestImgBBConnectionAsync("good").Returns(true);
        var vm = Create(settings: settings);
        vm.ImgBBApiKey = "good";

        await vm.TestImgBBCommand.ExecuteAsync(null);

        Assert.True(vm.ImgBBValid);
        Assert.Contains("Connected", vm.ImgBBStatus);
    }

    // === Browser-opening commands ===

    [Fact]
    public void Should_OpenOpenRouterSignupPage_When_CommandFires()
    {
        var browser = Substitute.For<IBrowserService>();
        var vm = Create(browser: browser);

        vm.OpenOpenRouterSignupCommand.Execute(null);

        browser.Received(1).OpenUrl(Arg.Is<string>(u => u.Contains("openrouter.ai")));
    }

    [Fact]
    public void Should_OpenImgBBSignupPage_When_CommandFires()
    {
        var browser = Substitute.For<IBrowserService>();
        var vm = Create(browser: browser);

        vm.OpenImgBBSignupCommand.Execute(null);

        browser.Received(1).OpenUrl(Arg.Is<string>(u => u.Contains("imgbb.com")));
    }
}
