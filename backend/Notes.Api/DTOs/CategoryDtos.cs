namespace Notes.Api.DTOs;

/// <summary>树形分类节点。NoteCount 为「含子孙分类」的递归汇总计数。</summary>
public record CategoryDto(
    int Id,
    string Name,
    int NoteCount,
    DateTime CreatedAt,
    int? ParentId,
    List<CategoryDto> Children);

public record CreateCategoryDto(string Name, int? ParentId);

public record UpdateCategoryDto(string Name, int? ParentId);
