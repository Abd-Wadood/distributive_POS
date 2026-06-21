using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class Order
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public Guid PublicId { get; set; } = Guid.NewGuid();

    [MaxLength(120)]
    public string? IdempotencyKey { get; set; }

    [MaxLength(120)]
    public string? ClientRequestId { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    public int? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public string CashierId { get; set; } = string.Empty;

    public ApplicationUser? Cashier { get; set; }

    public int? UserSessionId { get; set; }

    public UserSession? UserSession { get; set; }

    public string? TerminalName { get; set; }

    public int TerminalId { get; set; }

    public Terminal? Terminal { get; set; }

    public string TerminalCode { get; set; } = string.Empty;

    public OrderType OrderType { get; set; } = OrderType.Takeaway;

    public OrderStatus OrderStatus { get; set; } = OrderStatus.Draft;

    public OrderInventoryState InventoryState { get; set; } = OrderInventoryState.None;

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    [MaxLength(40)]
    public string? PaymentMethod { get; set; }

    public decimal Subtotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string? TableNumber { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentToKitchenAt { get; set; }

    public DateTime? ReadyAt { get; set; }

    public DateTime? DispatchedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancelledByUserId { get; set; }

    public ApplicationUser? CancelledByUser { get; set; }

    [MaxLength(500)]
    public string? CancellationReason { get; set; }

    [MaxLength(40)]
    public string? InventoryCorrectionType { get; set; }

    public DateTime? PaymentReceivedAt { get; set; }

    public string? PaymentReceivedByUserId { get; set; }

    public ApplicationUser? PaymentReceivedByUser { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    public ICollection<OrderInventoryReservation> InventoryReservations { get; set; } = new List<OrderInventoryReservation>();

    public ICollection<PrintJob> PrintJobs { get; set; } = new List<PrintJob>();

    public bool IsSynced { get; set; }

    public DateTime? SyncedAt { get; set; }
}
