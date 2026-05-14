using BranchPOS.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BranchPOS.ViewModels;

public class TerminalAdminViewModel
{
    public List<Terminal> Terminals { get; set; } = new();

    public List<TerminalHeartbeat> Heartbeats { get; set; } = new();

    public TerminalCreateViewModel NewTerminal { get; set; } = new();
}

public class TerminalCreateViewModel
{
    public string TerminalCode { get; set; } = string.Empty;

    public int BranchId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public List<SelectListItem> Branches { get; set; } = new();
}
