using System.ComponentModel.DataAnnotations;

namespace STWiki.Data.Entities;

public class Revision
{
    public long Id { get; set; }

    public Guid PageId { get; set; }

    public virtual Page Page { get; set; } = default!;

    [Required]
    [MaxLength(255)]
    public string Author { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(500)]
    public string Note { get; set; } = "";

    public string Snapshot { get; set; } = "";

    [MaxLength(20)]
    public string Format { get; set; } = "markdown";

    public byte[]? YjsUpdate { get; set; }
}