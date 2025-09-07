using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using STWiki.Data.Entities;
using STWiki.Services;
using System.Text.Json;

namespace STWiki.Pages;

public class RecentChangesModel : PageModel
{
    private readonly ActivityService _activityService;

    public RecentChangesModel(ActivityService activityService)
    {
        _activityService = activityService;
    }

    public List<Activity> Activities { get; set; } = new();
    public Dictionary<string, int> Statistics { get; set; } = new();
    public int UniqueUsers { get; set; }

    public async Task<IActionResult> OnGetAsync(string? filter = null, int limit = 50)
    {
        try
        {
            // Get recent activities with optional filter
            Activities = await _activityService.GetRecentActivitiesAsync(limit, filter);

            // Get statistics for the last 7 days
            var weekAgo = DateTimeOffset.UtcNow.AddDays(-7);
            Statistics = await _activityService.GetActivityStatisticsAsync(weekAgo);

            // Count unique users in recent activities
            UniqueUsers = Activities.Select(a => a.UserId)
                                  .Where(userId => !string.IsNullOrEmpty(userId))
                                  .Distinct()
                                  .Count();

            return Page();
        }
        catch (Exception ex)
        {
            // Log error but still show page
            Console.WriteLine($"Error loading recent changes: {ex.Message}");
            
            // Return empty data on error
            Activities = new List<Activity>();
            Statistics = new Dictionary<string, int>();
            UniqueUsers = 0;
            
            return Page();
        }
    }

    public async Task<IActionResult> OnGetApiAsync(string? filter = null, int offset = 0, int limit = 20)
    {
        try
        {
            // API endpoint for AJAX pagination
            var activities = await _activityService.GetRecentActivitiesAsync(limit, filter);
            
            // Skip the offset number of items for pagination
            var paginatedActivities = activities.Skip(offset).Take(limit).ToList();

            return new JsonResult(new
            {
                activities = paginatedActivities.Select(a => new
                {
                    id = a.Id,
                    activityType = a.ActivityType,
                    userDisplayName = a.UserDisplayName,
                    pageSlug = a.PageSlug,
                    pageTitle = a.PageTitle,
                    description = a.Description,
                    createdAt = a.CreatedAt,
                    timeAgo = GetTimeAgo(a.CreatedAt)
                }),
                hasMore = paginatedActivities.Count == limit
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading activities API: {ex.Message}");
            return new JsonResult(new { activities = new object[0], hasMore = false });
        }
    }

    public string GetDiffUrl(Activity activity)
    {
        if (activity.ActivityType != "page_updated" || string.IsNullOrEmpty(activity.Details))
            return $"/{activity.PageSlug}/history";

        try
        {
            var details = JsonSerializer.Deserialize<JsonElement>(activity.Details);
            
            if (details.TryGetProperty("CurrentRevisionId", out var currentRevision) && 
                details.TryGetProperty("PreviousRevisionId", out var previousRevision) &&
                currentRevision.ValueKind != JsonValueKind.Null &&
                previousRevision.ValueKind != JsonValueKind.Null)
            {
                var fromId = previousRevision.GetInt64();
                var toId = currentRevision.GetInt64();
                return $"/history?slug={Uri.EscapeDataString(activity.PageSlug)}&fromRevisionId={fromId}&toRevisionId={toId}&handler=Diff";
            }
        }
        catch (JsonException)
        {
            // Fall back to history page if JSON parsing fails
        }

        return $"/{activity.PageSlug}/history";
    }

    private string GetTimeAgo(DateTimeOffset timestamp)
    {
        var timeSpan = DateTimeOffset.UtcNow - timestamp;

        if (timeSpan.TotalMinutes < 1)
            return "just now";
        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes}m ago";
        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours}h ago";
        if (timeSpan.TotalDays < 30)
            return $"{(int)timeSpan.TotalDays}d ago";

        return timestamp.ToString("MMM d, yyyy");
    }
}