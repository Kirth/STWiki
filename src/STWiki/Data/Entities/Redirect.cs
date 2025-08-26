using System.ComponentModel.DataAnnotations;

namespace STWiki.Data.Entities;

public class Redirect
{
    public long Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string FromSlug { get; set; } = "";

    [Required]
    [MaxLength(255)]
    public string ToSlug { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}