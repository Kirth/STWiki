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

        // Foreign key relationship
        modelBuilder.Entity<Revision>(entity =>
        {
            entity.HasOne(r => r.Page)
                .WithMany(p => p.Revisions)
                .HasForeignKey(r => r.PageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Unique index on FromSlug for redirects
        modelBuilder.Entity<Redirect>(entity =>
        {
            entity.HasIndex(e => e.FromSlug)
                .IsUnique()
                .HasDatabaseName("IX_Redirects_FromSlug");
        });
    }
}