using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Notes.Api.Models;

namespace Notes.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<NoteTag> NoteTags => Set<NoteTag>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<NoteVersion> NoteVersions => Set<NoteVersion>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<NoteTag>(b =>
        {
            b.HasKey(nt => new { nt.NoteId, nt.TagId });

            b.HasOne(nt => nt.Note)
                .WithMany(n => n.NoteTags)
                .HasForeignKey(nt => nt.NoteId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(nt => nt.Tag)
                .WithMany(t => t.NoteTags)
                .HasForeignKey(nt => nt.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Note>(b =>
        {
            b.HasOne(n => n.Category)
                .WithMany(c => c.Notes)
                .HasForeignKey(n => n.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasIndex(n => n.UserId);

            b.Property(n => n.Content).HasColumnType("longtext");
        });

        builder.Entity<Attachment>(b =>
        {
            b.HasOne(a => a.Note)
                .WithMany(n => n.Attachments)
                .HasForeignKey(a => a.NoteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Category>(b =>
        {
            b.HasIndex(c => c.UserId);

            // 自引用：父分类 + 子分类集合
            b.HasOne(c => c.Parent)
                .WithMany(c => c.Children)
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Tag>().HasIndex(t => t.UserId);

        // 行为日志：不设外键（用户删除后日志保留）；按用户/时间、时间、行为类型建索引
        builder.Entity<ActivityLog>(b =>
        {
            b.HasIndex(a => new { a.UserId, a.CreatedAt });
            b.HasIndex(a => a.CreatedAt);
            b.HasIndex(a => a.Action);
        });
    }
}
