using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notes.Api.Data;
using Notes.Api.DTOs.Admin;
using Notes.Api.Models;
using Notes.Api.Services;

namespace Notes.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/notes")]
[Authorize(Policy = "Admin")]
public class AdminNotesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _files;

    public AdminNotesController(AppDbContext db, IFileStorageService files)
    {
        _db = db;
        _files = files;
    }

    /// <summary>跨用户笔记分页列表，支持用户/关键词/分类/时间范围筛选</summary>
    [HttpGet]
    public async Task<ActionResult<AdminNoteListResponseDto>> List(
        [FromQuery] string? userId,
        [FromQuery] string? q,
        [FromQuery] int? categoryId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _db.Notes
            .AsNoTracking()
            .Include(n => n.Category)
            .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
            .AsSplitQuery();

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(n => n.UserId == userId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(n =>
                EF.Functions.Collate(n.Title, "utf8mb4_general_ci").Contains(term) ||
                EF.Functions.Collate(n.Content, "utf8mb4_general_ci").Contains(term));
        }

        if (categoryId.HasValue)
            query = query.Where(n => n.CategoryId == categoryId);

        if (from.HasValue)
            query = query.Where(n => n.UpdatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(n => n.UpdatedAt <= to.Value.Date.AddDays(1).AddTicks(-1));

        var total = await query.CountAsync();

        // 一次性查出所需的用户信息拼接（避免 N+1）
        var notes = await query
            .OrderByDescending(n => n.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userIds = notes.Select(n => n.UserId).Distinct().ToList();
        var userMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.Email, u.DisplayName });

        // 统计附件数/版本数
        var noteIds = notes.Select(n => n.Id).ToList();
        var attachCounts = await _db.Attachments
            .Where(a => noteIds.Contains(a.NoteId))
            .GroupBy(a => a.NoteId)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
        var versionCounts = await _db.NoteVersions
            .Where(v => noteIds.Contains(v.NoteId))
            .GroupBy(v => v.NoteId)
            .ToDictionaryAsync(g => g.Key, g => g.Count());

        var items = notes.Select(n => new AdminNoteListItemDto(
            n.Id,
            n.Title,
            Preview(n.Content),
            n.UserId,
            userMap.GetValueOrDefault(n.UserId)?.Email,
            userMap.GetValueOrDefault(n.UserId)?.DisplayName,
            n.CategoryId,
            n.Category?.Name,
            n.NoteTags.Select(nt => nt.Tag.Name).OrderBy(x => x).ToList(),
            attachCounts.GetValueOrDefault(n.Id),
            versionCounts.GetValueOrDefault(n.Id),
            n.IsPinned,
            n.CreatedAt,
            n.UpdatedAt)).ToList();

        return Ok(new AdminNoteListResponseDto(items, total));
    }

    /// <summary>单篇笔记详情（含全文、作者、标签、附件列表）</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminNoteDetailDto>> Get(int id)
    {
        var note = await _db.Notes
            .AsNoTracking()
            .Include(n => n.Category)
            .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
            .Include(n => n.Attachments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(n => n.Id == id);

        if (note == null) return NotFound();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == note.UserId);

        return Ok(new AdminNoteDetailDto(
            note.Id,
            note.Title,
            note.Content,
            note.UserId,
            user?.Email,
            user?.DisplayName,
            note.CategoryId,
            note.Category?.Name,
            note.NoteTags.Select(nt => nt.Tag.Name).OrderBy(x => x).ToList(),
            note.Attachments.Select(a => new AdminAttachmentDto(a.Id, a.FileName, a.Size, a.ContentType, a.CreatedAt)).ToList(),
            note.IsPinned,
            note.CreatedAt,
            note.UpdatedAt));
    }

    /// <summary>
    /// 删除笔记：先删除关联的 NoteVersion、NoteTag、Attachment 及附件物理文件，再删除笔记。
    /// Note 与 NoteTag/Attachment 有数据库级级联，但 NoteVersion 无 Cascade 配置，需显式先删；
    /// 附件物理文件需手动清理。
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var note = await _db.Notes
            .Include(n => n.Attachments)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (note == null) return NotFound();

        // 清理附件物理文件
        foreach (var att in note.Attachments)
        {
            _files.Delete(att.FilePath);
        }

        // 显式删除版本历史（避免外键约束）
        var versions = _db.NoteVersions.Where(v => v.NoteId == id);
        _db.NoteVersions.RemoveRange(versions);

        // 删除笔记（NoteTag、Attachment 由数据库级联删除）
        _db.Notes.Remove(note);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static string Preview(string content)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        var text = content.Length > 120 ? content[..120] + "…" : content;
        return text.Replace("\n", " ").Replace("\r", "");
    }
}