using System.ComponentModel.DataAnnotations;

namespace Notes.Api.Models;

/// <summary>
/// 用户行为日志（浏览 / 登录 / 操作记录）。
/// 与业务实体无外键关联，用户/笔记删除后日志仍保留（依赖快照列）。
/// </summary>
public class ActivityLog
{
    public long Id { get; set; }

    /// <summary>Identity 用户 Id（登录失败且账号不存在时为空串）</summary>
    [MaxLength(64)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>用户名快照（入队瞬间采集，用户被删后仍可读）</summary>
    [MaxLength(64)]
    public string? UserName { get; set; }

    /// <summary>行为类型</summary>
    public ActivityAction Action { get; set; }

    /// <summary>关联实体类型</summary>
    public ActivityEntity EntityType { get; set; }

    /// <summary>实体 Id（字符串，兼容 Identity 的 string Id）</summary>
    [MaxLength(64)]
    public string? EntityId { get; set; }

    /// <summary>实体名称快照（如笔记标题，删除后仍可读）</summary>
    [MaxLength(256)]
    public string? EntityTitle { get; set; }

    /// <summary>JSON 扩展字段（旧标题→新标题、搜索词、失败原因 reason 等）</summary>
    public string? Detail { get; set; }

    /// <summary>客户端 IP</summary>
    [MaxLength(64)]
    public string? Ip { get; set; }

    /// <summary>User-Agent（截断 300 字符）</summary>
    [MaxLength(300)]
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
