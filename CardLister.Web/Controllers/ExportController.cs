using Microsoft.AspNetCore.Mvc;

namespace FlipKit.Web.Controllers
{
    /// <summary>
    /// Export feature is disabled in the web app.
    /// Use the FlipKit Desktop application to export cards to CSV.
    /// </summary>
    public class ExportController : Controller
    {
        public IActionResult Index()
        {
            TempData["ErrorMessage"] = "CSV export is only available in the FlipKit Desktop application.";
            return RedirectToAction("Index", "Scan");
        }
    }
}
