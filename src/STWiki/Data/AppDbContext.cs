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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique index on lowercase slug
        modelBuilder.Entity<Page>(entity =>
        {
            entity.HasIndex(e => e.Slug)
                .IsUnique()
                .HasDatabaseName("IX_Pages_Slug_Lower")
                .HasMethod("btree")
                .HasFilter(null);
            
            entity.Property(e => e.Slug)
                .UseCollation("C");
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

        // Activity configuration and indexes
        modelBuilder.Entity<Activity>(entity =>
        {
            entity.HasOne(a => a.Page)
                .WithMany()
                .HasForeignKey(a => a.PageId)
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
        });
    }
}