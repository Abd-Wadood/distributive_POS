using System.ComponentModel.DataAnnotations;
using BranchPOS.Models;

namespace BranchPOS.DTOs;

public class CreateStockCountDto
{
    [Required]
    public DateTime CountDate { get; set; } = DateTime.UtcNow.Date;

    [Required]
    public InventoryLocationType? LocationType { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Inventory item is required.")]
    public int InventoryItemId { get; set; }

    [Range(typeof(decimal), "0", "1000000000")]
    public decimal CountedQuantity { get; set; }

    [Required, MaxLength(500)]
    public string Reason { get; set; } = "Physical count";

    [MaxLength(500)]
    public string? Notes { get; set; }
}
