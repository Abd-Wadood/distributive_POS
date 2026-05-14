using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class Terminal
{
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string TerminalCode { get; set; } = string.Empty;

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(60)]
    public string? IpAddress { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
