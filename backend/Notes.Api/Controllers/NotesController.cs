using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notes.Api.Data;
using Notes.Api.DTOs;
using Notes.Api.Models;

namespace Notes.Api.Controllers;

[ApiController]
[Route("api/notes")]
[Authorize]
public class NotesController : ControllerBase
{
    private readonly AppDbContext _db;

    public NotesController(AppDbContext db)
    {
        _db = db;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NoteListItemDto>>> List([FromQuery] int? categoryId)
    {
        var query = _db.Notes
            .Include(n => n.Category)
            .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
            .Where(n => n.UserId == UserId);

        if (categoryId.HasValue)
            query = query.Where(n => n.CategoryId == categoryId);

        var notes = await query
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync();

        return Ok(notes.Select(MapToListItem));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NoteDto>> Get(int id)
    {
        var note = await _db.Notes
            .Include(n => n.Category)
            .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
            .Include(n => n.Attachments)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == UserId);

        if (note == null) return NotFound();

        return Ok(MapToDto(note));
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<NoteListItemDto>>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Array.Empty<NoteListItemDto>());

        var term = q.Trim();
        var notes = await _db.Notes
            .Include(n => n.Category)
            .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
            .Where(n => n.UserId == UserId &&
                (EF.Functions.Collate(n.Title, "utf8mb4_general_ci").Contains(term) ||
                 EF.Functions.Collate(n.Content, "utf8mb4_general_ci").Contains(term)))
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync();

        return Ok(notes.Select(MapToListItem));
    }

    [HttpPost]
    public async Task<ActionResult<NoteDto>> Create(CreateNoteDto dto)
    {
        var note = new Note
        {
            Title = dto.Title,
            Content = dto.Content,
            CategoryId = dto.CategoryId,
            UserId = UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await SyncTags(note, dto.TagIds);
        _db.Notes.Add(note);
        await _db.SaveChangesAsync();

        var saved = await _db.Notes
            .Include(n => n.Category)
            .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
            .Include(n => n.Attachments)
            .FirstAsync(n => n.Id == note.Id);

        return CreatedAtAction(nameof(Get), new { id = saved.Id }, MapToDto(saved));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateNoteDto dto)
    {
        var note = await _db.Notes
            .Include(n => n.NoteTags)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == UserId);
        if (note == null) return NotFound();

        note.Title = dto.Title;
        note.Content = dto.Content;
        note.CategoryId = dto.CategoryId;
        note.UpdatedAt = DateTime.UtcNow;

        _db.NoteTags.RemoveRange(note.NoteTags);
        await SyncTags(note, dto.TagIds);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == UserId);
        if (note == null) return NotFound();

        _db.Notes.Remove(note);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task SyncTags(Note note, List<int> tagIds)
    {
        if (tagIds is null || tagIds.Count == 0) return;

        var validTags = await _db.Tags
            .Where(t => t.UserId == UserId && tagIds.Contains(t.Id))
            .ToListAsync();

        foreach (var tag in validTags)
            note.NoteTags.Add(new NoteTag { Tag = tag });
    }

    private static NoteListItemDto MapToListItem(Note n) => new(
        n.Id,
        n.Title,
        Preview(n.Content),
        n.CategoryId,
        n.Category?.Name,
        n.UpdatedAt,
        n.NoteTags.Select(nt => nt.Tag.Name).ToList());

    private static NoteDto MapToDto(Note n) => new(
        n.Id,
        n.Title,
        n.Content,
        n.CategoryId,
        n.Category?.Name,
        n.CreatedAt,
        n.UpdatedAt,
        n.NoteTags.Select(nt => nt.Tag.Name).ToList(),
        n.Attachments.Select(a => new AttachmentDto(a.Id, a.FileName, a.Size, a.ContentType)).ToList());

    private static string Preview(string content)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        var text = content.Length > 120 ? content[..120] + "…" : content;
        return text.Replace("\n", " ").Replace("\r", "");
    }
}
