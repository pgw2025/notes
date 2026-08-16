namespace Notes.Api.DTOs;

public record CategoryDto(int Id, string Name, int NoteCount, DateTime CreatedAt);

public record CreateCategoryDto(string Name);

public record UpdateCategoryDto(string Name);
