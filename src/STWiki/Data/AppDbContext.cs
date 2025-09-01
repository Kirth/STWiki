using Microsoft.EntityFrameworkCore;
using STWiki.Data.Entities;

namespace STWiki.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Page> Pages { get; set; } = default!;
    public DbSet<Revision> Revisions { get; set; } = default!;
    public DbSet<Redirect> Redirects { get; set; } = default!;
    public DbSet<Activity> Activities { get; set; } = default!;
    public DbSet<User> Users { get; set; } = default!;
    public DbSet<MediaFile> MediaFiles { get; set; } = default!;
    public DbSet<MediaThumbnail> MediaThumbnails { get; set; } = default!;
    public DbSet<PageMediaReference> PageMediaReferences { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique index on lowercase slug and parent-child relationships
        modelBuilder.Entity<Page>(entity =>
        {
            entity.HasIndex(e => e.Slug)
                .IsUnique()
                .HasDatabaseName("IX_Pages_Slug_Lower")
                .HasMethod("btree")
                .HasFilter(null);
            
            entity.Property(e => e.Slug)
                .UseCollation("C");
                
            // Configure parent-child relationship
            entity.HasOne(p => p.Parent)
                .WithMany(p => p.Children)
                .HasForeignKey(p => p.ParentId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascade deletion
                
            // Index on ParentId for efficient child queries
            entity.HasIndex(e => e.ParentId)
                .HasDatabaseName("IX_Pages_ParentId");
        });

        // Foreign key relationship and performance indexes
        modelBuilder.Entity<Revision>(entity =>
        {
            entity.HasOne(r => r.Page)
                .WithMany(p => p.Revisions)
                .HasForeignKey(r => r.PageId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Composite index for efficient revision history pagination
            entity.HasIndex(e => new { e.PageId, e.CreatedAt })
                .HasDatabaseName("IX_Revisions_PageId_CreatedAt");
                
            // Index on CreatedAt for chronological queries
            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_Revisions_CreatedAt");
        });

        // Unique index on FromSlug for redirects
        modelBuilder.Entity<Redirect>(entity =>
        {
            entity.HasIndex(e => e.FromSlug)
                .IsUnique()
                .HasDatabaseName("IX_Redirects_FromSlug");
        });

        // User configuration and indexes
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.UserId)
                .IsUnique()
                .HasDatabaseName("IX_Users_UserId");

            // Non-unique index on email for performance (email might not be unique across auth providers)
            entity.HasIndex(e => e.Email)
                .HasDatabaseName("IX_Users_Email");
        });

        // Activity configuration and indexes
        modelBuilder.Entity<Activity>(entity =>
        {
            entity.HasOne(a => a.Page)
                .WithMany()
                .HasForeignKey(a => a.PageId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.User)
                .WithMany(u => u.Activities)
                .HasPrincipalKey(u => u.UserId)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Composite index for activity feed queries
            entity.HasIndex(e => new { e.CreatedAt, e.ActivityType })
                .HasDatabaseName("IX_Activities_CreatedAt_Type");

            // Index for user activity queries
            entity.HasIndex(e => new { e.UserId, e.CreatedAt })
                .HasDatabaseName("IX_Activities_UserId_CreatedAt");

            // Index for page activity queries
            entity.HasIndex(e => new { e.PageId, e.CreatedAt })
                .HasDatabaseName("IX_Activities_PageId_CreatedAt");

            // Index for UserEntityId queries
            entity.HasIndex(e => e.UserEntityId)
                .HasDatabaseName("IX_Activities_UserEntityId");
        });

        // MediaFile configuration and indexes
        modelBuilder.Entity<MediaFile>(entity =>
        {
            entity.HasIndex(e => e.OriginalFileName)
                .HasDatabaseName("IX_MediaFiles_OriginalFileName");
                
            entity.HasIndex(e => e.UploadedByUserId)
                .HasDatabaseName("IX_MediaFiles_UploadedByUserId");
                
            entity.HasIndex(e => e.UploadedAt)
                .HasDatabaseName("IX_MediaFiles_UploadedAt");
                
            entity.HasIndex(e => new { e.IsDeleted, e.IsPublic })
                .HasDatabaseName("IX_MediaFiles_IsDeleted_IsPublic");
                
            entity.HasOne(m => m.UploadedBy)
                .WithMany()
                .HasForeignKey(m => m.UploadedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // MediaThumbnail configuration and indexes
        modelBuilder.Entity<MediaThumbnail>(entity =>
        {
            entity.HasIndex(e => new { e.MediaFileId, e.Width })
                .HasDatabaseName("IX_MediaThumbnails_MediaFileId_Width");

            entity.HasOne(t => t.MediaFile)
                .WithMany(m => m.Thumbnails)
                .HasForeignKey(t => t.MediaFileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PageMediaReference configuration and indexes
        modelBuilder.Entity<PageMediaReference>(entity =>
        {
            entity.HasIndex(e => e.PageId)
                .HasDatabaseName("IX_PageMediaReferences_PageId");
                
            entity.HasIndex(e => e.MediaFileId)
                .HasDatabaseName("IX_PageMediaReferences_MediaFileId");

            entity.HasOne(r => r.Page)
                .WithMany()
                .HasForeignKey(r => r.PageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.MediaFile)
                .WithMany(m => m.PageReferences)
                .HasForeignKey(r => r.MediaFileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}