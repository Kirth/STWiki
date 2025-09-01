using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using STWiki.Data.Entities;
using STWiki.Services;

namespace STWiki.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class ActivityModel : PageModel
{
    private readonly AdminService _adminService;
    private readonly ActivityService _activityService;

    public ActivityModel(AdminService adminService, ActivityService activityService)
    {
        _adminService = adminService;
        _activityService = activityService;
    }

    public List<Activity> Activities { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalActivities { get; set; }
    public string ActivityTypeFilter { get; set; } = "";
    public string UserFilter { get; set; } = "";
    public DateTimeOffset? DateFrom { get; set; }
    public DateTimeOffset? DateTo { get; set; }
    public int PageSize { get; set; } = 50;

    [BindProperty(SupportsGet = true)]
    public string? ActivityType { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? User { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? To { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Page { get; set; } = 1;

    public List<string> AvailableActivityTypes { get; set; } = new()
    {
        ActivityTypes.PageCreated,
        ActivityTypes.PageUpdated,
        ActivityTypes.PageDeleted,
        ActivityTypes.PageViewed,
        ActivityTypes.PageLocked,
        ActivityTypes.PageUnlocked,
        ActivityTypes.UserLogin,
        ActivityTypes.UserLogout,
        ActivityTypes.SearchPerformed
    };

    public async Task<IActionResult> OnGetAsync()
    {
        ActivityTypeFilter = ActivityType ?? "";
        UserFilter = User ?? "";
        CurrentPage = Page;

        // Parse date filters
        if (DateTimeOffset.TryParse(From, out var fromDate))
            DateFrom = fromDate;
        if (DateTimeOffset.TryParse(To, out var toDate))
            DateTo = toDate.AddDays(1).AddSeconds(-1); // End of day

        try
        {
            // Get filtered activities
            Activities = await GetFilteredActivitiesAsync();
            TotalActivities = await GetFilteredActivityCountAsync();
            TotalPages = (int)Math.Ceiling((double)TotalActivities / PageSize);

            return Page();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "An error occurred while loading activities.";
            return Page();
        }
    }

    private async Task<List<Activity>> GetFilteredActivitiesAsync()
    {
        // For now, using the basic method and filtering manually
        // In a real implementation, you'd want to add filtering parameters to AdminService
        var allActivities = await _adminService.GetRecentActivitiesAsync(1000);
        
        var filteredActivities = allActivities.AsQueryable();

        if (!string.IsNullOrEmpty(ActivityTypeFilter))
        {
            filteredActivities = filteredActivities.Where(a => a.ActivityType == ActivityTypeFilter);
        }

        if (!string.IsNullOrEmpty(UserFilter))
        {
            filteredActivities = filteredActivities.Where(a => 
                a.UserId.Contains(UserFilter, StringComparison.OrdinalIgnoreCase) ||
                a.UserDisplayName.Contains(UserFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (DateFrom.HasValue)
        {
            filteredActivities = filteredActivities.Where(a => a.CreatedAt >= DateFrom.Value);
        }

        if (DateTo.HasValue)
        {
            filteredActivities = filteredActivities.Where(a => a.CreatedAt <= DateTo.Value);
        }

        return filteredActivities
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }

    private async Task<int> GetFilteredActivityCountAsync()
    {
        // Similar filtering logic for count
        var allActivities = await _adminService.GetRecentActivitiesAsync(1000);
        
        var filteredActivities = allActivities.AsQueryable();

        if (!string.IsNullOrEmpty(ActivityTypeFilter))
        {
            filteredActivities = filteredActivities.Where(a => a.ActivityType == ActivityTypeFilter);
        }

        if (!string.IsNullOrEmpty(UserFilter))
        {
            filteredActivities = filteredActivities.Where(a => 
                a.UserId.Contains(UserFilter, StringComparison.OrdinalIgnoreCase) ||
                a.UserDisplayName.Contains(UserFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (DateFrom.HasValue)
        {
            filteredActivities = filteredActivities.Where(a => a.CreatedAt >= DateFrom.Value);
        }

        if (DateTo.HasValue)
        {
            filteredActivities = filteredActivities.Where(a => a.CreatedAt <= DateTo.Value);
        }

        return filteredActivities.Count();
    }
}