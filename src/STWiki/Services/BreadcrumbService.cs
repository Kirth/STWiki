using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;

namespace STWiki.Services;

public class BreadcrumbService
{
    private readonly AppDbContext _context;
    private readonly ILogger<BreadcrumbService> _logger;

    public BreadcrumbService(AppDbContext context, ILogger<BreadcrumbService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<BreadcrumbItem>> BuildBreadcrumbsAsync(string slug, BreadcrumbOptions? options = null)
    {
        options ??= new BreadcrumbOptions();
        var breadcrumbs = new List<BreadcrumbItem>();

        try
        {
            // Always add home page if enabled
            if (options.IncludeHome)
            {
                breadcrumbs.Add(new BreadcrumbItem
                {
                    Text = options.HomeText,
                    Slug = options.HomeSlug,
                    Icon = options.HomeIcon,
                    IsHome = true
                });
            }

            // Handle special pages
            if (IsSpecialPage(slug))
            {
                var specialBreadcrumbs = await BuildSpecialPageBreadcrumbsAsync(slug, options);
                breadcrumbs.AddRange(specialBreadcrumbs);
                return breadcrumbs;
            }

            // Build hierarchical breadcrumbs for wiki pages
            var hierarchicalBreadcrumbs = await BuildHierarchicalBreadcrumbsAsync(slug, options);
            breadcrumbs.AddRange(hierarchicalBreadcrumbs);

            // Add category-based breadcrumbs if page has categories
            if (options.IncludeCategories)
            {
                var categoryBreadcrumbs = await BuildCategoryBreadcrumbsAsync(slug, options);
                if (categoryBreadcrumbs.Any())
                {
                    // Insert category breadcrumbs after home but before page hierarchy
                    var insertIndex = options.IncludeHome ? 1 : 0;
                    breadcrumbs.InsertRange(insertIndex, categoryBreadcrumbs);
                }
            }

            return breadcrumbs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building breadcrumbs for slug: {Slug}", slug);
            return GetFallbackBreadcrumbs(slug, options);
        }
    }

    private async Task<List<BreadcrumbItem>> BuildHierarchicalBreadcrumbsAsync(string slug, BreadcrumbOptions options)
    {
        var breadcrumbs = new List<BreadcrumbItem>();
        var parts = slug.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length <= 1 && !options.ShowSingleLevel)
        {
            return breadcrumbs; // No breadcrumbs for top-level pages unless forced
        }

        var currentPath = "";
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0) currentPath += "/";
            currentPath += parts[i];

            var page = await _context.Pages
                .Where(p => p.Slug.ToLower() == currentPath.ToLower())
                .Select(p => new { p.Title, p.Summary })
                .FirstOrDefaultAsync();

            var isLast = i == parts.Length - 1;
            
