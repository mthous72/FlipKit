using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FlipKit.Web.Models;
using FlipKit.Core.Services;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ICardRepository _cardRepository;

    public HomeController(ILogger<HomeController> logger, ICardRepository cardRepository)
    {
        _logger = logger;
        _cardRepository = cardRepository;
    }

    public IActionResult Index()
    {
        // Web app only provides Scan and Pricing features
        // Redirect to Scan page by default
        return RedirectToAction("Index", "Scan");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
