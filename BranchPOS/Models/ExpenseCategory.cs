using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Models;

public class ExpenseCategory
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public Branch? Branch { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OperationalExpense> Expenses { get; set; } = new List<OperationalExpense>();
}
