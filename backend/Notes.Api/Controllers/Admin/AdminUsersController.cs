using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notes.Api.Data;
using Notes.Api.DTOs.Admin;
using Notes.Api.Models;

namespace Notes.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminUsersController(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>用户分页列表，支持按邮箱/昵称搜索；附带每个用户的笔记数</summary>
    [HttpGet]
    public async Task<ActionResult<AdminUserListResponseDto>> List([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u =>
                EF.Functions.Collate(u.Email ?? "", "utf8mb4_general_ci").Contains(term) ||
                EF.Functions.Collate(u.DisplayName ?? "", "utf8mb4_general_ci").Contains(term) ||
                EF.Functions.Collate(u.UserName ?? "", "utf8mb4_general_ci").Contains(term));
        }

        var total = await query.CountAsync();

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                User = u,
                NoteCount = _db.Notes.Count(n => n.UserId == u.Id)
            })
            .ToListAsync();

        var items = users.Select(x => new AdminUserDto(
            x.User.Id,
            x.User.Email ?? string.Empty,
            x.User.DisplayName,
            x.User.AvatarUrl,
            x.User.CreatedAt,
            IsLockedOut(x.User),
            x.NoteCount)).ToList();

        return Ok(new AdminUserListResponseDto(items, total));
    }

    /// <summary>禁用 / 启用用户（用 Identity 的 LockoutEnd 实现，禁用时设为远未来时间）</summary>
    [HttpPut("{id}/status")]
    public async Task<IActionResult> SetStatus(string id, [FromBody] SetStatusDto dto)
    {
        if (id == CurrentUserId)
            return BadRequest(new { message = "不能禁用/操作自己的账号" });

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // 避免序列化问题：Identity 序列化 lockoutEnd 需要 Set（true）
        await _userManager.SetLockoutEnabledAsync(user, true);

        var result = dto.Locked
            ? await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100))
            : await _userManager.SetLockoutEndDateAsync(user, null);

        if (!result.Succeeded)
            return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });

        return Ok(new { user.Id, Locked = IsLockedOut(user) });
    }

    /// <summary>管理员重置指定用户密码</summary>
    [HttpPut("{id}/password")]
    public async Task<IActionResult> ResetPassword(string id, [FromBody] ResetPasswordDto dto)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // 校验强度由 Identity 规则把关（长度≥6 等）
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);

        if (!result.Succeeded)
            return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });

        return Ok(new { message = "密码已重置" });
    }

    private static bool IsLockedOut(ApplicationUser user)
        => user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
}

public record SetStatusDto(bool Locked);

public record ResetPasswordDto(string NewPassword);