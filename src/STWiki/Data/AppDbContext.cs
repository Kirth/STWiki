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
    public DbSet<Draft> Drafts { get; set; } = default!;
    public DbSet<MediaFile> MediaFiles { get; set; } = default!;
    public DbSet<MediaThumbnail> MediaThumbnails { get; set; } = default!;
    public DbSet<PageMediaReference> PageMediaReferences { get; set; } = default!;
    public DbSet<CollabSession> CollabSessions { get; set; } = default!;
    public DbSet<CollabUpdate> CollabUpdates { get; set; } = default!;
    public DbSet<CollabCheckpoint> CollabCheckpoints { get; set; } = default!;

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

        // Draft configuration and indexes
        modelBuilder.Entity<Draft>(entity =>
        {
            // Unique index on user and page combination - one draft per user per page
            entity.HasIndex(e => new { e.UserId, e.PageId })
                .IsUnique()
                .HasDatabaseName("IX_Drafts_UserId_PageId");

            // Index on PageId for finding all drafts for a page
            entity.HasIndex(e => e.PageId)
                .HasDatabaseName("IX_Drafts_PageId");

            // Index on UserId for finding all user's drafts
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_Drafts_UserId");

            // Index on UpdatedAt for cleanup/maintenance
            entity.HasIndex(e => e.UpdatedAt)
                .HasDatabaseName("IX_Drafts_UpdatedAt");

            // Foreign key to Page
            entity.HasOne(d => d.Page)
                .WithMany()
                .HasForeignKey(d => d.PageId)
                .OnDelete(DeleteBehavior.Cascade);

            // Optional foreign key to BaseRevision
            entity.HasOne(d => d.BaseRevision)
                .WithMany()
                .HasForeignKey(d => d.BaseRevisionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // CollabSession configuration and indexes
        modelBuilder.Entity<CollabSession>(entity =>
        {
            entity.HasIndex(e => e.PageId)
                .HasDatabaseName("IX_CollabSessions_PageId");

            entity.HasIndex(e => new { e.PageId, e.ClosedAt })
                .HasDatabaseName("IX_CollabSessions_PageId_ClosedAt");

            entity.Property(e => e.CheckpointBytes)
                .HasColumnType("bytea");

            entity.HasOne(s => s.Page)
                .WithMany()
                .HasForeignKey(s => s.PageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // CollabUpdate configuration and indexes
        modelBuilder.Entity<CollabUpdate>(entity =>
        {
            entity.Property(e => e.Id)
                .UseSerialColumn();

            entity.HasIndex(e => new { e.SessionId, e.Id })
                .HasDatabaseName("IX_CollabUpdates_SessionId_Id");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_CollabUpdates_CreatedAt");

            entity.Property(e => e.UpdateBytes)
                .HasColumnType("bytea");

            entity.HasOne(u => u.Session)
                .WithMany(s => s.Updates)
                .HasForeignKey(u => u.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // CollabCheckpoint configuration and indexes
        modelBuilder.Entity<CollabCheckpoint>(entity =>
        {
            entity.HasIndex(e => new { e.SessionId, e.Version })
                .IsUnique()
                .HasDatabaseName("IX_CollabCheckpoints_SessionId_Version");

            entity.Property(e => e.SnapshotBytes)
                .HasColumnType("bytea");

            entity.HasOne(c => c.Session)
                .WithMany(s => s.Checkpoints)
                .HasForeignKey(c => c.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}