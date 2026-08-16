using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notes.Api.Data;
using Notes.Api.DTOs;
using Notes.Api.Models;
using Notes.Api.Services;

namespace Notes.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class AttachmentsController : ControllerBase
{
    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB

    private readonly AppDbContext _db;
    private readonly IFileStorageService _files;

    public AttachmentsController(AppDbContext db, IFileStorageService files)
    {
        _db = db;
        _files = files;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpPost("notes/{noteId:int}/attachments")]
    public async Task<ActionResult<AttachmentDto>> Upload(int noteId, IFormFile file)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == UserId);
        if (note == null) return NotFound();

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "请选择文件" });

        if (file.Length > MaxFileSize)
            return BadRequest(new { message = "文件大小不能超过 10MB" });

        var (path, name, size) = await _files.SaveAsync(file, noteId);
        var att = new Attachment
        {
            NoteId = noteId,
            FileName = name,
            FilePath = path,
            ContentType = file.ContentType,
            Size = size
        };
        _db.Attachments.Add(att);
        await _db.SaveChangesAsync();

        return Ok(new AttachmentDto(att.Id, att.FileName, att.Size, att.ContentType));
    }

    [HttpGet("attachments/{id:int}")]
    public async Task<IActionResult> Download(int id)
    {
        var att = await _db.Attachments
            .Include(a => a.Note)
            .FirstOrDefaultAsync(a => a.Id == id && a.Note.UserId == UserId);
        if (att == null) return NotFound();

        var (stream, contentType, fileName) = await _files.GetAsync(att.FilePath);
        return File(stream, contentType, fileName);
    }

    [HttpDelete("attachments/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var att = await _db.Attachments
            .Include(a => a.Note)
            .FirstOrDefaultAsync(a => a.Id == id && a.Note.UserId == UserId);
        if (att == null) return NotFound();

        _files.Delete(att.FilePath);
        _db.Attachments.Remove(att);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
