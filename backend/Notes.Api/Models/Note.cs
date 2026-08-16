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

    public ICollection<NoteTag> NoteTags { get; set; } = new List<NoteTag>();

    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();

    public ICollection<NoteVersion> Versions { get; set; } = new List<NoteVersion>();
}
