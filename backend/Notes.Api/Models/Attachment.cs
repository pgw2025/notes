using System.ComponentModel.DataAnnotations;

namespace Notes.Api.Models;

public class Attachment
{
    public int Id { get; set; }

    public int NoteId { get; set; }

    public Note Note { get; set; } = null!;

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long Size { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
