using BranchPOS.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BranchPOS.ViewModels;

public class SessionStartViewModel
{
    public int BranchId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public string TerminalName { get; set; } = Environment.MachineName;

    public string TerminalCode { get; set; } = string.Empty;

    public string TerminalBranchName { get; set; } = string.Empty;

    public bool CanStartSession { get; set; } = true;

    public string? StartSessionBlockReason { get; set; }

    public string? Notes { get; set; }

    public decimal OpeningCashAmount { get; set; }

    public UserSession? ActiveSession { get; set; }

    public UserSession? AbandonedSession { get; set; }

    public List<SelectListItem> Branches { get; set; } = new();
}
