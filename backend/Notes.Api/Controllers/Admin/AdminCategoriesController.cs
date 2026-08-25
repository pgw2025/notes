using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notes.Api.Data;
using Notes.Api.DTOs.Admin;

namespace Notes.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/categories")]
[Authorize(Policy = "Admin")]
public class AdminCategoriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminCategoriesController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>跨用户分类分页列表，支持按分类名/用户邮箱搜索，附带每个分类的笔记数与所属用户</summary>
    [HttpGet]
    public async Task<ActionResult<AdminCategoryListResponseDto>> List(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _db.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c =>
                EF.Functions.Collate(c.Name, "utf8mb4_general_ci").Contains(term) ||
                EF.Functions.Collate(c.User!.Email ?? "", "utf8mb4_general_ci").Contains(term));
        }

        var total = await query.CountAsync();

        var categories = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.UserId,
                c.CreatedAt,
                NoteCount = c.Notes.Count
            })
            .ToListAsync();

        var userIds = categories.Select(c => c.UserId).Distinct().ToList();
        var userMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.Email, u.DisplayName });

        var items = categories.Select(c => new AdminCategoryDto(
            c.Id,
            c.Name,
            c.UserId,
            userMap.GetValueOrDefault(c.UserId)?.Email,
            userMap.GetValueOrDefault(c.UserId)?.DisplayName,
            c.NoteCount,
            c.CreatedAt)).ToList();

        return Ok(new AdminCategoryListResponseDto(items, total));
    }

    /// <summary>管理员重命名分类（同用户内唯一，冲突返回 409）</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Rename(int id, RenameCategoryDto dto)
    {
        var name = dto.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            return BadRequest(new { message = "分类名称不能为空" });

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category == null) return NotFound();

        var exists = await _db.Categories.AnyAsync(c =>
            c.UserId == category.UserId && c.Name == name && c.Id != id);
        if (exists) return Conflict(new { message = "该用户下已存在同名分类" });

        category.Name = name;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>删除分类（Note.CategoryId 为 SetNull，笔记自动变为未分类）</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category == null) return NotFound();

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record RenameCategoryDto(string Name);
