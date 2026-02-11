using Microsoft.AspNetCore.Mvc;

namespace FlipKit.Web.Controllers
{
    /// <summary>
    /// Reports feature is disabled in the web app.
    /// Use the FlipKit Desktop application for full reporting capabilities.
    /// </summary>
    public class ReportsController : Controller
    {
        public IActionResult Index()
        {
            TempData["ErrorMessage"] = "Reports are only available in the FlipKit Desktop application. The web app provides card scanning and pricing research only.";
            return RedirectToAction("Index", "Scan");
        }
    }
}
