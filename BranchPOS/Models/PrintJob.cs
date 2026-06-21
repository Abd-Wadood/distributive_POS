using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class PrintJob
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    public int? TerminalId { get; set; }

    public Terminal? Terminal { get; set; }

    public int OrderId { get; set; }

    public Order? Order { get; set; }

    public PrintJobType PrintType { get; set; } = PrintJobType.KOT;

    [MaxLength(80)]
    public string? PrinterTarget { get; set; }

    public string PayloadJson { get; set; } = "{}";

    public PrintJobStatus Status { get; set; } = PrintJobStatus.Pending;

    public int RetryCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PrintedAt { get; set; }

    public string? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }
}
