using System.ComponentModel.DataAnnotations;

namespace Notes.Api.Models;

public class Note
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public int? CategoryId { get; set; }

    public Category? Category { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>是否置顶（置顶笔记在列表和时间线中优先显示）</summary>
    public bool IsPinned { get; set; }

    /// <summary>置顶操作的时间（取消置顶后清空）；用此字段让置顶笔记按置顶时间倒序排列</summary>
    public DateTime? PinnedAt { get; set; }

    /// <summary>笔记背景色（HEX 格式如 "#FFF9C4"）。null 表示用用户默认色或系统白色。</summary>
    public string? BackgroundColor { get; set; }

    public ICollection<NoteTag> NoteTags { get; set; } = new List<NoteTag>();

    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();

    public ICollection<NoteVersion> Versions { get; set; } = new List<NoteVersion>();
}
