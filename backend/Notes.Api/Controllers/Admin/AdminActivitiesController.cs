using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notes.Api.Data;
using Notes.Api.DTOs.Admin;
using Notes.Api.Models;

namespace Notes.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/activities")]
[Authorize(Policy = "Admin")]
public class AdminActivitiesController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminActivitiesController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>行为日志分页列表，支持按用户/行为类型/实体类型/时间范围/关键词筛选。</summary>
    [HttpGet]
    public async Task<ActionResult<AdminActivityListResponseDto>> List(
        [FromQuery] string? userId,
        [FromQuery] int? action,
        [FromQuery] int? entityType,
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _db.ActivityLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(a => a.UserId == userId);

        if (action.HasValue)
            query = query.Where(a => (int)a.Action == action.Value);

        if (entityType.HasValue)
            query = query.Where(a => (int)a.EntityType == entityType.Value);

        if (start.HasValue)
            query = query.Where(a => a.CreatedAt >= start.Value);

        if (end.HasValue)
            query = query.Where(a => a.CreatedAt <= end.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(a =>
                (a.EntityTitle != null && EF.Functions.Collate(a.EntityTitle, "utf8mb4_general_ci").Contains(term)) ||
                (a.UserName != null && EF.Functions.Collate(a.UserName, "utf8mb4_general_ci").Contains(term)));
        }

        var total = await query.CountAsync();

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = logs.Select(a => new AdminActivityDto(
            a.Id,
            a.UserId,
            a.UserName,
            (int)a.Action,
            (int)a.EntityType,
            a.EntityId,
            a.EntityTitle,
            a.Detail,
            a.Ip,
            a.UserAgent,
            a.CreatedAt)).ToList();

        return Ok(new AdminActivityListResponseDto(items, total));
    }

    /// <summary>行为类型枚举字典，供前端下拉。</summary>
    [HttpGet("actions")]
    public IActionResult Actions()
    {
        var actions = Enum.GetValues<ActivityAction>()
            .Select(a => new { value = (int)a, label = a.ToString() })
            .ToList();
        return Ok(actions);
    }

    /// <summary>手动清空日志（可选，配合保留策略）。</summary>
    [HttpDelete]
    public async Task<IActionResult> Clear([FromQuery] DateTime? before)
    {
        var cutoff = before ?? DateTime.UtcNow;
        var count = await _db.ActivityLogs
            .Where(a => a.CreatedAt < cutoff)
            .ExecuteDeleteAsync();
        return Ok(new { deleted = count });
    }
}
