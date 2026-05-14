namespace BranchPOS.Models;

public class Purchase
{
    public int Id { get; set; }

    public Guid PublicId { get; set; } = Guid.NewGuid();

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    public int? UserSessionId { get; set; }

    public UserSession? UserSession { get; set; }

    public string? PerformedByUserId { get; set; }

    public ApplicationUser? PerformedByUser { get; set; }

    public int TerminalId { get; set; }

    public Terminal? Terminal { get; set; }

    public string TerminalCode { get; set; } = string.Empty;

    public int SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();

    public bool IsSynced { get; set; }

    public DateTime? SyncedAt { get; set; }
}
