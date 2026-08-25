namespace Notes.Api.DTOs.Admin;

/// <summary>仪表盘概览统计</summary>
public record AdminStatsOverviewDto(
    int UserCount,
    int NoteCount,
    int AttachmentCount,
    long AttachTotalSize,
    int TodayNewNotes);

/// <summary>趋势图单点（按天）</summary>
public record AdminTrendPointDto(string Date, int NewUsers, int NewNotes);

/// <summary>用户管理列表项</summary>
public record AdminUserDto(
    string Id,
    string Email,
    string? DisplayName,
    string? AvatarUrl,
    DateTime CreatedAt,
    bool LockedOut,
    int NoteCount);

/// <summary>用户分页响应</summary>
public record AdminUserListResponseDto(List<AdminUserDto> Items, int Total);

/// <summary>笔记管理列表项</summary>
public record AdminNoteListItemDto(
    int Id,
    string Title,
    string ContentPreview,
    string UserId,
    string? UserEmail,
    string? UserDisplayName,
    int? CategoryId,
    string? CategoryName,
    List<string> Tags,
    int AttachmentCount,
    int VersionCount,
    bool IsPinned,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>笔记分页响应</summary>
public record AdminNoteListResponseDto(List<AdminNoteListItemDto> Items, int Total);

/// <summary>后台单篇笔记详情</summary>
public record AdminNoteDetailDto(
    int Id,
    string Title,
    string Content,
    string UserId,
    string? UserEmail,
    string? UserDisplayName,
    int? CategoryId,
    string? CategoryName,
    List<string> Tags,
    List<AdminAttachmentDto> Attachments,
    bool IsPinned,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>后台附件信息</summary>
public record AdminAttachmentDto(
    int Id,
    string FileName,
    long Size,
    string ContentType,
    DateTime CreatedAt);

/// <summary>分类管理列表项</summary>
public record AdminCategoryDto(
    int Id,
    string Name,
    string UserId,
    string? UserEmail,
    string? UserDisplayName,
    int NoteCount,
    DateTime CreatedAt);

public record AdminCategoryListResponseDto(List<AdminCategoryDto> Items, int Total);

/// <summary>标签管理列表项（Tag 模型无 CreatedAt 字段，故不包含）</summary>
public record AdminTagDto(
    int Id,
    string Name,
    string UserId,
    string? UserEmail,
    string? UserDisplayName,
    int NoteCount);

public record AdminTagListResponseDto(List<AdminTagDto> Items, int Total);

/// <summary>附件管理列表项</summary>
public record AdminAttachmentListItemDto(
    int Id,
    string FileName,
    long Size,
    string ContentType,
    int NoteId,
    string? NoteTitle,
    string UserId,
    string? UserEmail,
    string? UserDisplayName,
    DateTime CreatedAt);

public record AdminAttachmentListResponseDto(List<AdminAttachmentListItemDto> Items, int Total);

/// <summary>孤立文件信息</summary>
public record AdminOrphanFileDto(
    string RelativePath,
    string FileName,
    long Size,
    DateTime LastWriteTimeUtc);

public record AdminOrphanScanResponseDto(List<AdminOrphanFileDto> Items, int Count, long TotalSize);