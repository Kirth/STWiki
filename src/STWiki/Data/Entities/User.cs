using System.ComponentModel.DataAnnotations;

namespace STWiki.Data.Entities;

public class User
{
    public long Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string UserId { get; set; } = "";

    [MaxLength(255)]
    public string Email { get; set; } = "";

    [MaxLength(255)]
    public string DisplayName { get; set; } = "";

    [MaxLength(255)]
    public string PreferredUsername { get; set; } = "";

    [MaxLength(1000)]
    public string Bio { get; set; } = "";

    [MaxLength(500)]
    public string AvatarUrl { get; set; } = "";

    public bool IsProfilePublic { get; set; } = true;

    public bool ShowActivityPublic { get; set; } = true;

    public bool ShowContributionsPublic { get; set; } = true;

    [MaxLength(50)]
    public string ThemePreference { get; set; } = "auto";

    public bool EmailNotifications { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastLoginAt { get; set; }

    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
}