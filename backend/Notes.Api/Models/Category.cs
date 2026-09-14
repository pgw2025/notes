using System.ComponentModel.DataAnnotations;

namespace Notes.Api.Models;

public class Category
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>父分类 Id，null 表示顶级分类（自引用树，支持无限层级）</summary>
    public int? ParentId { get; set; }

    public Category? Parent { get; set; }

    public ICollection<Category> Children { get; set; } = new List<Category>();

    public ICollection<Note> Notes { get; set; } = new List<Note>();
}
