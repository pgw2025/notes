namespace Notes.Api.DTOs;

public record TagDto(int Id, string Name, int NoteCount);

public record CreateTagDto(string Name);
