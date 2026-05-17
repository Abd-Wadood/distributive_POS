using BranchPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BranchPOS.Controllers;

[Authorize(Roles = "Admin,StockManager")]
[EnableRateLimiting("ReportsPolicy")]
public class ReportsController : Controller
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    public async Task<IActionResult> Index(int page = 1, int? pageSize = null, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var model = await _reportService.BuildReportAsync(page, pageSize, from, to, cancellationToken);
        return View(model);
    }
}
