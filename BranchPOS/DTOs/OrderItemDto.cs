using System.ComponentModel.DataAnnotations;

namespace BranchPOS.DTOs;

public class OrderItemDto
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 10000)]
    public int Quantity { get; set; }
}
