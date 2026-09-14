using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notes.Api.Data;
using Notes.Api.DTOs;
using Notes.Api.Models;

namespace Notes.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public CategoriesController(AppDbContext db)
    {
        _db = db;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> List()
    {
        var cats = await _db.Categories
            .Where(c => c.UserId == UserId)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.CreatedAt,
                c.ParentId,
                DirectCount = c.Notes.Count
            })
            .ToListAsync();

        // 构造 id -> 子树 的映射，用于递归汇总计数
        var byId = cats.ToDictionary(c => c.Id);
        var childrenMap = cats
            .GroupBy(c => c.ParentId)
            .ToDictionary(g => g.Key ?? 0, g => g.Select(c => c.Id).ToList());

        // 计算每个分类「含子孙」的递归笔记总数（记忆化）
        var memo = new Dictionary<int, int>();
        int TotalNotes(int id)
        {
            if (memo.TryGetValue(id, out var cached)) return cached;
            var sum = byId[id].DirectCount;
            if (childrenMap.TryGetValue(id, out var childIds))
                foreach (var cid in childIds)
                    sum += TotalNotes(cid);
            memo[id] = sum;
            return sum;
        }

        CategoryDto Build(int id)
        {
            var c = byId[id];
            var children = childrenMap.TryGetValue(id, out var childIds)
                ? childIds.OrderBy(x => byId[x].Name).Select(Build).ToList()
                : new List<CategoryDto>();
            return new CategoryDto(c.Id, c.Name, TotalNotes(id), c.CreatedAt, c.ParentId, children);
        }

        var roots = childrenMap.TryGetValue(0, out var rootIds)
            ? rootIds.OrderBy(x => byId[x].Name).Select(Build).ToList()
            : new List<CategoryDto>();

        return Ok(roots);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryDto dto)
    {
        var name = dto.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            return BadRequest(new { message = "分类名称不能为空" });

        // 校验父分类存在且属于当前用户
        if (dto.ParentId.HasValue)
        {
            var parentExists = await _db.Categories.AnyAsync(c => c.Id == dto.ParentId.Value && c.UserId == UserId);
            if (!parentExists) return BadRequest(new { message = "父分类不存在" });
        }

        // 同名冲突：仅在同一父分类下检查
        var exists = await _db.Categories.AnyAsync(c =>
            c.UserId == UserId && c.Name == name && c.ParentId == dto.ParentId);
        if (exists) return Conflict(new { message = "该层级下已存在同名分类" });

        var cat = new Category { Name = name, UserId = UserId, ParentId = dto.ParentId };
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        return Ok(new CategoryDto(cat.Id, cat.Name, 0, cat.CreatedAt, cat.ParentId, new List<CategoryDto>()));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateCategoryDto dto)
    {
        var name = dto.Name?.Trim() ?? string.Empty;
        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId);
        if (cat == null) return NotFound();

        if (string.IsNullOrEmpty(name))
            return BadRequest(new { message = "分类名称不能为空" });

        // 不能把自己（或自己的子孙）设为父分类，避免成环
        if (dto.ParentId.HasValue)
        {
            if (dto.ParentId.Value == id)
                return BadRequest(new { message = "不能将分类设为自身的子分类" });

            var descendantIds = await GetDescendantIds(id);
            if (descendantIds.Contains(dto.ParentId.Value))
                return BadRequest(new { message = "不能将分类移动到其子分类下" });

            var parentExists = await _db.Categories.AnyAsync(c => c.Id == dto.ParentId.Value && c.UserId == UserId);
            if (!parentExists) return BadRequest(new { message = "父分类不存在" });
        }

        cat.Name = name;
        cat.ParentId = dto.ParentId;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId);
        if (cat == null) return NotFound();

        var hasChildren = await _db.Categories.AnyAsync(c => c.ParentId == id && c.UserId == UserId);
        if (hasChildren)
            return BadRequest(new { message = "该分类下还有子分类，请先删除或移动子分类" });

        // 笔记的 CategoryId 外键为 SetNull，删除分类后笔记变为「未分类」
        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>返回某分类所有子孙分类的 Id 集合（用于防止成环）</summary>
    private async Task<HashSet<int>> GetDescendantIds(int id)
    {
        var all = await _db.Categories
            .Where(c => c.UserId == UserId)
            .Select(c => new { c.Id, c.ParentId })
            .ToListAsync();

        var childrenMap = all
            .Where(c => c.ParentId.HasValue)
            .GroupBy(c => c.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Id).ToList());

        var result = new HashSet<int>();
        var stack = new Stack<int>();
        if (childrenMap.TryGetValue(id, out var direct))
            foreach (var cid in direct) stack.Push(cid);

        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            if (!result.Add(cur)) continue;
            if (childrenMap.TryGetValue(cur, out var next))
                foreach (var cid in next) stack.Push(cid);
        }
        return result;
    }
}
