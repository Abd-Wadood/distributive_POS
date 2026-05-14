using System.Security.Claims;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using BranchPOS.Services;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BranchPOS.Data;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[Authorize(Roles = "Cashier")]
public class OrdersController : Controller
{
    private readonly AppDbContext _context;
    private readonly IOrderService _orderService;
    private readonly IProductAvailabilityService _productAvailabilityService;
    private readonly IUserSessionService _userSessionService;
    private readonly ITerminalContextService _terminalContextService;
    private readonly IErrorLoggingService _errorLoggingService;

    public OrdersController(
        AppDbContext context,
        IOrderService orderService,
        IProductAvailabilityService productAvailabilityService,
        IUserSessionService userSessionService,
        ITerminalContextService terminalContextService,
        IErrorLoggingService errorLoggingService)
    {
        _context = context;
        _orderService = orderService;
        _productAvailabilityService = productAvailabilityService;
        _userSessionService = userSessionService;
        _terminalContextService = terminalContextService;
        _errorLoggingService = errorLoggingService;
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index()
    {
        return View(await _orderService.GetOrdersAsync());
    }

    public async Task<IActionResult> Create()
    {
        var cashierId = GetCashierId();
        await _terminalContextService.RequireCurrentTerminalAsync();
        var activeSession = await _userSessionService.GetActiveSessionAsync(cashierId);
        if (activeSession is null)
        {
            return RedirectToAction("Index", "Sessions");
        }

        var products = await _productAvailabilityService.GetPosProductsAsync();
        var drafts = await _orderService.ResumeDraftOrdersAsync(activeSession.Id);
        var categories = await _context.Categories.OrderBy(x => x.Name).ToListAsync();

        return View(new PosOrderViewModel
        {
            Products = products,
            Categories = categories,
            DraftOrders = drafts.Select(x => new PosDraftOrderViewModel
            {
                Id = x.Id,
                OrderNumber = x.OrderNumber,
                OrderType = x.OrderType.ToString(),
                DiscountAmount = x.DiscountAmount,
                TableNumber = x.TableNumber,
                Notes = x.Notes,
                CustomerName = x.Customer?.Name,
                CustomerPhone = x.Customer?.PhoneNumber,
                CustomerAddress = x.Customer?.Address,
                Items = x.Items.Select(i => new PosDraftItemViewModel
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity
                }).ToList()
            }).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDraft([FromBody] DraftOrderDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Json(new { success = false, message = GetModelStateMessage("Please correct the held order details and try again.") });
            }

            dto.CashierId = GetCashierId();
            var terminal = await _terminalContextService.RequireCurrentTerminalAsync();
            var session = await _userSessionService.GetActiveSessionAsync(dto.CashierId);
            if (session is null)
            {
                throw new InvalidOperationException("Start or continue a session before holding orders.");
            }
            dto.BranchId = session.BranchId;
            dto.UserSessionId = session.Id;
            dto.TerminalName = session.TerminalName;
            dto.TerminalId = terminal.Id;
            dto.TerminalCode = terminal.TerminalCode;
            var result = dto.DraftOrderId.HasValue
                ? await _orderService.UpdateDraftOrderAsync(dto)
                : await _orderService.CreateDraftOrderAsync(dto);

            return Json(new { success = true, draft = result });
        }
        catch (InvalidOperationException ex)
        {
            var message = ToUserMessage(ex);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = false, message });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Finalize([FromBody] CreateOrderDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Json(new { success = false, message = GetModelStateMessage("Please correct the order details and try again.") });
            }

            dto.CashierId = GetCashierId();
            var terminal = await _terminalContextService.RequireCurrentTerminalAsync();
            var session = await _userSessionService.GetActiveSessionAsync(dto.CashierId);
            if (session is null)
            {
                throw new InvalidOperationException("Start or continue a session before finalizing orders.");
            }
            dto.BranchId = session.BranchId;
            dto.UserSessionId = session.Id;
            dto.TerminalName = session.TerminalName;
            dto.TerminalId = terminal.Id;
            dto.TerminalCode = terminal.TerminalCode;
            var result = await _orderService.FinalizeOrderAsync(dto);
            return Json(new { success = true, receiptUrl = Url.Action(nameof(Receipt), new { id = result.OrderId }), order = result });
        }
        catch (InvalidOperationException ex)
        {
            var message = ToUserMessage(ex);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = false, message });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelDraft([FromBody] CancelDraftRequest request)
    {
        try
        {
            await _orderService.CancelDraftOrderAsync(request.OrderId, GetCashierId());
            return Json(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            var message = ToUserMessage(ex);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = false, message });
        }
    }

    public async Task<IActionResult> Receipt(int id)
    {
        var order = await _orderService.GetReceiptAsync(id);
        return order is null ? NotFound() : View(order);
    }

    private string GetCashierId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("Authenticated user was not found.");

    private string ToUserMessage(InvalidOperationException ex)
    {
        var message = ex is BranchPosException branchPosException ? branchPosException.UserMessage : ex.Message;
        _errorLoggingService.LogException(HttpContext, ex, message);
        return message;
    }

    private string GetModelStateMessage(string fallback)
    {
        var firstError = ModelState.Values
            .SelectMany(x => x.Errors)
            .Select(x => x.ErrorMessage)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        return string.IsNullOrWhiteSpace(firstError) ? fallback : firstError;
    }
}

public class CancelDraftRequest
{
    public int OrderId { get; set; }
}
