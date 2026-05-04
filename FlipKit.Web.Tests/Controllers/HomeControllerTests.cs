using FlipKit.Core.Services;
using FlipKit.Web.Controllers;
using FlipKit.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlipKit.Web.Tests.Controllers;

public class HomeControllerTests
{
    private static HomeController Create() =>
        new(NullLogger<HomeController>.Instance, Substitute.For<ICardRepository>());

    [Fact]
    public void Should_RedirectToScanIndex_When_HomeIndexCalled()
    {
        var controller = Create();

        var result = controller.Index();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Scan", redirect.ControllerName);
    }

    [Fact]
    public void Should_ReturnPrivacyView_When_PrivacyCalled()
    {
        var controller = Create();

        var result = controller.Privacy();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Should_ReturnErrorViewWithRequestId_When_ErrorCalled()
    {
        var controller = Create();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = controller.Error();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(view.Model);
        Assert.NotNull(model.RequestId); // either Activity.Current?.Id or HttpContext.TraceIdentifier
    }
}
