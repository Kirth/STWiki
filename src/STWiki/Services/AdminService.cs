using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;

namespace STWiki.Services;

public class AdminService
{
    private readonly AppDbContext _context;
    private readonly ILogger<AdminService> _logger;

    public AdminService(AppDbContext context, ILogger<AdminService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Dictionary<string, object>> GetSystemStatisticsAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);
        var sevenDaysAgo = now.AddDays(-7);
        var oneDayAgo = now.AddDays(-1);

        var stats = new Dictionary<string, object>();

        // Total counts
        stats["TotalPages"] = await _context.Pages.CountAsync();
        stats["TotalUsers"] = await _context.Users.CountAsync();
        stats["TotalActivities"] = await _context.Activities.CountAsync();
        stats["TotalRevisions"] = await _context.Revisions.CountAsync();
        stats["TotalRedirects"] = await _context.Redirects.CountAsync();

        // Recent activity
        stats["ActivitiesLast30Days"] = await _context.Activities
            .CountAsync(a => a.CreatedAt >= thirtyDaysAgo);
        stats["ActivitiesLast7Days"] = await _context.Activities
            .CountAsync(a => a.CreatedAt >= sevenDaysAgo);
        stats["ActivitiesLast24Hours"] = await _context.Activities
            .CountAsync(a => a.CreatedAt >= oneDayAgo);

        // Page statistics
        stats["PagesCreatedLast30Days"] = await _context.Activities
            .CountAsync(a => a.ActivityType == ActivityTypes.PageCreated && a.CreatedAt >= thirtyDaysAgo);
        stats["PagesUpdatedLast30Days"] = await _context.Activities
            .CountAsync(a => a.ActivityType == ActivityTypes.PageUpdated && a.CreatedAt >= thirtyDaysAgo);

        // User statistics
        stats["NewUsersLast30Days"] = await _context.Users
            .CountAsync(u => u.CreatedAt >= thirtyDaysAgo);
        stats["ActiveUsersLast7Days"] = await _context.Users
            .CountAsync(u => u.LastLoginAt >= sevenDaysAgo);

        // Most active users (last 30 days) - join with Users to get current display names
        var topUsers = await _context.Activities
            .Where(a => a.CreatedAt >= thirtyDaysAgo)
            .Include(a => a.User)
            .Where(a => a.User != null)
            .GroupBy(a => a.User!.Id)
            .Select(g => new { 
                UserId = g.First().User!.UserId, 
                DisplayName = g.First().User!.DisplayName, 
                ActivityCount = g.Count() 
            })
            .OrderByDescending(x => x.ActivityCount)
            .Take(5)
            .ToListAsync();

        stats["TopActiveUsers"] = topUsers;

        // Most edited pages (last 30 days)
        var topPages = await _context.Activities
            .Where(a => a.CreatedAt >= thirtyDaysAgo && 
                       a.PageId.HasValue && 
                       (a.ActivityType == ActivityTypes.PageCreated || a.ActivityType == ActivityTypes.PageUpdated))
            .GroupBy(a => new { a.PageId, a.PageTitle, a.PageSlug })
            .Select(g => new { 
                PageId = g.Key.PageId, 
                Title = g.Key.PageTitle, 
                Slug = g.Key.PageSlug, 
                EditCount = g.Count() 
            })
            .OrderByDescending(x => x.EditCount)
            .Take(5)
            .ToListAsync();

        stats["TopEditedPages"] = topPages;

        // Activity breakdown by type (last 30 days)
        var activityBreakdown = await _context.Activities
            .Where(a => a.CreatedAt >= thirtyDaysAgo)
            .GroupBy(a => a.ActivityType)
            .Select(g => new { ActivityType = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        stats["ActivityBreakdown"] = activityBreakdown;

        return stats;
    }

    public async Task<List<User>> GetAllUsersAsync(int page = 1, int pageSize = 50, string searchTerm = "")
    {
        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(u => 
                u.DisplayName.Contains(searchTerm) ||
                u.Email.Contains(searchTerm) ||
                u.PreferredUsername.Contains(searchTerm) ||
                u.UserId.Contains(searchTerm));
        }

        return await query
            .OrderByDescending(u => u.LastLoginAt)
            .ThenByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetUserCountAsync(string searchTerm = "")
    {
        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(u => 
                u.DisplayName.Contains(searchTerm) ||
                u.Email.Contains(searchTerm) ||
                u.PreferredUsername.Contains(searchTerm) ||
                u.UserId.Contains(searchTerm));
        }

        return await query.CountAsync();
    }

    public async Task<List<Activity>> GetRecentActivitiesAsync(int count = 50)
    {
        return await _context.Activities
            .Include(a => a.Page)
            .Include(a => a.User)
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<Page>> GetAllPagesAsync(int page = 1, int pageSize = 50, string searchTerm = "")
    {
        var query = _context.Pages.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(p => 
                p.Title.Contains(searchTerm) ||
                p.Slug.Contains(searchTerm) ||
                p.Summary.Contains(searchTerm));
        }

        return await query
            .Include(p => p.Parent)
            .OrderByDescending(p => p.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetPageCountAsync(string searchTerm = "")
    {
        var query = _context.Pages.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(p => 
                p.Title.Contains(searchTerm) ||
                p.Slug.Contains(searchTerm) ||
                p.Summary.Contains(searchTerm));
        }

        return await query.CountAsync();
    }

    public async Task<bool> DeletePageAsync(Guid pageId, string adminUserId)
    {
        try
        {
            var page = await _context.Pages.FindAsync(pageId);
            if (page == null) return false;

            // Log the deletion
            var activity = new Activity
            {
                ActivityType = ActivityTypes.PageDeleted,
                UserId = adminUserId,
                UserDisplayName = "Admin", // Could be improved to get actual admin name
                PageId = pageId,
                PageSlug = page.Slug,
                PageTitle = page.Title,
                Description = "Page deleted by admin",
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.Activities.Add(activity);
            _context.Pages.Remove(page);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Page {PageId} ({PageTitle}) deleted by admin {AdminUserId}", pageId, page.Title, adminUserId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete page {PageId}", pageId);
            return false;
        }
    }

    public async Task<bool> TogglePageLockAsync(Guid pageId, bool locked, string adminUserId)
    {
        try
        {
            var page = await _context.Pages.FindAsync(pageId);
            if (page == null) return false;

            page.IsLocked = locked;
            page.UpdatedAt = DateTimeOffset.UtcNow;

            // Log the action
            var activity = new Activity
            {
                ActivityType = locked ? ActivityTypes.PageLocked : ActivityTypes.PageUnlocked,
                UserId = adminUserId,
                UserDisplayName = "Admin",
                PageId = pageId,
                PageSlug = page.Slug,
                PageTitle = page.Title,
                Description = $"Page {(locked ? "locked" : "unlocked")} by admin",
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Page {PageId} ({PageTitle}) {Action} by admin {AdminUserId}", 
                pageId, page.Title, locked ? "locked" : "unlocked", adminUserId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to {Action} page {PageId}", locked ? "lock" : "unlock", pageId);
            return false;
        }
    }
}