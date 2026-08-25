using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notes.Api.Data;
using Notes.Api.DTOs.Admin;

namespace Notes.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/stats")]
[Authorize(Policy = "Admin")]
public class AdminStatsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminStatsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>概览统计：用户数、笔记数、附件数、附件总大小、今日新增笔记</summary>
    [HttpGet("overview")]
    public async Task<ActionResult<AdminStatsOverviewDto>> Overview()
    {
        var today = DateTime.UtcNow.Date;

        // 注意：EF Core 的 DbContext 非线程安全，必须串行查询，不能并发
        var userCount = await _db.Users.CountAsync();
        var noteCount = await _db.Notes.CountAsync();
        var attachmentCount = await _db.Attachments.CountAsync();
        var attachSize = await _db.Attachments.SumAsync(a => (long?)a.Size) ?? 0;
        var todayNew = await _db.Notes.CountAsync(n => n.CreatedAt >= today);

        return Ok(new AdminStatsOverviewDto(
            userCount,
            noteCount,
            attachmentCount,
            attachSize,
            todayNew));
    }

    /// <summary>近 N 天趋势：每天新增用户数与新增笔记数</summary>
    [HttpGet("trends")]
    public async Task<ActionResult<List<AdminTrendPointDto>>> Trends([FromQuery] int days = 30)
    {
        if (days < 1) days = 7;
        if (days > 365) days = 365;

        var from = DateTime.UtcNow.Date.AddDays(-(days - 1));

        // 一次性查区间内数据，再在内存按天聚合（数据量小，避免复杂 SQL）
        var userCounts = await _db.Users
            .Where(u => u.CreatedAt >= from)
            .GroupBy(u => u.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        var noteCounts = await _db.Notes
            .Where(n => n.CreatedAt >= from)
            .GroupBy(n => n.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        var userMap = userCounts.ToDictionary(x => x.Date, x => x.Count);
        var noteMap = noteCounts.ToDictionary(x => x.Date, x => x.Count);

        var result = new List<AdminTrendPointDto>();
        for (var i = 0; i < days; i++)
        {
            var date = from.AddDays(i);
            result.Add(new AdminTrendPointDto(
                date.ToString("yyyy-MM-dd"),
                userMap.GetValueOrDefault(date),
                noteMap.GetValueOrDefault(date)));
        }

        return Ok(result);
    }
}