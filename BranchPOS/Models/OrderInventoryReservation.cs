using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class OrderInventoryReservation
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    public int OrderId { get; set; }

    public Order? Order { get; set; }

    public int InventoryStockId { get; set; }

    public InventoryStock? InventoryStock { get; set; }

    public int InventoryItemId { get; set; }

    public InventoryItem? InventoryItem { get; set; }

    public int InventoryLocationId { get; set; }

    public InventoryLocation? InventoryLocation { get; set; }

    public decimal RequiredQuantityBase { get; set; }

    public OrderInventoryReservationStatus Status { get; set; } = OrderInventoryReservationStatus.Active;

    [MaxLength(120)]
    public string? IdempotencyKey { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReleasedAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public DateTime? WastedAt { get; set; }
}
