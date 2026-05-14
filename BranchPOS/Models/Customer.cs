using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class Customer
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Address { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsSynced { get; set; }

    public DateTime? SyncedAt { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
