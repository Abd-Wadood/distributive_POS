using System.ComponentModel.DataAnnotations;

namespace BranchPOS.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember this device")]
    public bool RememberMe { get; set; }

    public string? TerminalCode { get; set; }

    public string? TerminalName { get; set; }

    public string? TerminalBranchName { get; set; }

    public bool HasRegisteredTerminal { get; set; }

    public string? ReturnUrl { get; set; }
}
