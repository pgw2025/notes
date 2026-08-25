using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notes.Api.Data;
using Notes.Api.DTOs.Admin;

namespace Notes.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/tags")]
[Authorize(Policy = "Admin")]
public class AdminTagsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminTagsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>跨用户标签分页列表，支持按标签名/用户邮箱搜索，附带每个标签的笔记数与所属用户</summary>
    [HttpGet]
    public async Task<ActionResult<AdminTagListResponseDto>> List(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _db.Tags.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(t =>
                EF.Functions.Collate(t.Name, "utf8mb4_general_ci").Contains(term) ||
                EF.Functions.Collate(t.User!.Email ?? "", "utf8mb4_general_ci").Contains(term));
        }

        var total = await query.CountAsync();

        var tags = await query
            .OrderBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.UserId,
                NoteCount = t.NoteTags.Count
            })
            .ToListAsync();

        var userIds = tags.Select(t => t.UserId).Distinct().ToList();
        var userMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.Email, u.DisplayName });

        var items = tags.Select(t => new AdminTagDto(
            t.Id,
            t.Name,
            t.UserId,
            userMap.GetValueOrDefault(t.UserId)?.Email,
            userMap.GetValueOrDefault(t.UserId)?.DisplayName,
            t.NoteCount)).ToList();

        return Ok(new AdminTagListResponseDto(items, total));
    }

    /// <summary>管理员重命名标签（同用户内唯一，冲突返回 409）</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Rename(int id, RenameTagDto dto)
    {
        var name = dto.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            return BadRequest(new { message = "标签名称不能为空" });

        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id);
        if (tag == null) return NotFound();

        var exists = await _db.Tags.AnyAsync(t =>
            t.UserId == tag.UserId && t.Name == name && t.Id != id);
        if (exists) return Conflict(new { message = "该用户下已存在同名标签" });

        tag.Name = name;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>删除标签（NoteTag 关联由数据库级联删除）</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id);
        if (tag == null) return NotFound();

        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record RenameTagDto(string Name);
