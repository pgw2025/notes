using Microsoft.AspNetCore.Identity;

namespace Notes.Api.Models;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }

    public string? AvatarUrl { get; set; }

    /// <summary>用户级默认笔记背景色（HEX 格式如 "#FFF9C4"）。新建笔记时若未指定颜色则用此值。</summary>
    public string? DefaultNoteColor { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
