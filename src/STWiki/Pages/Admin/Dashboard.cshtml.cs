using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using STWiki.Data.Entities;
using STWiki.Services;

namespace STWiki.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class DashboardModel : PageModel
{
    private readonly AdminService _adminService;
    private readonly ActivityService _activityService;

    public DashboardModel(AdminService adminService, ActivityService activityService)
    {
        _adminService = adminService;
        _activityService = activityService;
    }

    public Dictionary<string, object> SystemStats { get; set; } = new();
    public List<Activity> RecentActivities { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            SystemStats = await _adminService.GetSystemStatisticsAsync();
            RecentActivities = await _adminService.GetRecentActivitiesAsync(20);
            
            return Page();
        }
        catch (Exception ex)
        {
            // Log error and show page with empty data
            return Page();
        }
    }
}