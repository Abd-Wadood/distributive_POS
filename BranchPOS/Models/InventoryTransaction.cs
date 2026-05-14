namespace BranchPOS.Models;

public class InventoryTransaction
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

    public int IngredientId { get; set; }

    public Ingredient? Ingredient { get; set; }

    public InventoryTransactionType TransactionType { get; set; }

    public decimal QuantityChanged { get; set; }

    public int? ReferenceId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsSynced { get; set; }

    public DateTime? SyncedAt { get; set; }
}
