using FlipKit.Web.Controllers;
using FlipKit.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FlipKit.Web.Tests.Controllers;

/// <summary>
/// Both ExportController and ReportsController are deliberately disabled in the web
/// app — Index sets a TempData error and redirects to Scan. Trivial pair.
/// </summary>
public class ExportAndReportsControllersTests
{
    [Fact]
    public void Should_RedirectToScanWithError_When_ExportIndexCalled()
    {
        var controller = new ExportController();
        TempDataHelper.Attach(controller);

        var result = controller.Index();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Scan", redirect.ControllerName);
        Assert.True(controller.TempData.ContainsKey("ErrorMessage"));
        Assert.Contains("Desktop", controller.TempData["ErrorMessage"]!.ToString());
    }

    [Fact]
    public void Should_RedirectToScanWithError_When_ReportsIndexCalled()
    {
        var controller = new ReportsController();
        TempDataHelper.Attach(controller);

        var result = controller.Index();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Scan", redirect.ControllerName);
        Assert.True(controller.TempData.ContainsKey("ErrorMessage"));
        Assert.Contains("Desktop", controller.TempData["ErrorMessage"]!.ToString());
    }
}
