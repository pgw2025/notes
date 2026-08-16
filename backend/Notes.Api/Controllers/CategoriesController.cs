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
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Notes.Count, c.CreatedAt))
            .ToListAsync();
        return Ok(cats);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryDto dto)
    {
        var name = dto.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            return BadRequest(new { message = "分类名称不能为空" });

        var exists = await _db.Categories.AnyAsync(c => c.UserId == UserId && c.Name == name);
        if (exists) return Conflict(new { message = "分类名称已存在" });

        var cat = new Category { Name = name, UserId = UserId };
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        return Ok(new CategoryDto(cat.Id, cat.Name, 0, cat.CreatedAt));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateCategoryDto dto)
    {
        var name = dto.Name?.Trim() ?? string.Empty;
        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId);
        if (cat == null) return NotFound();

        cat.Name = name;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId);
        if (cat == null) return NotFound();

        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
