using Microsoft.AspNetCore.Identity;

namespace Notes.Api.Models;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }

    public string? AvatarUrl { get; set; }

    /// <summary>用户级默认笔记背景色（HEX 格式如 "#FFF9C4"）。新建笔记时若未指定颜色则用此值。</summary>
    public string? DefaultNoteColor { get; set; }

    /// <summary>
    /// 用户自定义的色板（JSON 数组字符串，如 "[\"#FFFFFF\",\"#FFF9C4\"]"）。
    /// null 表示未初始化，首次访问时由前端或后端填入默认色板。
    /// </summary>
    public string? CustomColors { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
