using Microsoft.AspNetCore.Mvc;

namespace FlipKit.Web.Controllers
{
    /// <summary>
    /// Settings feature is disabled in the web app.
    /// Use the FlipKit Desktop application to configure API keys and preferences.
    /// </summary>
    public class SettingsController : Controller
    {
        public IActionResult Index()
        {
            TempData["ErrorMessage"] = "Settings are only available in the FlipKit Desktop application. Configure API keys and preferences there.";
            return RedirectToAction("Index", "Scan");
        }
    }
}
