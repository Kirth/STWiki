using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;

namespace STWiki.Services;

public interface IRedirectService
{
    Task<string?> GetRedirectTargetAsync(string fromSlug);
    Task CreateRedirectAsync(string fromSlug, string toSlug);
    Task DeleteRedirectAsync(string fromSlug);
    Task DeleteRedirectsToSlugAsync(string toSlug);
    Task<bool> RedirectExistsAsync(string fromSlug);
    Task<IEnumerable<Redirect>> GetRedirectsFromSlugAsync(string fromSlug);
    Task<IEnumerable<Redirect>> GetRedirectsToSlugAsync(string toSlug);
    Task CleanupRedirectChainAsync(string slug);
    Task UpdateChildRedirectsAsync(string oldParentSlug, string newParentSlug);
}

public class RedirectService : IRedirectService
{
    private readonly AppDbContext _context;
    private readonly ILogger<RedirectService> _logger;

    public RedirectService(AppDbContext context, ILogger<RedirectService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string?> GetRedirectTargetAsync(string fromSlug)
    {
        if (string.IsNullOrWhiteSpace(fromSlug))
            return null;

        // Normalize slug for comparison
        var normalizedSlug = fromSlug.ToLowerInvariant();
        
        var redirect = await _context.Redirects
            .Where(r => EF.Functions.ILike(r.FromSlug, normalizedSlug))
            .FirstOrDefaultAsync();

        if (redirect == null)
            return null;

        _logger.LogDebug("Found redirect from '{FromSlug}' to '{ToSlug}'", fromSlug, redirect.ToSlug);
        return redirect.ToSlug;
    }

    public async Task CreateRedirectAsync(string fromSlug, string toSlug)
    {
        if (string.IsNullOrWhiteSpace(fromSlug) || string.IsNullOrWhiteSpace(toSlug))
        {
            _logger.LogWarning("Cannot create redirect: fromSlug or toSlug is empty");
            return;
        }

        // Normalize slugs
        fromSlug = fromSlug.ToLowerInvariant();
        toSlug = toSlug.ToLowerInvariant();

        // Don't create redirect to itself
        if (fromSlug.Equals(toSlug, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Skipping redirect creation from '{FromSlug}' to itself", fromSlug);
            return;
        }

        // Check if target page exists
        var targetExists = await _context.Pages
            .AnyAsync(p => EF.Functions.ILike(p.Slug, toSlug));
        
        if (!targetExists)
        {
            _logger.LogWarning("Cannot create redirect from '{FromSlug}' to '{ToSlug}': target page does not exist", fromSlug, toSlug);
            return;
        }

        // Check if redirect already exists
        var existingRedirect = await _context.Redirects
            .Where(r => EF.Functions.ILike(r.FromSlug, fromSlug))
            .FirstOrDefaultAsync();

        if (existingRedirect != null)
        {
            // Update existing redirect
            existingRedirect.ToSlug = toSlug;
            existingRedirect.CreatedAt = DateTimeOffset.UtcNow;
            _logger.LogInformation("Updated existing redirect from '{FromSlug}' to '{ToSlug}'", fromSlug, toSlug);
        }
        else
        {
            // Create new redirect
            var redirect = new Redirect
            {
                FromSlug = fromSlug,
                ToSlug = toSlug,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.Redirects.Add(redirect);
            _logger.LogInformation("Created redirect from '{FromSlug}' to '{ToSlug}'", fromSlug, toSlug);
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteRedirectAsync(string fromSlug)
    {
        if (string.IsNullOrWhiteSpace(fromSlug))
            return;

        var normalizedSlug = fromSlug.ToLowerInvariant();
        var redirect = await _context.Redirects
            .Where(r => EF.Functions.ILike(r.FromSlug, normalizedSlug))
            .FirstOrDefaultAsync();

        if (redirect != null)
        {
            _context.Redirects.Remove(redirect);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Deleted redirect from '{FromSlug}'", fromSlug);
        }
    }

    public async Task DeleteRedirectsToSlugAsync(string toSlug)
    {
        if (string.IsNullOrWhiteSpace(toSlug))
            return;

        var normalizedSlug = toSlug.ToLowerInvariant();
        var redirects = await _context.Redirects
            .Where(r => EF.Functions.ILike(r.ToSlug, normalizedSlug))
            .ToListAsync();

        if (redirects.Any())
        {
            _context.Redirects.RemoveRange(redirects);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Deleted {Count} redirects pointing to '{ToSlug}'", redirects.Count, toSlug);
        }
    }

    public async Task<bool> RedirectExistsAsync(string fromSlug)
    {
        if (string.IsNullOrWhiteSpace(fromSlug))
            return false;

        var normalizedSlug = fromSlug.ToLowerInvariant();
        return await _context.Redirects
            .AnyAsync(r => EF.Functions.ILike(r.FromSlug, normalizedSlug));
    }

    public async Task<IEnumerable<Redirect>> GetRedirectsFromSlugAsync(string fromSlug)
    {
        if (string.IsNullOrWhiteSpace(fromSlug))
            return Enumerable.Empty<Redirect>();

        var normalizedSlug = fromSlug.ToLowerInvariant();
        return await _context.Redirects
            .Where(r => EF.Functions.ILike(r.FromSlug, normalizedSlug))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Redirect>> GetRedirectsToSlugAsync(string toSlug)
    {
        if (string.IsNullOrWhiteSpace(toSlug))
            return Enumerable.Empty<Redirect>();

        var normalizedSlug = toSlug.ToLowerInvariant();
        return await _context.Redirects
            .Where(r => EF.Functions.ILike(r.ToSlug, normalizedSlug))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task CleanupRedirectChainAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return;

        var normalizedSlug = slug.ToLowerInvariant();
        
        // Find redirects that point to this slug
        var redirectsToSlug = await GetRedirectsToSlugAsync(normalizedSlug);
        
        // Check if the target slug itself has redirects pointing elsewhere
        var redirectFromSlug = await GetRedirectTargetAsync(normalizedSlug);
        
        if (redirectFromSlug != null)
        {
            // Update all redirects that point to this slug to point to the final target
            foreach (var redirect in redirectsToSlug)
            {
                var existingRedirect = await _context.Redirects
                    .Where(r => EF.Functions.ILike(r.FromSlug, redirect.FromSlug))
                    .FirstOrDefaultAsync();
                
                if (existingRedirect != null)
                {
                    existingRedirect.ToSlug = redirectFromSlug;
                    existingRedirect.CreatedAt = DateTimeOffset.UtcNow;
                }
            }
            
            await _context.SaveChangesAsync();
            _logger.LogInformation("Cleaned up redirect chain for '{Slug}', updated {Count} redirects to point to '{Target}'", 
                slug, redirectsToSlug.Count(), redirectFromSlug);
        }
    }

    public async Task UpdateChildRedirectsAsync(string oldParentSlug, string newParentSlug)
    {
        if (string.IsNullOrWhiteSpace(oldParentSlug) || string.IsNullOrWhiteSpace(newParentSlug))
            return;

        var oldPrefix = oldParentSlug.ToLowerInvariant() + "/";
        var newPrefix = newParentSlug.ToLowerInvariant() + "/";

        // Find all redirects that point to child pages under the old parent slug
        var childRedirects = await _context.Redirects
            .Where(r => r.ToSlug.StartsWith(oldPrefix))
            .ToListAsync();

        var updatedCount = 0;
        foreach (var redirect in childRedirects)
        {
            // Update the redirect target to use the new parent slug
            var oldChildSlug = redirect.ToSlug;
            var newChildSlug = newPrefix + oldChildSlug.Substring(oldPrefix.Length);
            
            // Verify the new target page exists
            var targetExists = await _context.Pages
                .AnyAsync(p => EF.Functions.ILike(p.Slug, newChildSlug));

            if (targetExists)
            {
                redirect.ToSlug = newChildSlug;
                redirect.CreatedAt = DateTimeOffset.UtcNow;
                updatedCount++;
            }
            else
            {
                _logger.LogWarning("Child redirect target '{NewChildSlug}' does not exist, removing redirect from '{FromSlug}'", 
                    newChildSlug, redirect.FromSlug);
                _context.Redirects.Remove(redirect);
            }
        }

        if (updatedCount > 0 || childRedirects.Count != updatedCount)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated {UpdatedCount} child redirects from '{OldParent}/*' to '{NewParent}/*'", 
                updatedCount, oldParentSlug, newParentSlug);
        }
    }
}