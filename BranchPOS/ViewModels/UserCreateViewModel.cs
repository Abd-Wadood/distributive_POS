using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BranchPOS.ViewModels;

public class UserCreateViewModel
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    public int? BranchId { get; set; }

    public List<SelectListItem> Roles { get; set; } = new();

    public List<SelectListItem> Branches { get; set; } = new();
}
