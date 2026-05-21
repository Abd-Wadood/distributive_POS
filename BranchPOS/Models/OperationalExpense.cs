using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class OperationalExpense
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    public int ExpenseCategoryId { get; set; }

    public ExpenseCategory? ExpenseCategory { get; set; }

    public decimal Amount { get; set; }

    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow.Date;

    public int? PaymentMethodId { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }
}
