using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Notes.Api.Data;
using Notes.Api.Models;
using Notes.Api.Services;

namespace Notes.Api.Infrastructure;

/// <summary>
/// 全局行为日志过滤器。
/// 通过「Controller + Action → (行为类型, 实体类型)」静态映射表识别需要记录的行为，
/// 在 <see cref="OnActionExecutedAsync"/> 中（仅业务成功时）采集上下文并入队异步落库。
/// 规则：管理后台（api/admin/）只记写操作；普通用户端记全量写操作 + 笔记浏览。
/// </summary>
public sealed class ActivityLogFilter : IAsyncActionFilter
{
    // 需要「实体标题快照」的行为：用于落库前补查标题（如笔记标题）
    private static readonly HashSet<ActivityAction> ActionsNeedTitle = new()
    {
        ActivityAction.NoteUpdate, ActivityAction.NoteDelete, ActivityAction.NoteRestore,
        ActivityAction.NotePin, ActivityAction.NoteUnpin, ActivityAction.NoteView
    };

    // 浏览去重：键 (UserId, EntityId) → 最近记录时间
    private static readonly ConcurrentDictionary<(string, string), DateTime> ViewDedup = new();
    private static readonly TimeSpan ViewDedupWindow = TimeSpan.FromMinutes(10);

    private readonly ActivityLogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public ActivityLogFilter(ActivityLogger logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 先执行动作
        var executed = await next();

        // 仅记录成功的写操作/浏览；失败请求不记（登录失败例外，由 AuthController 通过 Items 触发）
        if (executed.Exception != null) return;

        var action = context.ActionDescriptor as ControllerActionDescriptor;
        if (action == null) return;

        var (actType, entityType, needEntityId) = ResolveAction(action);
        if (actType == null) return; // 不在映射表 → 不记

        // 登录（成功/失败/被拒）：成功与失败都在 AuthController.Login 方法内，
        // 由 AuthController 通过 HttpContext.Items 标记最终结果，这里统一读取。
        if (actType == ActivityAction.Login)
        {
            EnqueueLogin(executed);
            return;
        }

        // 登出：仅成功时记录
        if (actType == ActivityAction.Logout)
        {
            EnqueueAuth(executed, actType.Value);
            return;
        }

        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return;

        // 普通写操作 + 浏览
        string? entityId = null;
        if (actType == ActivityAction.AttachmentUpload)
            entityId = context.RouteData.Values.TryGetValue("noteId", out var noteIdVal) ? noteIdVal?.ToString() : null;
        else if (needEntityId)
            entityId = context.RouteData.Values.TryGetValue("id", out var idVal) ? idVal?.ToString() : null;

        // 浏览去重：同一用户同一笔记 10 分钟内只记一条；且必须确属本人（补查标题时校验）
        if (actType == ActivityAction.NoteView)
        {
            if (string.IsNullOrEmpty(entityId)) return;
            if (!TryMarkView(userId, entityId)) return;
        }

        string? title = null;
        // 管理员操作：目标名称由控制器在操作前写入 Items（删除后无法再查库）
        if (actType == ActivityAction.AdminSetStatus || actType == ActivityAction.AdminResetPassword ||
            actType == ActivityAction.AdminDeleteUser)
        {
            title = context.HttpContext.Items["Activity:TargetUserName"] as string;
        }
        else if (actType == ActivityAction.AdminDeleteNote)
        {
            title = context.HttpContext.Items["Activity:TargetNoteTitle"] as string;
        }
        else if (ActionsNeedTitle.Contains(actType.Value) && !string.IsNullOrEmpty(entityId))
        {
            title = await ResolveTitleAsync(entityId, userId);
            // 浏览且查不到（404/越权）→ 不记，并回滚去重标记
            if (actType == ActivityAction.NoteView && title == null)
            {
                ViewDedup.TryRemove((userId, entityId), out _);
                return;
            }
        }

        var (ip, ua) = CollectClient(executed);

        _logger.Enqueue(new ActivityLog
        {
            UserId = userId,
            UserName = context.HttpContext.User.FindFirstValue("display_name")
                       ?? context.HttpContext.User.Identity?.Name
                       ?? userId,
            Action = actType.Value,
            EntityType = entityType,
            EntityId = entityId,
            EntityTitle = title,
            Ip = ip,
            UserAgent = ua,
            CreatedAt = DateTime.UtcNow
        });
    }

