using System.Text.RegularExpressions;

namespace STWiki.Services;

public static class SlugService
{
    public static string GenerateSlug(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        // Convert to lowercase and replace spaces with hyphens
        string slug = title.ToLowerInvariant().Replace(" ", "-");
        
        // Remove any characters that aren't letters, numbers, or hyphens
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");
        
        // Remove duplicate hyphens
        slug = Regex.Replace(slug, @"-+", "-");
        
        // Remove leading/trailing hyphens
        slug = slug.Trim('-');
        
        return slug;
    }

    public static bool IsValidSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return false;

        // Must be lowercase alphanumeric with hyphens only
        return Regex.IsMatch(slug, @"^[a-z0-9-]+$");
    }
}