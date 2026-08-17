using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    private readonly UserManager<ApplicationUser> _userManager;

    public NotesController(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>获取用户默认笔记色（用于单篇笔记未指定色时的 fallback）</summary>
    private async Task<string?> GetUserDefaultColorAsync()
    {
        var user = await _userManager.FindByIdAsync(UserId);
        return user?.DefaultNoteColor;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NoteListItemDto>>> List([FromQuery] int? categoryId)
    {
        var userDefaultColor = await GetUserDefaultColorAsync();

        var query = _db.Notes
            .Include(n => n.Category)
            .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
            .Where(n => n.UserId == UserId);

        if (categoryId.HasValue)
            query = query.Where(n => n.CategoryId == categoryId);

        var notes = await query
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.PinnedAt)
            .ThenByDescending(n => n.UpdatedAt)
            .ToListAsync();

        return Ok(notes.Select(n => MapToListItem(n, userDefaultColor)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NoteDto>> Get(int id)
    {
        var userDefaultColor = await GetUserDefaultColorAsync();

        var note = await _db.Notes
            .Include(n => n.Category)
            .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
            .Include(n => n.Attachments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == UserId);

        if (note == null) return NotFound();

        return Ok(MapToDto(note, userDefaultColor));
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<NoteListItemDto>>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Array.Empty<NoteListItemDto>());

        var userDefaultColor = await GetUserDefaultColorAsync();

        var term = q.Trim();
        var notes = await _db.Notes
            .Include(n => n.Category)
            .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
            .Where(n => n.UserId == UserId &&
                (EF.Functions.Collate(n.Title, "utf8mb4_general_ci").Contains(term) ||
                 EF.Functions.Collate(n.Content, "utf8mb4_general_ci").Contains(term)))
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.PinnedAt)
            .ThenByDescending(n => n.UpdatedAt)
            .ToListAsync();

        return Ok(notes.Select(n => MapToListItem(n, userDefaultColor)));
    }

    [HttpGet("timeline")]
    public async Task<ActionResult<TimelineResponseDto>> Timeline([FromQuery] TimelineQueryParams q)
    {
        var userDefaultColor = await GetUserDefaultColorAsync();

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
                                dayGroup
                                    .Select(n => MapToListItem(n, userDefaultColor))
                                    .OrderByDescending(x => x.IsPinned)
                                    .ThenByDescending(x => x.PinnedAt)
                                    .ThenByDescending(x => x.UpdatedAt)
                                    .ToList(),
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
        // 颜色 fallback：dto 指定了就用 dto 的，否则用用户默认色
        var userDefaultColor = await GetUserDefaultColorAsync();
        var backgroundColor = ResolveColor(dto.BackgroundColor, userDefaultColor);
        var isPinned = dto.IsPinned ?? false;

        var note = new Note
        {
            Title = dto.Title,
            Content = dto.Content,
            CategoryId = dto.CategoryId,
            UserId = UserId,
            BackgroundColor = backgroundColor,
            IsPinned = isPinned,
            PinnedAt = isPinned ? DateTime.UtcNow : null,
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
        var snapshot = ComputeSnapshotHash(dto.Title, dto.Content, dto.CategoryId, backgroundColor, freshTags);
        _db.NoteVersions.Add(new NoteVersion
        {
            NoteId = note.Id,
            Title = dto.Title,
            Content = dto.Content,
            CategoryId = dto.CategoryId,
            BackgroundColor = backgroundColor,
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
            .AsSplitQuery()
            .FirstAsync(n => n.Id == note.Id);

        return CreatedAtAction(nameof(Get), new { id = saved.Id }, MapToDto(saved, userDefaultColor));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateNoteDto dto)
    {
        var note = await _db.Notes
            .Include(n => n.NoteTags)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == UserId);
        if (note == null) return NotFound();

        var userDefaultColor = await GetUserDefaultColorAsync();
        // 更新颜色：dto.BackgroundColor 是 null 表示保持不变（前端没改色），空串表示清除为系统默认，非空串表示新色
        // 但 UpdateNoteDto 的 BackgroundColor 我们约定：null = 不变，"" = 清除，"#XXXXXX" = 新色
        // 这里简化为：如果 dto 传了 BackgroundColor 字段（非 null），就更新
        string? newBackgroundColor = note.BackgroundColor;
        if (dto.BackgroundColor != null)
        {
            var trimmed = dto.BackgroundColor.Trim();
            newBackgroundColor = trimmed.Length == 0 ? null : trimmed;
        }
        // 如果当前笔记色为空（用默认色），保存时也存一个 fallback 后的具体色，避免下次默认色变了老笔记跟着变
        // 但其实业务上"老笔记色=null 跟随默认色变化"是想要的——所以这里保持 newBackgroundColor 可以为 null
        // 即：dto 传空串 → 笔记色变 null（跟随默认色）；dto 传具体色 → 笔记色变具体色；dto 不传（null）→ 保持原色
        // 但 dto 是 record，前端不传字段时默认是 null，所以"不传"和"传空串"需要区分。这里前端传 BackgroundColor: '' 表示清除。

        // 计算保存后的快照 Hash，和上一个版本相同则不生成新版本
        var sortedNewTagIds = (dto.TagIds ?? new List<int>()).OrderBy(x => x).ToList();
        var newHash = ComputeSnapshotHash(dto.Title, dto.Content, dto.CategoryId, newBackgroundColor, sortedNewTagIds);

        var lastHash = await _db.NoteVersions
            .Where(v => v.NoteId == id && v.CreatedByUserId == UserId)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => v.SnapshotHash)
            .FirstOrDefaultAsync();

        var contentChanged = lastHash == null || lastHash != newHash;

        note.Title = dto.Title;
        note.Content = dto.Content;
        note.CategoryId = dto.CategoryId;
        note.BackgroundColor = newBackgroundColor;
        note.UpdatedAt = DateTime.UtcNow;

        // 置顶字段：dto.IsPinned 为 null 表示保持不变
        if (dto.IsPinned.HasValue)
        {
            var newIsPinned = dto.IsPinned.Value;
            if (newIsPinned && !note.IsPinned)
            {
                note.IsPinned = true;
                note.PinnedAt = DateTime.UtcNow;
            }
            else if (!newIsPinned && note.IsPinned)
            {
                note.IsPinned = false;
                note.PinnedAt = null;
            }
        }

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
                BackgroundColor = newBackgroundColor,
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
            note.Title, note.Content, note.CategoryId, note.BackgroundColor,
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

        // 恢复内容到 Note（含背景色）
        var newTagIds = TagIdsFromSnapshot(version.TagIdsSnapshot);
        note.Title = version.Title;
        note.Content = version.Content;
        note.CategoryId = version.CategoryId;
        note.BackgroundColor = version.BackgroundColor;
        note.UpdatedAt = DateTime.UtcNow;

        _db.NoteTags.RemoveRange(note.NoteTags);
        await SyncTags(note, newTagIds ?? new List<int>());

        // 恢复后再生成"新版本"（作为恢复操作的留痕；Hash 若与 latest 不同才插入）
        var sortedTagIds = (newTagIds ?? new List<int>()).OrderBy(x => x).ToList();
        var newHash = ComputeSnapshotHash(note.Title, note.Content, note.CategoryId, note.BackgroundColor, sortedTagIds);
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
                BackgroundColor = note.BackgroundColor,
                TagIdsSnapshot = TagIdsToSnapshot(sortedTagIds),
                CreatedByUserId = UserId,
                CreatedAt = DateTime.UtcNow,
                SnapshotHash = newHash
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { NoteId = id, RestoredFromVersionId = vid });
    }

    // ===================== 置顶 / 取消置顶 =====================

    [HttpPost("{id:int}/pin")]
    public async Task<IActionResult> Pin(int id)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == UserId);
        if (note == null) return NotFound();

        if (!note.IsPinned)
        {
            note.IsPinned = true;
            note.PinnedAt = DateTime.UtcNow;
            note.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return Ok(new { note.Id, note.IsPinned, note.PinnedAt });
    }

    [HttpPost("{id:int}/unpin")]
    public async Task<IActionResult> Unpin(int id)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == UserId);
        if (note == null) return NotFound();

        if (note.IsPinned)
        {
            note.IsPinned = false;
            note.PinnedAt = null;
            note.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return Ok(new { note.Id, note.IsPinned, note.PinnedAt });
    }

    // ===================== 私有工具方法 =====================

    /// <summary>
    /// 计算笔记内容快照的 SHA-256 Hash，用于版本去重。
    /// 包含标题、正文、分类、背景色、排序后的标签 ID。
    /// </summary>
    private static string ComputeSnapshotHash(string title, string content, int? categoryId, string? backgroundColor, List<int> sortedTagIds)
    {
        var raw = $"{title}\u0001{content}\u0001{categoryId?.ToString() ?? ""}\u0001{backgroundColor ?? ""}\u0001{string.Join(",", sortedTagIds)}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// 解析笔记背景色：dto 指定了就用 dto 的，否则 fallback 到用户默认色；都为空返回 null（前端用白色）。
    /// </summary>
    private static string? ResolveColor(string? dtoColor, string? userDefaultColor)
    {
        if (!string.IsNullOrWhiteSpace(dtoColor)) return dtoColor.Trim();
        return userDefaultColor; // 可能为 null
    }

    private static NoteListItemDto MapToListItem(Note n, string? userDefaultColor)
    {
        var effectiveColor = !string.IsNullOrWhiteSpace(n.BackgroundColor) ? n.BackgroundColor : userDefaultColor;
        return new NoteListItemDto(
            n.Id,
            n.Title,
            Preview(n.Content, 120),
            n.CategoryId,
            n.Category?.Name,
            n.UpdatedAt,
            n.NoteTags.Select(nt => nt.Tag.Name).OrderBy(x => x).ToList(),
            effectiveColor,
            n.IsPinned,
            n.PinnedAt);
    }

    private static NoteDto MapToDto(Note n, string? userDefaultColor)
    {
        var effectiveColor = !string.IsNullOrWhiteSpace(n.BackgroundColor) ? n.BackgroundColor : userDefaultColor;
        return new NoteDto(
            n.Id,
            n.Title,
            n.Content,
            n.CategoryId,
            n.Category?.Name,
            n.CreatedAt,
            n.UpdatedAt,
            n.NoteTags.Select(nt => nt.Tag.Name).OrderBy(x => x).ToList(),
            n.Attachments.Select(a => new AttachmentDto(a.Id, a.FileName, a.Size, a.ContentType)).ToList(),
            effectiveColor,
            n.IsPinned,
            n.PinnedAt);
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

    private static string Preview(string content) => Preview(content, 120);

    private static string Preview(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        var text = content.Length > maxLength ? content[..maxLength] + "…" : content;
        return text.Replace("\n", " ").Replace("\r", "");
    }
}
