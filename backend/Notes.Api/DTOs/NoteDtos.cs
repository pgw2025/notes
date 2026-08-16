namespace Notes.Api.DTOs;

public record AttachmentDto(int Id, string FileName, long Size, string ContentType);

public record NoteDto(
    int Id,
    string Title,
    string Content,
    int? CategoryId,
    string? CategoryName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<string> Tags,
    List<AttachmentDto> Attachments,
    string? BackgroundColor);

public record NoteListItemDto(
    int Id,
    string Title,
    string ContentPreview,
    int? CategoryId,
    string? CategoryName,
    DateTime UpdatedAt,
    List<string> Tags,
    string? BackgroundColor);

public record CreateNoteDto(string Title, string Content, int? CategoryId, List<int> TagIds, string? BackgroundColor);

public record UpdateNoteDto(string Title, string Content, int? CategoryId, List<int> TagIds, string? BackgroundColor);
