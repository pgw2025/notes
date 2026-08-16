using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notes.Api.Data;
using Notes.Api.DTOs;
using Notes.Api.Models;

namespace Notes.Api.Controllers;

[ApiController]
[Route("api/tags")]
[Authorize]
public class TagsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TagsController(AppDbContext db)
    {
        _db = db;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TagDto>>> List()
    {
        var tags = await _db.Tags
            .Where(t => t.UserId == UserId)
            .OrderBy(t => t.Name)
            .Select(t => new TagDto(t.Id, t.Name, t.NoteTags.Count))
            .ToListAsync();
        return Ok(tags);
    }

    [HttpPost]
    public async Task<ActionResult<TagDto>> Create(CreateTagDto dto)
    {
        var name = dto.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            return BadRequest(new { message = "标签名称不能为空" });

        var exists = await _db.Tags.AnyAsync(t => t.UserId == UserId && t.Name == name);
        if (exists) return Conflict(new { message = "标签名称已存在" });

        var tag = new Tag { Name = name, UserId = UserId };
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync();
        return Ok(new TagDto(tag.Id, tag.Name, 0));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == UserId);
        if (tag == null) return NotFound();

        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
