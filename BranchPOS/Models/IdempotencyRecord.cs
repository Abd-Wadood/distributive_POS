using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class IdempotencyRecord
{
    public long Id { get; set; }

    [Required, MaxLength(120)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string OperationType { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string RequestHash { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public int? BranchId { get; set; }

    public Branch? Branch { get; set; }

    public int? TerminalId { get; set; }

    public Terminal? Terminal { get; set; }

    public int? UserSessionId { get; set; }

    public UserSession? UserSession { get; set; }

    [MaxLength(80)]
    public string? ReferenceType { get; set; }

    public int? ReferenceId { get; set; }

    [MaxLength(80)]
    public string? ResourceType { get; set; }

    public int? ResourceId { get; set; }

    public IdempotencyStatus Status { get; set; } = IdempotencyStatus.InProgress;

    public int? ResponseCode { get; set; }

    [MaxLength(500)]
    public string? ResponseBodySummary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }
}
