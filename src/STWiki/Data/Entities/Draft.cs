using System.ComponentModel.DataAnnotations;

namespace STWiki.Data.Entities;

public class Draft
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign key to the page
    public Guid PageId { get; set; }
    public virtual Page Page { get; set; } = null!;

    // User who created this draft
    [Required]
    [MaxLength(255)]
    public string UserId { get; set; } = "";

    // Draft content
    public string Content { get; set; } = "";

    // When this draft was created
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // When this draft was last updated
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Optional: Track what the original content was when this draft was started
    public string? BaseContent { get; set; }

    // Optional: Track the revision this draft is based on
    public long? BaseRevisionId { get; set; }
    public virtual Revision? BaseRevision { get; set; }
}