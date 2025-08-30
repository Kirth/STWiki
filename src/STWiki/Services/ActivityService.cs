using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;
using System.Text.Json;

namespace STWiki.Services;

public class ActivityService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ActivityService> _logger;

    public ActivityService(AppDbContext context, ILogger<ActivityService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAsync(
        string activityType,
        string userId,
        string userDisplayName,
        Guid? pageId = null,
        string pageSlug = "",
        string pageTitle = "",
        string description = "",
        object? details = null,
        string ipAddress = "",
        string userAgent = "")
    {
        try
        {
            var activity = new Activity
            {
                ActivityType = activityType,
                UserId = userId,
                UserDisplayName = userDisplayName,
                PageId = pageId,
                PageSlug = pageSlug,
                PageTitle = pageTitle,
                Description = description,
                Details = details != null ? JsonSerializer.Serialize(details) : null,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Activity logged: {ActivityType} by {UserId} for page {PageSlug}",
                activityType, userId, pageSlug);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log activity: {ActivityType} by {UserId}", activityType, userId);
            // Don't rethrow - activity logging should not break the main flow
        }
    }

    public async Task LogPageCreatedAsync(string userId, string userDisplayName, Page page, string ipAddress = "", string userAgent = "")
    {
        await LogAsync(
            ActivityTypes.PageCreated,
            userId,
            userDisplayName,
            page.Id,
            page.Slug,
            page.Title,
            $"Created page '{page.Title}'",
            new { Format = page.BodyFormat, SummaryLength = page.Summary.Length, BodyLength = page.Body.Length },
            ipAddress,
            userAgent
        );
    }

    public async Task LogPageUpdatedAsync(string userId, string userDisplayName, Page page, string editNote = "", string ipAddress = "", string userAgent = "")
    {
        await LogAsync(
            ActivityTypes.PageUpdated,
            userId,
            userDisplayName,
            page.Id,
            page.Slug,
            page.Title,
            string.IsNullOrEmpty(editNote) ? $"Updated page '{page.Title}'" : editNote,
            new { Format = page.BodyFormat, EditNote = editNote },
            ipAddress,
            userAgent
        );
    }

    public async Task LogPageViewedAsync(string userId, string userDisplayName, Page page, string ipAddress = "", string userAgent = "")
    {
        // Only log views for authenticated users to avoid spam
        if (!string.IsNullOrEmpty(userId))
        {
            await LogAsync(
                ActivityTypes.PageViewed,
                userId,
                userDisplayName,
                page.Id,
                page.Slug,
                page.Title,
                $"Viewed page '{page.Title}'",
                null,
                ipAddress,
                userAgent
            );
        }
    }

    public async Task LogSearchAsync(string userId, string userDisplayName, string searchQuery, int resultCount, string ipAddress = "", string userAgent = "")
    {
        await LogAsync(
            ActivityTypes.SearchPerformed,
            userId,
            userDisplayName,
            null,
            "",
            "",
            $"Searched for '{searchQuery}' ({resultCount} results)",
            new { Query = searchQuery, ResultCount = resultCount },
            ipAddress,
            userAgent
        );
    }

    public async Task LogUserLoginAsync(string userId, string userDisplayName, string ipAddress = "", string userAgent = "")
    {
        await LogAsync(
            ActivityTypes.UserLogin,
            userId,
            userDisplayName,
            null,
            "",
            "",
            $"User {userDisplayName} logged in",
            null,
            ipAddress,
            userAgent
        );
    }

    public async Task<List<Activity>> GetRecentActivitiesAsync(int limit = 50, string? activityType = null)
    {
        var query = _context.Activities
            .Include(a => a.Page)
            .OrderByDescending(a => a.CreatedAt);

        if (!string.IsNullOrEmpty(activityType))
        {
            query = (IOrderedQueryable<Activity>)query.Where(a => a.ActivityType == activityType);
        }

        return await query
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<Activity>> GetUserActivitiesAsync(string userId, int limit = 50, string? activityType = null)
    {
        var query = _context.Activities
            .Include(a => a.Page)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt);

        if (!string.IsNullOrEmpty(activityType))
        {
            query = (IOrderedQueryable<Activity>)query.Where(a => a.ActivityType == activityType);
        }

        return await query
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<Activity>> GetPageActivitiesAsync(Guid pageId, int limit = 50, string? activityType = null)
    {
        var query = _context.Activities
            .Include(a => a.Page)
            .Where(a => a.PageId == pageId)
            .OrderByDescending(a => a.CreatedAt);

        if (!string.IsNullOrEmpty(activityType))
        {
            query = (IOrderedQueryable<Activity>)query.Where(a => a.ActivityType == activityType);
        }

        return await query
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetActivityStatisticsAsync(DateTimeOffset since)
    {
        return await _context.Activities
            .Where(a => a.CreatedAt >= since)
            .GroupBy(a => a.ActivityType)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task CleanupOldActivitiesAsync(TimeSpan maxAge)
    {
        var cutoffDate = DateTimeOffset.UtcNow - maxAge;
        
        var oldActivities = _context.Activities
            .Where(a => a.CreatedAt < cutoffDate);

        _context.Activities.RemoveRange(oldActivities);
        var deletedCount = await _context.SaveChangesAsync();

        _logger.LogInformation("Cleaned up {Count} old activities older than {CutoffDate}",
            deletedCount, cutoffDate);
    }
}