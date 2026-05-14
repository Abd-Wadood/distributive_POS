using Microsoft.AspNetCore.Identity;

namespace BranchPOS.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    public int? BranchId { get; set; }

    public Branch? Branch { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Order> Orders { get; set; } = new List<Order>();

    public ICollection<UserSession> UserSessions { get; set; } = new List<UserSession>();
}
