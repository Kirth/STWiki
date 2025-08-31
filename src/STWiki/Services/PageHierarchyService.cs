using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;

namespace STWiki.Services;

public interface IPageHierarchyService
{
    Task<int> PopulateParentIdsFromSlugsAsync();
    Task<Guid?> GetParentIdFromSlugAsync(string slug);
    Task<List<Page>> GetChildPagesAsync(Guid pageId);
    Task UpdateChildrenSlugsAsync(Page parentPage, string oldSlug, string newSlug);
    Task<bool> ValidateHierarchyConsistencyAsync();
}

public class PageHierarchyService : IPageHierarchyService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PageHierarchyService> _logger;

    public PageHierarchyService(AppDbContext context, ILogger<PageHierarchyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Populates ParentId for all existing pages based on their slug paths
    /// </summary>
    public async Task<int> PopulateParentIdsFromSlugsAsync()
    {
        _logger.LogInformation("Starting ParentId population from existing slugs");
        
        var pages = await _context.Pages
            .Where(p => p.ParentId == null) // Only process pages without ParentId set
            .OrderBy(p => p.Slug) // Process in order to handle parents before children
            .ToListAsync();

        int updatedCount = 0;

        foreach (var page in pages)
        {
            var parentId = await GetParentIdFromSlugAsync(page.Slug);
            if (parentId.HasValue)
            {
                page.ParentId = parentId.Value;
                updatedCount++;
                _logger.LogDebug("Set ParentId for page '{Slug}' to '{ParentId}'", page.Slug, parentId);
            }
        }

        if (updatedCount > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated ParentId for {Count} pages", updatedCount);
        }

        return updatedCount;
    }

    /// <summary>
    /// Determines the parent page ID based on a slug path
    /// </summary>
    public async Task<Guid?> GetParentIdFromSlugAsync(string slug)
    {
        if (string.IsNullOrEmpty(slug))
            return null;

        var parts = slug.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
            return null; // Top-level page, no parent

        // Get parent path (all parts except the last one)
        var parentSlug = string.Join("/", parts.Take(parts.Length - 1));

        var parentPage = await _context.Pages
            .Where(p => p.Slug.ToLower() == parentSlug.ToLower())
            .Select(p => new { p.Id })
            .FirstOrDefaultAsync();

        return parentPage?.Id;
    }

    /// <summary>
    /// Gets all direct children of a page
    /// </summary>
    public async Task<List<Page>> GetChildPagesAsync(Guid pageId)
    {
        return await _context.Pages
            .Where(p => p.ParentId == pageId)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    /// <summary>
    /// Updates slugs for all descendant pages when a parent is moved
    /// </summary>
    public async Task UpdateChildrenSlugsAsync(Page parentPage, string oldSlug, string newSlug)
    {
        _logger.LogInformation("Updating children slugs for moved page: '{OldSlug}' -> '{NewSlug}'", oldSlug, newSlug);

        // Get all descendant pages that start with the old slug path
        var oldPrefix = oldSlug + "/";
        var descendants = await _context.Pages
            .Where(p => p.Slug.StartsWith(oldPrefix))
            .ToListAsync();

        foreach (var descendant in descendants)
        {
            var oldDescendantSlug = descendant.Slug;
            
            // Replace the old parent path with the new parent path
            var newDescendantSlug = newSlug + descendant.Slug.Substring(oldSlug.Length);
            
            descendant.Slug = newDescendantSlug;
            descendant.UpdatedAt = DateTimeOffset.UtcNow;
            descendant.UpdatedBy = parentPage.UpdatedBy;

            _logger.LogDebug("Updated descendant slug: '{OldSlug}' -> '{NewSlug}'", oldDescendantSlug, newDescendantSlug);
        }

        if (descendants.Any())
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated {Count} descendant page slugs", descendants.Count);
        }
    }

    /// <summary>
    /// Validates that ParentId relationships match slug hierarchy
    /// </summary>
    public async Task<bool> ValidateHierarchyConsistencyAsync()
    {
        _logger.LogInformation("Validating hierarchy consistency");

        var pages = await _context.Pages
            .Include(p => p.Parent)
            .ToListAsync();

        bool isConsistent = true;
        int inconsistencies = 0;

        foreach (var page in pages)
        {
            var expectedParentId = await GetParentIdFromSlugAsync(page.Slug);
            
            if (page.ParentId != expectedParentId)
            {
                _logger.LogWarning("Inconsistency detected for page '{Slug}': ParentId={ActualParentId}, Expected={ExpectedParentId}", 
                    page.Slug, page.ParentId, expectedParentId);
                isConsistent = false;
                inconsistencies++;
            }
        }

        if (isConsistent)
        {
            _logger.LogInformation("Hierarchy is consistent");
        }
        else
        {
            _logger.LogWarning("Found {Count} hierarchy inconsistencies", inconsistencies);
        }

        return isConsistent;
    }
}