namespace BranchPOS.ViewModels;

public class ProfitReportViewModel
{
    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public decimal SalesRevenue { get; set; }

    public decimal IngredientCost { get; set; }

    public decimal OperationalExpenses { get; set; }

    public decimal NetProfit { get; set; }
}