            breadcrumbs.Add(new BreadcrumbItem
            {
                Text = page?.Title ?? FormatSlugAsBreadcrumb(parts[i]),
                Slug = currentPath,
                IsActive = isLast,
                Description = page?.Summary,
                Level = i + 1
            });
        }

        return breadcrumbs;
    }

    private Task<List<BreadcrumbItem>> BuildSpecialPageBreadcrumbsAsync(string slug, BreadcrumbOptions options)
    {
        var breadcrumbs = new List<BreadcrumbItem>();

        var specialPages = new Dictionary<string, BreadcrumbItem>
        {
            ["recent-changes"] = new BreadcrumbItem
            {
                Text = "Recent Changes",
                Slug = "recent-changes",
                Icon = "bi bi-clock-history",
                IsActive = true
            },
            ["search"] = new BreadcrumbItem
            {
                Text = "Search",
                Slug = "search",
                Icon = "bi bi-search",
                IsActive = true
            }
        };

        // Handle user pages
        if (slug.StartsWith("user/"))
        {
            var userParts = slug.Split('/');
            if (userParts.Length >= 2)
            {
                var username = userParts[1];
                breadcrumbs.Add(new BreadcrumbItem
                {
                    Text = "Users",
                    Slug = "users", // This could be a user directory page
                    Icon = "bi bi-people"
                });

                breadcrumbs.Add(new BreadcrumbItem
                {
                    Text = username,
                    Slug = $"user/{username}",
                    Icon = "bi bi-person",
                    IsActive = userParts.Length == 2
                });

                if (userParts.Length > 2)
                {
                    var subPage = userParts[2];
                    breadcrumbs.Add(new BreadcrumbItem
                    {
                        Text = FormatSlugAsBreadcrumb(subPage),
                        Slug = slug,
                        IsActive = true
                    });
                }
            }
        }
        else if (specialPages.TryGetValue(slug, out var specialBreadcrumb))
        {
            breadcrumbs.Add(specialBreadcrumb);
        }

        return Task.FromResult(breadcrumbs);
    }

    private Task<List<BreadcrumbItem>> BuildCategoryBreadcrumbsAsync(string slug, BreadcrumbOptions options)
    {
        var breadcrumbs = new List<BreadcrumbItem>();
        
        // TODO: When category system is implemented, build category-based breadcrumbs
        // For now, return empty list
        
        return Task.FromResult(breadcrumbs);
    }

    private bool IsSpecialPage(string slug)
    {
        var specialPages = new[]
        {
            "recent-changes",
            "search",
            "user/"
        };

        return specialPages.Any(sp => slug.Equals(sp, StringComparison.OrdinalIgnoreCase) || 
                                     slug.StartsWith(sp, StringComparison.OrdinalIgnoreCase));
    }

    private string FormatSlugAsBreadcrumb(string slugPart)
    {
        return slugPart.Replace("-", " ")
                      .Replace("_", " ")
                      .Split(' ')
                      .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower())
                      .Aggregate((a, b) => a + " " + b);
    }

    private List<BreadcrumbItem> GetFallbackBreadcrumbs(string slug, BreadcrumbOptions options)
    {
        var breadcrumbs = new List<BreadcrumbItem>();

        if (options.IncludeHome)
        {
            breadcrumbs.Add(new BreadcrumbItem
            {
                Text = options.HomeText,
                Slug = options.HomeSlug,
                Icon = options.HomeIcon,
                IsHome = true
            });
        }

        // Add current page as fallback
        breadcrumbs.Add(new BreadcrumbItem
        {
            Text = FormatSlugAsBreadcrumb(slug.Split('/').LastOrDefault() ?? slug),
            Slug = slug,
            IsActive = true,
            IsFallback = true
        });

        return breadcrumbs;
    }

    public async Task<List<BreadcrumbItem>> GetSiblingPagesAsync(string slug, int maxSiblings = 10)
    {
        try
        {
            var parts = slug.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1) return new List<BreadcrumbItem>();

            // Get parent path
            var parentPath = string.Join("/", parts.Take(parts.Length - 1));
            var parentPrefix = parentPath + "/";

            // Find sibling pages (pages that start with parent path)
            var siblings = await _context.Pages
                .Where(p => p.Slug.StartsWith(parentPrefix) && 
                           p.Slug != slug &&
                           !p.Slug.Substring(parentPrefix.Length).Contains('/')) // Direct children only
                .OrderBy(p => p.Title)
                .Take(maxSiblings)
                .Select(p => new BreadcrumbItem
                {
                    Text = p.Title,
                    Slug = p.Slug,
                    Description = p.Summary
                })
                .ToListAsync();

            return siblings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sibling pages for slug: {Slug}", slug);
            return new List<BreadcrumbItem>();
        }
    }
}

public class BreadcrumbItem
{
    public string Text { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsHome { get; set; }
    public bool IsFallback { get; set; }
    public int Level { get; set; }
}

public class BreadcrumbOptions
{
    public bool IncludeHome { get; set; } = true;
    public bool IncludeCategories { get; set; } = false;
    public bool ShowSingleLevel { get; set; } = false;
    public string HomeText { get; set; } = "Home";
    public string HomeSlug { get; set; } = "main-page";
    public string HomeIcon { get; set; } = "bi bi-house";
    public int MaxDepth { get; set; } = 10;
}