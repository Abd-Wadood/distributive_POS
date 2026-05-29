using System.ComponentModel.DataAnnotations;
using BranchPOS.Models;

namespace BranchPOS.DTOs;

public class CreateInventoryAdjustmentDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Inventory item is required.")]
    public int InventoryItemId { get; set; }

    [Required]
    public InventoryLocationType? LocationType { get; set; }

    [Required]
    public InventoryAdjustmentType? AdjustmentType { get; set; }

    [Range(typeof(decimal), "0.001", "1000000000", ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    public string? UnitName { get; set; }

    [Required, MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Notes { get; set; }
}
