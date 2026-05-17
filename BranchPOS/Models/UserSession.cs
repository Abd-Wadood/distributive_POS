using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class UserSession
{
    public int Id { get; set; }

    public Guid PublicId { get; set; } = Guid.NewGuid();

    [MaxLength(120)]
    public string? IdempotencyKey { get; set; }

    [MaxLength(120)]
    public string? CloseIdempotencyKey { get; set; }

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

    public decimal OpeningCashAmount { get; set; }

    public decimal? CountedClosingCash { get; set; }

    public decimal? ExpectedClosingCash { get; set; }

    public decimal? CashDifference { get; set; }

    public bool RequiresManagerApproval { get; set; }

    public DateTime? ClosingRequestedAt { get; set; }

    public string? ClosedByUserId { get; set; }

    public ApplicationUser? ClosedByUser { get; set; }

    public string? ReopenedByUserId { get; set; }

    public ApplicationUser? ReopenedByUser { get; set; }

    public DateTime? ReopenedAt { get; set; }

    [MaxLength(500)]
    public string? ReopenReason { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsSynced { get; set; }

    public DateTime? SyncedAt { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
