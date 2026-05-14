using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class UserSession
{
    public int Id { get; set; }

    public Guid PublicId { get; set; } = Guid.NewGuid();

    [Required, MaxLength(50)]
    public string SessionCode { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    [Required, MaxLength(40)]
    public string RoleName { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string TerminalName { get; set; } = string.Empty;

    public int TerminalId { get; set; }

    public Terminal? Terminal { get; set; }

    [Required, MaxLength(40)]
    public string TerminalCode { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EndedAt { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.Active;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsSynced { get; set; }

    public DateTime? SyncedAt { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
