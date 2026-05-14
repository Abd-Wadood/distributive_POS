using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class Branch
{
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string BranchCode { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