    /// <summary>登录成功 / 登出。</summary>
    private void EnqueueAuth(ActionExecutedContext context, ActivityAction act)
    {
        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return;

        var (ip, ua) = CollectClient(context);
        _logger.Enqueue(new ActivityLog
        {
            UserId = userId,
            UserName = context.HttpContext.User.FindFirstValue("display_name")
                       ?? context.HttpContext.User.Identity?.Name
                       ?? userId,
            Action = act,
            EntityType = ActivityEntity.None,
            Ip = ip,
            UserAgent = ua,
            CreatedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// 登录统一入口：根据 AuthController 写入 HttpContext.Items 的结果
    /// （"Activity:LoginResult" = success / failed / denied）生成对应日志。
    /// </summary>
    private void EnqueueLogin(ActionExecutedContext context)
    {
        var result = context.HttpContext.Items["Activity:LoginResult"] as string;
        var (ip, ua) = CollectClient(context);

        switch (result)
        {
            case "success":
                var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? context.HttpContext.Items["Activity:LoginUserId"] as string;
                if (string.IsNullOrEmpty(userId)) return;
                _logger.Enqueue(new ActivityLog
                {
                    UserId = userId,
                    UserName = context.HttpContext.Items["Activity:LoginUserName"] as string ?? userId,
                    Action = ActivityAction.Login,
                    EntityType = ActivityEntity.None,
                    Ip = ip,
                    UserAgent = ua,
                    CreatedAt = DateTime.UtcNow
                });
                break;

            case "failed":
            case "denied":
                var reason = context.HttpContext.Items["Activity:LoginReason"] as string;
                var attemptAccount = context.HttpContext.Items["Activity:AttemptAccount"] as string;
                var detail = new Dictionary<string, string?> { ["reason"] = reason ?? "" };
                _logger.Enqueue(new ActivityLog
                {
                    UserId = string.Empty,
                    UserName = attemptAccount, // 脱敏：仅存尝试账号，绝不存密码
                    Action = result == "denied" ? ActivityAction.LoginDenied : ActivityAction.LoginFailed,
                    EntityType = ActivityEntity.User,
                    Detail = JsonSerializer.Serialize(detail),
                    Ip = ip,
                    UserAgent = ua,
                    CreatedAt = DateTime.UtcNow
                });
                break;

            default:
                return; // 未标记（如参数校验失败提前返回）不记
        }
    }

    /// <summary>查询笔记标题；带 UserId 校验，查不到（404/越权）返回 null。</summary>
    private async Task<string?> ResolveTitleAsync(string entityId, string userId)
    {
        if (!int.TryParse(entityId, out var noteId)) return null;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var note = await db.Notes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);
        return note?.Title;
    }

    private static bool TryMarkView(string userId, string entityId)
    {
        var key = (userId, entityId);
        var now = DateTime.UtcNow;
        // 已有且未过期 → 去重命中，不记
        if (ViewDedup.TryGetValue(key, out var last) && now - last < ViewDedupWindow)
            return false;
        ViewDedup[key] = now;
        return true;
    }

    private static (string? ip, string? ua) CollectClient(ActionExecutedContext context)
    {
        var req = context.HttpContext.Request;
        var ip = req.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();
        if (string.IsNullOrEmpty(ip))
            ip = context.HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = req.Headers["User-Agent"].FirstOrDefault();
        if (!string.IsNullOrEmpty(ua) && ua.Length > 300) ua = ua[..300];
        return (ip, ua);
    }

    /// <summary>
    /// 路由映射表：Controller 名 + Action 名 → (行为, 实体, 是否取路由 {id})。
    /// 命中 null 表示不记录。
    /// </summary>
    private static (ActivityAction? action, ActivityEntity entity, bool needRouteId) ResolveAction(ControllerActionDescriptor action)
    {
        var c = action.ControllerName;
        var a = action.ActionName;
        var isAdmin = c.StartsWith("Admin", StringComparison.Ordinal);

        // 管理后台：只记写操作（禁用/重置密码/删用户/删笔记），不记浏览
        if (isAdmin)
        {
            return (c, a) switch
            {
                ("AdminUsers", "SetStatus") => (ActivityAction.AdminSetStatus, ActivityEntity.User, true),
                ("AdminUsers", "ResetPassword") => (ActivityAction.AdminResetPassword, ActivityEntity.User, true),
                ("AdminUsers", "Delete") => (ActivityAction.AdminDeleteUser, ActivityEntity.User, true),
                ("AdminNotes", "Delete") => (ActivityAction.AdminDeleteNote, ActivityEntity.Note, true),
                _ => (null, ActivityEntity.None, false)
            };
        }

        // 普通用户端
        return (c, a) switch
        {
            ("Auth", "Login") => (ActivityAction.Login, ActivityEntity.None, false),
            ("Auth", "Logout") => (ActivityAction.Logout, ActivityEntity.None, false),

            ("Notes", "Create") => (ActivityAction.NoteCreate, ActivityEntity.Note, false),
            ("Notes", "Update") => (ActivityAction.NoteUpdate, ActivityEntity.Note, true),
            ("Notes", "Delete") => (ActivityAction.NoteDelete, ActivityEntity.Note, true),
            ("Notes", "RestoreVersion") => (ActivityAction.NoteRestore, ActivityEntity.Note, true),
            ("Notes", "Pin") => (ActivityAction.NotePin, ActivityEntity.Note, true),
            ("Notes", "Unpin") => (ActivityAction.NoteUnpin, ActivityEntity.Note, true),
            ("Notes", "Get") => (ActivityAction.NoteView, ActivityEntity.Note, true),

            ("Attachments", "Upload") => (ActivityAction.AttachmentUpload, ActivityEntity.Attachment, false),
            ("Attachments", "Delete") => (ActivityAction.AttachmentDelete, ActivityEntity.Attachment, true),

            ("Categories", "Create") => (ActivityAction.CategoryCreate, ActivityEntity.Category, false),
            ("Categories", "Update") => (ActivityAction.CategoryUpdate, ActivityEntity.Category, true),
            ("Categories", "Delete") => (ActivityAction.CategoryDelete, ActivityEntity.Category, true),

            ("Tags", "Create") => (ActivityAction.TagCreate, ActivityEntity.Tag, false),
            ("Tags", "Delete") => (ActivityAction.TagDelete, ActivityEntity.Tag, true),

            ("Auth", "UpdateProfile") => (ActivityAction.ProfileUpdate, ActivityEntity.Profile, false),

            _ => (null, ActivityEntity.None, false)
        };
    }
}
