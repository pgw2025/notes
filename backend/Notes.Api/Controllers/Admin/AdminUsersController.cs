using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notes.Api.Data;
using Notes.Api.DTOs.Admin;
using Notes.Api.Models;
using Notes.Api.Services;

namespace Notes.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IFileStorageService _files;

    public AdminUsersController(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IFileStorageService files)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _files = files;
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

        // 批量计算管理员 Id 集合，供列表标记 IsAdmin（避免逐条查角色）
        var adminRole = await _roleManager.FindByNameAsync("Admin");
        var adminUserIds = adminRole != null
            ? await _db.UserRoles.Where(ur => ur.RoleId == adminRole.Id).Select(ur => ur.UserId).ToListAsync()
            : new List<string>();

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
            adminUserIds.Contains(x.User.Id),
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

    /// <summary>
    /// 删除用户：先清理关联的物理文件（附件 + 头像）与无级联的 NoteVersion，
    /// 再 UserManager.DeleteAsync 级联删除业务表（Note/Category/Tag/NoteTag/Attachment）与 Identity 表。
    /// 硬删除，不可恢复。
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (id == CurrentUserId)
            return BadRequest(new { message = "不能删除自己的账号" });

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (await _userManager.IsInRoleAsync(user, "Admin"))
            return BadRequest(new { message = "不能删除管理员账号" });

        // 1. 载入该用户所有笔记及附件，用于清理物理文件与定位 NoteVersion
        var notes = await _db.Notes
            .Where(n => n.UserId == id)
            .Include(n => n.Attachments)
            .ToListAsync();
        var noteIds = notes.Select(n => n.Id).ToList();

        // 2. 清理附件物理文件（单文件失败不影响主流程）
        foreach (var att in notes.SelectMany(n => n.Attachments))
        {
            try { _files.Delete(att.FilePath); } catch { /* 忽略单个文件删除失败 */ }
        }

        // 3. 清理头像物理文件
        if (!string.IsNullOrEmpty(user.AvatarUrl))
        {
            var avatarName = user.AvatarUrl.StartsWith("/api/auth/avatar/")
                ? user.AvatarUrl.Substring("/api/auth/avatar/".Length)
                : Path.GetFileName(user.AvatarUrl);
            var avatarPath = Path.Combine(_files.GetUploadsRoot(), "Avatars", avatarName);
            try { _files.Delete(avatarPath); } catch { /* 忽略 */ }
        }

        // 4. 显式删除无级联配置的 NoteVersion，避免后续 DeleteAsync 的 FK 约束冲突
        var versions = await _db.NoteVersions.Where(v => noteIds.Contains(v.NoteId)).ToListAsync();
        _db.NoteVersions.RemoveRange(versions);
        await _db.SaveChangesAsync();

        // 5. 删除用户（业务表与 Identity 表由 EF / Identity 级联删除）
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });

        return NoContent();
    }
}

public record SetStatusDto(bool Locked);

public record ResetPasswordDto(string NewPassword);