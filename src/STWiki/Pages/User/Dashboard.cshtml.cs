using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using STWiki.Data.Entities;
using STWiki.Services;

namespace STWiki.Pages.User;

[Authorize]
public class DashboardModel : PageModel
{
    private readonly UserService _userService;
    private readonly ActivityService _activityService;

    public DashboardModel(UserService userService, ActivityService activityService)
    {
        _userService = userService;
        _activityService = activityService;
    }

    public STWiki.Data.Entities.User UserProfile { get; set; } = null!;
    public Dictionary<string, object> UserStats { get; set; } = new();
    public List<Activity> RecentActivity { get; set; } = new();
    public List<Activity> RecentContributions { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.Name == null)
        {
            return Challenge();
        }

        // Get or create user profile
        UserProfile = await _userService.GetOrCreateUserAsync(User);
        
        // Get user statistics
        UserStats = await _userService.GetUserStatsAsync(User.Identity.Name);
        
        // Get recent activity (last 10)
        RecentActivity = (await _activityService.GetUserActivitiesAsync(User.Identity.Name, 10)).ToList();
        
        // Get recent contributions (pages created/edited in last 30 days)
        var thirtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-30);
        var allActivity = await _activityService.GetUserActivitiesAsync(User.Identity.Name, 100);
        RecentContributions = allActivity
            .Where(a => a.CreatedAt >= thirtyDaysAgo && 
                       (a.ActivityType == ActivityTypes.PageCreated || a.ActivityType == ActivityTypes.PageUpdated))
            .Take(5)
            .ToList();
        
        return Page();
    }
}