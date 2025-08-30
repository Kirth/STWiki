using System.ComponentModel.DataAnnotations;

namespace STWiki.Data.Entities;

public class Activity
{
    public long Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string ActivityType { get; set; } = "";

    [Required]
    [MaxLength(255)]
    public string UserId { get; set; } = "";

    [Required]
    [MaxLength(255)]
    public string UserDisplayName { get; set; } = "";

    public Guid? PageId { get; set; }

    public virtual Page? Page { get; set; }

    [MaxLength(255)]
    public string PageSlug { get; set; } = "";

    [MaxLength(255)]
    public string PageTitle { get; set; } = "";

    [MaxLength(500)]
    public string Description { get; set; } = "";

    public string? Details { get; set; }

    [MaxLength(45)]
    public string IpAddress { get; set; } = "";

    [MaxLength(500)]
    public string UserAgent { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class ActivityTypes
{
    public const string PageCreated = "page_created";
    public const string PageUpdated = "page_updated";
    public const string PageDeleted = "page_deleted";
    public const string PageViewed = "page_viewed";
    public const string PageRenamed = "page_renamed";
    public const string PageLocked = "page_locked";
    public const string PageUnlocked = "page_unlocked";
    public const string UserLogin = "user_login";
    public const string UserLogout = "user_logout";
    public const string SearchPerformed = "search_performed";
    public const string RedirectCreated = "redirect_created";
    public const string RedirectDeleted = "redirect_deleted";
}