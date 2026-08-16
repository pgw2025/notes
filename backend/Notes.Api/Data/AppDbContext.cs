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

        builder.Entity<Category>().HasIndex(c => c.UserId);
        builder.Entity<Tag>().HasIndex(t => t.UserId);
    }
}
