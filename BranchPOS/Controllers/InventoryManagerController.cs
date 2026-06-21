using System.Security.Claims;
using System.Text.Json;
using BranchPOS.Data;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using BranchPOS.Services;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[Authorize(Roles = "StockManager,Admin")]
[Route("InventoryManager")]
public class InventoryManagerController : Controller
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;
    private readonly IOrderService _orderService;

    public InventoryManagerController(AppDbContext context, IBranchContextService branchContextService, IOrderService orderService)
    {
        _context = context;
        _branchContextService = branchContextService;
        _orderService = orderService;
    }

    [HttpGet("OrderCorrection")]
    public async Task<IActionResult> OrderCorrection(string? orderNumber, string? message, CancellationToken cancellationToken)
    {
        var model = new OrderCorrectionViewModel
        {
            SearchOrderNumber = orderNumber,
            Message = message
        };

        if (!string.IsNullOrWhiteSpace(orderNumber))
        {
            model.Order = await FindOrderAsync(orderNumber.Trim(), cancellationToken);
            if (model.Order is null)
            {
                model.Message = "Order was not found in your branch.";
            }
        }

        return View(model);
    }

    [HttpPost("OrderCorrection/Search")]
    [ValidateAntiForgeryToken]
    public IActionResult Search(string orderNumber) =>
        RedirectToAction(nameof(OrderCorrection), new { orderNumber = orderNumber?.Trim() });

    [HttpPost("OrderCorrection/CancelAndRestoreStock")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelAndRestoreStock(int orderId, string? reason, CancellationToken cancellationToken)
    {
        var order = await GetOrderForCorrectionActionAsync(orderId, cancellationToken);
        if (order is null)
        {
            return RedirectToAction(nameof(OrderCorrection), new { message = "Order number was not found in your branch." });
        }

        if (IsCancelled(order.OrderStatus))
        {
            return RedirectToAction(nameof(OrderCorrection), new { orderNumber = order.OrderNumber, message = $"Order {order.OrderNumber} is already cancelled. You cannot cancel the same order again." });
        }

        try
        {
            await _orderService.CancelAndRestoreOrderAsync(orderId, GetUserId(), reason, cancellationToken);
            return RedirectToAction(nameof(OrderCorrection), new { orderNumber = order.OrderNumber, message = "Order cancelled as not prepared. Stock was restored." });
        }
        catch (PosNotFoundException)
        {
            return RedirectToAction(nameof(OrderCorrection), new { message = "Order number was not found in your branch." });
        }
        catch (BusinessException ex)
        {
            return RedirectToAction(nameof(OrderCorrection), new { orderNumber = order.OrderNumber, message = ex.Message });
        }
    }

    [HttpPost("OrderCorrection/CancelAsWaste")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelAsWaste(int orderId, string? reason, CancellationToken cancellationToken)
    {
        var order = await GetOrderForCorrectionActionAsync(orderId, cancellationToken);
        if (order is null)
        {
            return RedirectToAction(nameof(OrderCorrection), new { message = "Order number was not found in your branch." });
        }

        if (IsCancelled(order.OrderStatus))
        {
            return RedirectToAction(nameof(OrderCorrection), new { orderNumber = order.OrderNumber, message = $"Order {order.OrderNumber} is already cancelled. You cannot cancel the same order again." });
        }

        try
        {
            await _orderService.CancelConsumedAsWasteAsync(orderId, GetUserId(), reason, cancellationToken);
            return RedirectToAction(nameof(OrderCorrection), new { orderNumber = order.OrderNumber, message = "Order cancelled as prepared. Stock was left unchanged." });
        }
        catch (PosNotFoundException)
        {
            return RedirectToAction(nameof(OrderCorrection), new { message = "Order number was not found in your branch." });
        }
        catch (BusinessException ex)
        {
            return RedirectToAction(nameof(OrderCorrection), new { orderNumber = order.OrderNumber, message = ex.Message });
        }
    }

    [HttpPost("OrderCorrection/ReprintKot")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReprintKot(int orderId, string? reason, CancellationToken cancellationToken)
    {
        var orderNumber = await QueueReprintAsync(orderId, PrintJobType.KOTReprint, "Kitchen", reason, cancellationToken);
        return RedirectToAction(nameof(OrderCorrection), new { orderNumber, message = "KOT reprint queued." });
    }

    [HttpPost("OrderCorrection/ReprintBill")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReprintBill(int orderId, string? reason, CancellationToken cancellationToken)
    {
        var orderNumber = await QueueReprintAsync(orderId, PrintJobType.CustomerBillReprint, "Counter", reason, cancellationToken);
        return RedirectToAction(nameof(OrderCorrection), new { orderNumber, message = "Customer bill reprint queued." });
    }

    [HttpPost("OrderCorrection/MarkCodPaid")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkCodPaid(int orderId, CancellationToken cancellationToken)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        var order = await _context.Orders.FirstOrDefaultAsync(x => x.Id == orderId && x.BranchId == branchId, cancellationToken)
            ?? throw new PosNotFoundException("Order was not found.");

        if (order.PaymentStatus != PaymentStatus.Paid)
        {
            if (order.PaymentStatus != PaymentStatus.CODPending)
            {
                throw new BusinessException("Only COD pending orders can be marked paid here.");
            }

            order.PaymentStatus = PaymentStatus.Paid;
            order.PaymentMethod = "COD";
            order.PaymentReceivedAt = DateTime.UtcNow;
            order.PaymentReceivedByUserId = GetUserId();
            order.OrderStatus = OrderStatus.Completed;
            order.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return RedirectToAction(nameof(OrderCorrection), new { orderNumber = order.OrderNumber, message = "COD payment marked paid." });
    }

    private async Task<OrderCorrectionOrderViewModel?> FindOrderAsync(string orderNumber, CancellationToken cancellationToken)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        var order = await _context.Orders
            .AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.Cashier)
            .Include(x => x.Terminal)
            .Include(x => x.UserSession)
            .Include(x => x.Items)
            .Include(x => x.PrintJobs)
            .FirstOrDefaultAsync(x => x.BranchId == branchId && x.OrderNumber == orderNumber, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var movements = await _context.InventoryMovements
            .AsNoTracking()
            .Include(x => x.InventoryItem)
            .Where(x =>
                x.ReferenceType == nameof(Order) &&
                x.ReferenceId == order.Id &&
                (x.MovementType == InventoryMovementType.SaleConsumption ||
                 x.MovementType == InventoryMovementType.ConsumeReservation ||
                 x.MovementType == InventoryMovementType.WasteReservation))
            .OrderBy(x => x.InventoryItem!.Name)
            .ToListAsync(cancellationToken);

        return new OrderCorrectionOrderViewModel
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            OrderType = order.OrderType,
            OrderStatus = order.OrderStatus,
            InventoryState = order.InventoryState,
            PaymentStatus = order.PaymentStatus,
            BranchName = order.Branch?.Name ?? order.BranchId.ToString(),
            CashierName = order.Cashier?.Email ?? order.CashierId,
            TerminalName = order.TerminalName ?? order.Terminal?.Name ?? order.TerminalCode,
            SessionCode = order.UserSession?.SessionCode,
            CreatedAt = order.CreatedAt,
            SentToKitchenAt = order.SentToKitchenAt,
            TotalAmount = order.TotalAmount,
            Items = order.Items.Select(x => new OrderCorrectionLineViewModel
            {
                Name = x.ProductNameSnapshot,
                Quantity = x.Quantity,
                LineTotal = x.LineTotal
            }).ToList(),
            ConsumedInventory = movements
                .GroupBy(x => new { x.InventoryItemId, Name = x.InventoryItem == null ? x.InventoryItemId.ToString() : x.InventoryItem.Name, Unit = x.InventoryItem == null ? string.Empty : x.InventoryItem.BaseUnit })
                .Select(x => new OrderCorrectionIngredientViewModel
                {
                    Name = x.Key.Name,
                    Unit = x.Key.Unit,
                    Quantity = x.Sum(y => y.QuantityBase)
                }).ToList(),
            PrintJobs = order.PrintJobs
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new OrderCorrectionPrintJobViewModel
                {
                    PrintType = x.PrintType.ToString(),
                    Status = x.Status.ToString(),
                    CreatedAt = x.CreatedAt
                }).ToList()
        };
    }

    private async Task<string> QueueReprintAsync(int orderId, PrintJobType printType, string target, string? reason, CancellationToken cancellationToken)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        var order = await _context.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == orderId && x.BranchId == branchId, cancellationToken)
            ?? throw new PosNotFoundException("Order was not found.");

        _context.PrintJobs.Add(new PrintJob
        {
            BranchId = order.BranchId,
            TerminalId = order.TerminalId > 0 ? order.TerminalId : null,
            OrderId = order.Id,
            PrintType = printType,
            PrinterTarget = target,
            Status = PrintJobStatus.Pending,
            CreatedByUserId = GetUserId(),
            PayloadJson = JsonSerializer.Serialize(new
            {
                order.Id,
                order.OrderNumber,
                order.OrderType,
                order.PaymentStatus,
                order.TotalAmount,
                Reason = string.IsNullOrWhiteSpace(reason) ? "Manager reprint." : reason.Trim(),
                Items = order.Items.Select(x => new { x.ProductNameSnapshot, x.Quantity, x.UnitPrice, x.LineTotal })
            })
        });
        await _context.SaveChangesAsync(cancellationToken);
        return order.OrderNumber;
    }

    private async Task<string> GetOrderNumberAsync(int orderId, CancellationToken cancellationToken)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        return await _context.Orders
            .Where(x => x.Id == orderId && x.BranchId == branchId)
            .Select(x => x.OrderNumber)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new PosNotFoundException("Order was not found.");
    }

    private async Task<Order?> GetOrderForCorrectionActionAsync(int orderId, CancellationToken cancellationToken)
    {
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        return await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == orderId && x.BranchId == branchId, cancellationToken);
    }

    private static bool IsCancelled(OrderStatus orderStatus) =>
        orderStatus is OrderStatus.Cancelled or OrderStatus.CancelledAfterPreparation or OrderStatus.CancelledAsWaste;

    private string GetUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("Authenticated user was not found.");
}
