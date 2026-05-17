using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class Order
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public Guid PublicId { get; set; } = Guid.NewGuid();

    [MaxLength(120)]
    public string? IdempotencyKey { get; set; }

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

    public decimal Subtotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string? TableNumber { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    public bool IsSynced { get; set; }

    public DateTime? SyncedAt { get; set; }
}
