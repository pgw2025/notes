using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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

    [HttpGet("timeline")]
    public async Task<ActionResult<TimelineResponseDto>> Timeline([FromQuery] TimelineQueryParams q)
    {
        var query = _db.Notes
            .Include(n => n.Category)
            .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
            .Where(n => n.UserId == UserId);

        // 按分类过滤（多分类 "或"）
        if (q.CategoryIds != null && q.CategoryIds.Count > 0)
            query = query.Where(n => n.CategoryId.HasValue && q.CategoryIds.Contains(n.CategoryId.Value));

        // 按标签过滤：笔记标签集合中任意一个包含 tagIds 里的即匹配
        if (q.TagIds != null && q.TagIds.Count > 0)
            query = query.Where(n => n.NoteTags.Any(nt => q.TagIds.Contains(nt.TagId)));

        // 按关键词
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(n =>
                EF.Functions.Collate(n.Title, "utf8mb4_general_ci").Contains(kw) ||
                EF.Functions.Collate(n.Content, "utf8mb4_general_ci").Contains(kw));
        }

        // 按时间范围（UpdatedAt）
        if (q.FromDate.HasValue)
        {
            var from = q.FromDate.Value.Date;
            query = query.Where(n => n.UpdatedAt >= from);
        }
        if (q.ToDate.HasValue)
        {
            var to = q.ToDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(n => n.UpdatedAt <= to);
        }

        var notes = await query
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync();

        var total = notes.Count;

        // 按 UpdatedAt 分组：年 → 月 → 日
        var years = notes
            .GroupBy(n => n.UpdatedAt.Year)
            .OrderByDescending(g => g.Key)
            .Select(yearGroup =>
            {
                var currentYear = DateTime.UtcNow.Year;
                var months = yearGroup
                    .GroupBy(n => n.UpdatedAt.Month)
                    .OrderByDescending(g => g.Key)
                    .Select(monthGroup =>
                    {
                        var now = DateTime.UtcNow;
                        var isCurrentMonth = yearGroup.Key == now.Year && monthGroup.Key == now.Month;
                        var culture = new CultureInfo("zh-CN");
                        var days = monthGroup
                            .GroupBy(n => n.UpdatedAt.Date)
                            .OrderByDescending(g => g.Key)
                            .Select(dayGroup => new TimelineDayDto(
                                dayGroup.Key,
                                culture.DateTimeFormat.GetDayName(dayGroup.Key.DayOfWeek),
                                dayGroup.Select(MapToListItem).OrderByDescending(x => x.UpdatedAt).ToList(),
                                dayGroup.Count()))
                            .ToList();

                        return new TimelineMonthDto(
                            yearGroup.Key,
                            monthGroup.Key,
                            $"{monthGroup.Key}月",
                            days,
                            days.Sum(d => d.NoteCount),
                            isCurrentMonth);
                    })
                    .ToList();

                return new TimelineYearDto(
                    yearGroup.Key,
                    months,
                    months.Sum(m => m.NoteCount),
                    yearGroup.Key == currentYear);
            })
            .ToList();

        return Ok(new TimelineResponseDto(total, years));
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

        // 新建后立即写初始版本（不会触发去重，因为是新笔记）
        var freshTags = await _db.NoteTags
            .Where(nt => nt.NoteId == note.Id)
            .Select(nt => nt.TagId)
            .OrderBy(x => x)
            .ToListAsync();
        var snapshot = ComputeSnapshotHash(dto.Title, dto.Content, dto.CategoryId, freshTags);
        _db.NoteVersions.Add(new NoteVersion
        {
            NoteId = note.Id,
            Title = dto.Title,
            Content = dto.Content,
            CategoryId = dto.CategoryId,
            TagIdsSnapshot = TagIdsToSnapshot(freshTags),
            CreatedByUserId = UserId,
            CreatedAt = DateTime.UtcNow,
            SnapshotHash = snapshot
        });
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

        // 计算保存后的快照 Hash，和上一个版本相同则不生成新版本
        var sortedNewTagIds = (dto.TagIds ?? new List<int>()).OrderBy(x => x).ToList();
        var newHash = ComputeSnapshotHash(dto.Title, dto.Content, dto.CategoryId, sortedNewTagIds);

        var lastHash = await _db.NoteVersions
            .Where(v => v.NoteId == id && v.CreatedByUserId == UserId)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => v.SnapshotHash)
            .FirstOrDefaultAsync();

        var contentChanged = lastHash == null || lastHash != newHash;

        note.Title = dto.Title;
        note.Content = dto.Content;
        note.CategoryId = dto.CategoryId;
        note.UpdatedAt = DateTime.UtcNow;

        _db.NoteTags.RemoveRange(note.NoteTags);
        await SyncTags(note, dto.TagIds ?? new List<int>());

        if (contentChanged)
        {
            _db.NoteVersions.Add(new NoteVersion
            {
                NoteId = id,
                Title = dto.Title,
                Content = dto.Content,
                CategoryId = dto.CategoryId,
                TagIdsSnapshot = TagIdsToSnapshot(sortedNewTagIds),
                CreatedByUserId = UserId,
                CreatedAt = DateTime.UtcNow,
                SnapshotHash = newHash
            });
        }

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

    // ===================== 笔记版本历史 =====================

    [HttpGet("{id:int}/versions")]
    public async Task<ActionResult<IEnumerable<NoteVersionListItemDto>>> ListVersions(int id)
    {
        var note = await _db.Notes
            .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == UserId);
        if (note == null) return NotFound();

        var currentHash = ComputeSnapshotHash(
            note.Title, note.Content, note.CategoryId,
            note.NoteTags.Select(nt => nt.TagId).OrderBy(x => x).ToList());

        var tagNamesMap = await _db.Tags
            .Where(t => t.UserId == UserId)
            .ToDictionaryAsync(t => t.Id, t => t.Name);

        var categoryNamesMap = await _db.Categories
            .Where(c => c.UserId == UserId)
            .ToDictionaryAsync(c => c.Id, c => c.Name);

        var versions = await _db.NoteVersions
            .Where(v => v.NoteId == id && v.CreatedByUserId == UserId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();

        var result = versions.Select(v =>
        {
            var tagIds = TagIdsFromSnapshot(v.TagIdsSnapshot);
            var tagNames = tagIds.Where(tid => tagNamesMap.ContainsKey(tid)).Select(tid => tagNamesMap[tid]).ToList();
            var isCurrent = string.Equals(v.SnapshotHash, currentHash, StringComparison.Ordinal);
            return new NoteVersionListItemDto(
                v.Id,
                string.IsNullOrEmpty(v.Title) ? "(无标题)" : (v.Title.Length > 40 ? v.Title[..40] + "…" : v.Title),
                Preview(v.Content, 80),
                v.CategoryId,
                v.CategoryId.HasValue && categoryNamesMap.ContainsKey(v.CategoryId.Value) ? categoryNamesMap[v.CategoryId.Value] : null,
                tagNames,
                v.CreatedAt,
                isCurrent,
                v.SnapshotHash);
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{id:int}/versions/{vid:int}")]
    public async Task<ActionResult<NoteVersionDetailDto>> GetVersionDetail(int id, int vid)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == UserId);
        if (note == null) return NotFound();

        var version = await _db.NoteVersions
            .FirstOrDefaultAsync(v => v.Id == vid && v.NoteId == id && v.CreatedByUserId == UserId);
        if (version == null) return NotFound();

        var tagIds = TagIdsFromSnapshot(version.TagIdsSnapshot);
        var tagNames = await _db.Tags
            .Where(t => t.UserId == UserId && tagIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name);
        var catName = version.CategoryId.HasValue
            ? await _db.Categories
                .Where(c => c.Id == version.CategoryId.Value && c.UserId == UserId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync()
            : null;

        return Ok(new NoteVersionDetailDto(
            version.Id,
            version.Title,
            version.Content,
            version.CategoryId,
            catName,
            tagIds,
            tagIds.Where(tagNames.ContainsKey).Select(tid => tagNames[tid]).ToList(),
            version.CreatedAt,
            version.SnapshotHash));
    }

    [HttpPost("{id:int}/versions/{vid:int}/restore")]
    public async Task<IActionResult> RestoreVersion(int id, int vid)
    {
        var note = await _db.Notes
            .Include(n => n.NoteTags)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == UserId);
        if (note == null) return NotFound();

        var version = await _db.NoteVersions
            .FirstOrDefaultAsync(v => v.Id == vid && v.NoteId == id && v.CreatedByUserId == UserId);
        if (version == null) return NotFound();

        // 恢复内容到 Note
        var newTagIds = TagIdsFromSnapshot(version.TagIdsSnapshot);
        note.Title = version.Title;
        note.Content = version.Content;
        note.CategoryId = version.CategoryId;
        note.UpdatedAt = DateTime.UtcNow;

        _db.NoteTags.RemoveRange(note.NoteTags);
        await SyncTags(note, newTagIds ?? new List<int>());

        // 恢复后再生成"新版本"（作为恢复操作的留痕；Hash 若与 latest 不同才插入）
        var sortedTagIds = (newTagIds ?? new List<int>()).OrderBy(x => x).ToList();
        var newHash = ComputeSnapshotHash(note.Title, note.Content, note.CategoryId, sortedTagIds);
        var lastHash = await _db.NoteVersions
            .Where(v => v.NoteId == id && v.CreatedByUserId == UserId)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => v.SnapshotHash)
            .FirstOrDefaultAsync();

        if (lastHash == null || lastHash != newHash)
        {
            _db.NoteVersions.Add(new NoteVersion
            {
                NoteId = id,
                Title = note.Title,
                Content = note.Content,
                CategoryId = note.CategoryId,
                TagIdsSnapshot = TagIdsToSnapshot(sortedTagIds),
                CreatedByUserId = UserId,
                CreatedAt = DateTime.UtcNow,
                SnapshotHash = newHash
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { NoteId = id, RestoredFromVersionId = vid });
    }

    // ===================== 私有工具方法 =====================

    private async Task SyncTags(Note note, List<int> tagIds)
    {
        if (tagIds is null || tagIds.Count == 0) return;

        var validTags = await _db.Tags
            .Where(t => t.UserId == UserId && tagIds.Contains(t.Id))
            .ToListAsync();

        foreach (var tag in validTags)
            note.NoteTags.Add(new NoteTag { Tag = tag });
    }

    /// <summary>根据标题/正文/分类/标签ID列表生成 SHA256 快照哈希，用于去重判断版本是否真的变化了</summary>
    private static string ComputeSnapshotHash(string title, string content, int? categoryId, List<int> tagIds)
    {
        var sb = new StringBuilder();
        sb.Append("T:").Append(title ?? string.Empty).Append('\n');
        sb.Append("C:").Append(content ?? string.Empty).Append('\n');
        sb.Append("G:").Append(categoryId.HasValue ? categoryId.Value.ToString() : "null").Append('\n');
        sb.Append("TIDS:").AppendJoin(',', tagIds ?? new List<int>());
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string TagIdsToSnapshot(List<int> tagIds)
        => tagIds == null || tagIds.Count == 0 ? string.Empty : string.Join(",", tagIds);

    private static List<int> TagIdsFromSnapshot(string? snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot)) return new List<int>();
        var list = new List<int>();
        foreach (var s in snapshot.Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(s, out var id)) list.Add(id);
        return list;
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

    private static string Preview(string content) => Preview(content, 120);

    private static string Preview(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        var text = content.Length > maxLength ? content[..maxLength] + "…" : content;
        return text.Replace("\n", " ").Replace("\r", "");
    }
}
