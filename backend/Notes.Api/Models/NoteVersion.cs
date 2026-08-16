using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Notes.Api.Models;

public class NoteVersion
{
    public int Id { get; set; }

    public int NoteId { get; set; }

    public Note? Note { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public int? CategoryId { get; set; }

    /// <summary>保存时的标签ID列表，用逗号分隔存字符串</summary>
    [MaxLength(500)]
    public string? TagIdsSnapshot { get; set; }

    [Required, MaxLength(255)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public ApplicationUser? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>SHA256(Title+Content+CategoryId+TagIds)，用于去重</summary>
    [Required, MaxLength(64)]
    public string SnapshotHash { get; set; } = string.Empty;
}
