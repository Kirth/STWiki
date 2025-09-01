using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.Encodings.Web;
using STWiki.Data.Entities;

namespace STWiki.Helpers;

public static class UserLinkHelper
{
    /// <summary>
    /// Generates an HTML link to a user's profile page
    /// </summary>
    /// <param name="user">The user entity</param>
    /// <param name="displayText">Optional custom display text (defaults to user's display name)</param>
    /// <param name="cssClass">Optional CSS classes for the link</param>
    /// <returns>HTML link element</returns>
    public static IHtmlContent UserProfileLink(User? user, string? displayText = null, string? cssClass = null)
    {
        if (user == null)
        {
            return new HtmlString("<span class=\"text-muted\">Unknown User</span>");
        }

        var slug = GetUserSlug(user);
        var text = displayText ?? user.DisplayName ?? "User";
        var classes = cssClass ?? "text-decoration-none";
        
        return new HtmlString($"<a href=\"/user/{Uri.EscapeDataString(slug)}\" class=\"{classes}\">{HtmlEncoder.Default.Encode(text)}</a>");
    }

    /// <summary>
    /// Generates an HTML link to a user's profile page from activity data
    /// </summary>
    /// <param name="userId">The user ID (could be sub claim or legacy)</param>
    /// <param name="userDisplayName">The user's display name</param>
    /// <param name="cssClass">Optional CSS classes for the link</param>
    /// <returns>HTML link element</returns>
    public static IHtmlContent UserProfileLinkFromActivity(string? userId, string? userDisplayName, string? cssClass = null)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userDisplayName))
        {
            return new HtmlString("<span class=\"text-muted\">Unknown User</span>");
        }

        var classes = cssClass ?? "text-decoration-none";
        
        // For activity data, we'll use the display name as the slug if it looks user-friendly,
        // otherwise fall back to the userId
        var slug = IsUserFriendlyIdentifier(userDisplayName) ? userDisplayName : userId;
        
        return new HtmlString($"<a href=\"/user/{Uri.EscapeDataString(slug)}\" class=\"{classes}\">{HtmlEncoder.Default.Encode(userDisplayName)}</a>");
    }

    /// <summary>
    /// Gets the preferred URL slug for a user
    /// </summary>
    /// <param name="user">The user entity</param>
    /// <returns>URL-friendly slug</returns>
    public static string GetUserSlug(User user)
    {
        if (!string.IsNullOrEmpty(user.PreferredUsername))
            return user.PreferredUsername;
        if (!string.IsNullOrEmpty(user.DisplayName))
            return user.DisplayName;
        return user.UserId;
    }

    /// <summary>
    /// Checks if an identifier looks user-friendly (not a technical ID)
    /// </summary>
    /// <param name="identifier">The identifier to check</param>
    /// <returns>True if it looks user-friendly</returns>
    private static bool IsUserFriendlyIdentifier(string? identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return false;
            
        // Check if it's a long base64-like string (sub claim)
        if (identifier.Length > 40 && !identifier.Contains(" "))
            return false;
        
        // Check if it's an email
        if (identifier.Contains("@"))
            return false;
        
        // Assume it's user-friendly if it's short and contains spaces or looks like a name
        return identifier.Length <= 30;
    }
}

// Extension method to use in Razor views
public static class HtmlHelperExtensions
{
    public static IHtmlContent UserLink(this IHtmlHelper htmlHelper, User? user, string? displayText = null, string? cssClass = null)
    {
        return UserLinkHelper.UserProfileLink(user, displayText, cssClass);
    }

    public static IHtmlContent UserLink(this IHtmlHelper htmlHelper, string? userId, string? userDisplayName, string? cssClass = null)
    {
        return UserLinkHelper.UserProfileLinkFromActivity(userId, userDisplayName, cssClass);
    }
}