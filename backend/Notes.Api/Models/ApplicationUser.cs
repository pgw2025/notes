using Microsoft.AspNetCore.Identity;

namespace Notes.Api.Models;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }

    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
