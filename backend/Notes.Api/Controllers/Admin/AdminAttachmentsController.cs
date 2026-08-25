using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notes.Api.Data;
using Notes.Api.DTOs.Admin;
using Notes.Api.Services;

namespace Notes.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/attachments")]
[Authorize(Policy = "Admin")]
public class AdminAttachmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _files;

    public AdminAttachmentsController(AppDbContext db, IFileStorageService files)
    {
        _db = db;
        _files = files;
    }

    /// <summary>跨用户附件分页列表，支持文件名/用户/笔记/类型/时间范围筛选</summary>
    [HttpGet]
    public async Task<ActionResult<AdminAttachmentListResponseDto>> List(
        [FromQuery] string? q,
        [FromQuery] string? userId,
        [FromQuery] int? noteId,
        [FromQuery] string? contentType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _db.Attachments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(a => EF.Functions.Collate(a.FileName, "utf8mb4_general_ci").Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(a => a.Note.UserId == userId);

        if (noteId.HasValue)
            query = query.Where(a => a.NoteId == noteId.Value);

        if (!string.IsNullOrWhiteSpace(contentType))
            query = query.Where(a => a.ContentType == contentType);

        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value.Date.AddDays(1).AddTicks(-1));

        var total = await query.CountAsync();

        var attachments = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.FileName,
                a.Size,
                a.ContentType,
                a.NoteId,
                a.CreatedAt,
                a.Note.Title,
                a.Note.UserId
            })
            .ToListAsync();

        var userIds = attachments.Select(a => a.UserId).Distinct().ToList();
        var userMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.Email, u.DisplayName });

        var items = attachments.Select(a => new AdminAttachmentListItemDto(
            a.Id,
            a.FileName,
            a.Size,
            a.ContentType,
            a.NoteId,
            a.Title,
            a.UserId,
            userMap.GetValueOrDefault(a.UserId)?.Email,
            userMap.GetValueOrDefault(a.UserId)?.DisplayName,
            a.CreatedAt)).ToList();

        return Ok(new AdminAttachmentListResponseDto(items, total));
    }

    /// <summary>管理员下载任意附件；物理文件丢失时返回 404</summary>
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var att = await _db.Attachments.FirstOrDefaultAsync(a => a.Id == id);
        if (att == null) return NotFound();

        try
        {
            var (stream, contentType, fileName) = await _files.GetAsync(att.FilePath);
            return File(stream, contentType, fileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { message = "附件物理文件不存在" });
        }
    }

    /// <summary>删除附件：先清理物理文件，再删除数据库记录（硬删除，不可恢复）</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var att = await _db.Attachments.FirstOrDefaultAsync(a => a.Id == id);
        if (att == null) return NotFound();

        _files.Delete(att.FilePath);
        _db.Attachments.Remove(att);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>扫描 Uploads/note_* 目录下的孤立文件（DB 无记录但磁盘仍存在）。排除 Avatars 目录。</summary>
    [HttpGet("orphans")]
    public async Task<ActionResult<AdminOrphanScanResponseDto>> ScanOrphans()
    {
        var (items, count, totalSize) = await ScanOrphanFilesAsync();
        return Ok(new AdminOrphanScanResponseDto(items, count, totalSize));
    }

    /// <summary>
    /// 清理孤立文件。为防误删：删除前重新扫描一次（与前端展示之间存在新上传的窗口期），
    /// 仅删除此刻仍为孤立的文件。
    /// </summary>
    [HttpDelete("orphans")]
    public async Task<ActionResult<AdminOrphanScanResponseDto>> DeleteOrphans()
    {
        var (items, count, totalSize) = await ScanOrphanFilesAsync();
        foreach (var item in items)
        {
            _files.Delete(Path.Combine(_files.GetUploadsRoot(), item.RelativePath));
        }
        return Ok(new AdminOrphanScanResponseDto(items, count, totalSize));
    }

    private async Task<(List<AdminOrphanFileDto> Items, int Count, long TotalSize)> ScanOrphanFilesAsync()
    {
        var uploadsRoot = _files.GetUploadsRoot();

        // DB 中所有附件路径（相对 Uploads 根目录），归一化为相对路径比较
        var dbPaths = await _db.Attachments
            .AsNoTracking()
            .Select(a => a.FilePath)
            .ToListAsync();
        var dbSet = new HashSet<string>(
            dbPaths.Select(p => NormalizeRelative(p, uploadsRoot)),
            StringComparer.OrdinalIgnoreCase);

        var items = new List<AdminOrphanFileDto>();
        long totalSize = 0;

        if (!Directory.Exists(uploadsRoot))
            return (items, 0, 0);

        // 只扫 note_* 目录，排除 Avatars 等
        foreach (var dir in Directory.EnumerateDirectories(uploadsRoot))
        {
            var dirName = Path.GetFileName(dir);
            if (!dirName.StartsWith("note_", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var file in Directory.EnumerateFiles(dir))
            {
                var rel = NormalizeRelative(file, uploadsRoot);
                if (dbSet.Contains(rel))
                    continue;

                var info = new FileInfo(file);
                items.Add(new AdminOrphanFileDto(
                    rel.Replace('\\', '/'),
                    info.Name,
                    info.Length,
                    info.LastWriteTimeUtc));
                totalSize += info.Length;
            }
        }

        items = items.OrderByDescending(i => i.Size).ToList();
        return (items, items.Count, totalSize);
    }

    /// <summary>把绝对路径转成相对 Uploads 根的路径（统一分隔符）</summary>
    private static string NormalizeRelative(string fullPath, string uploadsRoot)
    {
        var root = uploadsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var abs = Path.GetFullPath(fullPath);
        return abs.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? abs[(root.Length + 1)..]
            : abs;
    }
}
