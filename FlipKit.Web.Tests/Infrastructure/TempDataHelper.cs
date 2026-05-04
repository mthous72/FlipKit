using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;

namespace FlipKit.Web.Tests.Infrastructure;

/// <summary>
/// Controller tests need a real <see cref="ITempDataDictionary"/> assigned to
/// <c>controller.TempData</c> before invoking actions that write to it — otherwise
/// MVC throws a NullReferenceException on the first TempData access. NSubstitute
/// can fake ITempDataDictionary but it'd need every indexer mocked individually;
/// using the real implementation backed by an in-memory provider is simpler.
/// </summary>
public static class TempDataHelper
{
    public static void Attach(Controller controller)
    {
        controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            Substitute.For<ITempDataProvider>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }
}
