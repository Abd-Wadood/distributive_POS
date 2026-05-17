using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class AuditLog
{
    public long Id { get; set; }

    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public int? BranchId { get; set; }

    public Branch? Branch { get; set; }

    public int? TerminalId { get; set; }

    public Terminal? Terminal { get; set; }

    [Required, MaxLength(120)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(120)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Severity { get; set; } = "Info";

    [MaxLength(500)]
    public string? Message { get; set; }

    [MaxLength(256)]
    public string? AttemptedUserName { get; set; }

    [Required, MaxLength(120)]
    public string EntityName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? EntityId { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(80)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }
}
