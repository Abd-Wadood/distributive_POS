namespace BranchPOS.ViewModels;

public class UserListItemViewModel
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? UserName { get; set; }

    public string? BranchName { get; set; }

    public string Roles { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
