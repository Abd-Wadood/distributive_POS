using BranchPOS.Models;

namespace BranchPOS.ViewModels;

public class TerminalSetupViewModel
{
    public string TerminalCode { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }

    public List<Terminal> Terminals { get; set; } = new();
}
