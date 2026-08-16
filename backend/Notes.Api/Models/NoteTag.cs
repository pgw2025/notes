namespace Notes.Api.Models;

public class NoteTag
{
    public int NoteId { get; set; }

    public Note Note { get; set; } = null!;

    public int TagId { get; set; }

    public Tag Tag { get; set; } = null!;
}
