using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using BranchPOS.Models;
using BranchPOS.Services;

namespace BranchPOS.Controllers;

public class HomeController : Controller
{
    private readonly IAdminDashboardService _adminDashboardService;

    public HomeController(IAdminDashboardService adminDashboardService)
    {
        _adminDashboardService = adminDashboardService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
        {
            return View(await _adminDashboardService.GetDashboardAsync(cancellationToken));
        }

        return View();
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
