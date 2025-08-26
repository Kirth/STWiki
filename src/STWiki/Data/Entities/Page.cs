using System.ComponentModel.DataAnnotations;

namespace STWiki.Data.Entities;

public class Page
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    public string Slug { get; set; } = "";

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = "";

    [MaxLength(500)]
    public string Summary { get; set; } = "";

    public string Body { get; set; } = "";

    [MaxLength(20)]
    public string BodyFormat { get; set; } = "markdown";

    public bool IsLocked { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(255)]
    public string UpdatedBy { get; set; } = "";

    public virtual ICollection<Revision> Revisions { get; set; } = new List<Revision>();
}