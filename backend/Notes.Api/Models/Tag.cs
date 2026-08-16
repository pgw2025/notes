using System.ComponentModel.DataAnnotations;

namespace Notes.Api.Models;

public class Tag
{
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string Name { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public ICollection<NoteTag> NoteTags { get; set; } = new List<NoteTag>();
}
