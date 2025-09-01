using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;
using STWiki.Services;
using System.Security.Claims;

namespace STWiki.Pages.User;

public class ProfileModel : PageModel
{
    private readonly ActivityService _activityService;
    private readonly UserService _userService;
    private readonly AppDbContext _context;

    public ProfileModel(ActivityService activityService, UserService userService, AppDbContext context)
    {
        _activityService = activityService;
        _userService = userService;
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public string Username { get; set; } = "";

    public STWiki.Data.Entities.User? UserProfile { get; set; }
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Bio { get; set; } = "";
    public bool IsCurrentUser { get; set; }
    public bool IsProfileVisible { get; set; } = true;
    public int TotalContributions { get; set; }
    public int PagesCreated { get; set; }
    public DateTimeOffset? LastActive { get; set; }
    public DateTimeOffset? MemberSince { get; set; }
    public string MostActiveDay { get; set; } = "";
    public List<UserContribution> TopContributions { get; set; } = new();
    public Dictionary<string, int> WeeklyActivity { get; set; } = new();
    public Dictionary<string, int> ActivityBreakdown { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrEmpty(Username))
        {
            return NotFound();
        }

        try
        {
            // Try to find user by any identifier (username, display name, sub, email)
            UserProfile = await _userService.GetUserByIdentifierAsync(Username);

            // Determine if this is the current user
            var currentUserName = User.Identity?.Name;
            IsCurrentUser = !string.IsNullOrEmpty(currentUserName) && 
                           (currentUserName.Equals(Username, StringComparison.OrdinalIgnoreCase) ||
                            (UserProfile != null && currentUserName.Equals(UserProfile.UserId, StringComparison.OrdinalIgnoreCase)));

            // Check if profile is visible
            if (UserProfile != null)
            {
                IsProfileVisible = UserProfile.IsProfilePublic || IsCurrentUser;
                
                if (!IsProfileVisible)
                {
                    return Page(); // Return page but with limited info
                }

                // Set user info from profile
                DisplayName = UserProfile.DisplayName;
                Bio = UserProfile.Bio;
                UserId = UserProfile.UserId;
                MemberSince = UserProfile.CreatedAt;
            }
            else
            {
                // Fallback for users without profile records
                DisplayName = Username;
                UserId = Username;
                IsProfileVisible = true; // Always show if no profile exists
            }
            
            // Load user activities if profile is visible
            if (IsProfileVisible)
            {
                await LoadUserStatistics();
            }
            
            return Page();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading user profile: {ex.Message}");
            return Page(); // Show page with empty data rather than error
        }
    }

    public async Task<IActionResult> OnPostUpdateProfileAsync(string displayName, string bio)
    {
        if (!IsCurrentUser)
        {
            return Forbid();
        }

        try
        {
            // In a real system, you would update the user profile in the database
            // For now, we'll just redirect back to the profile page
            DisplayName = displayName ?? Username;
            Bio = bio ?? "";
            
            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToPage(new { username = Username });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating profile: {ex.Message}");
            TempData["ErrorMessage"] = "Failed to update profile. Please try again.";
            return RedirectToPage(new { username = Username });
        }
    }

    private async Task LoadUserStatistics()
    {
        // Get user activities
        var userActivities = await _activityService.GetUserActivitiesAsync(UserId, 200);
        
        if (!userActivities.Any())
        {
            return; // No activities found
        }

        // Calculate statistics
        TotalContributions = userActivities.Count;
        PagesCreated = userActivities.Count(a => a.ActivityType == ActivityTypes.PageCreated);
        LastActive = userActivities.FirstOrDefault()?.CreatedAt;
        MemberSince = userActivities.LastOrDefault()?.CreatedAt;

        // Find most active day of week
        var dayActivity = userActivities
            .GroupBy(a => a.CreatedAt.DayOfWeek)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());
        
        MostActiveDay = dayActivity.Any() 
            ? dayActivity.OrderByDescending(kvp => kvp.Value).First().Key 
            : "Unknown";

        // Calculate weekly activity (last 7 days)
        var weekAgo = DateTimeOffset.UtcNow.AddDays(-7);
        var weeklyActivities = userActivities.Where(a => a.CreatedAt >= weekAgo).ToList();
        
        WeeklyActivity = new Dictionary<string, int>();
        for (int i = 6; i >= 0; i--)
        {
            var day = DateTimeOffset.UtcNow.AddDays(-i);
            var dayName = day.ToString("ddd");
            var dayActivities = weeklyActivities.Count(a => a.CreatedAt.Date == day.Date);
            WeeklyActivity[dayName] = dayActivities;
        }

        // Activity type breakdown
        ActivityBreakdown = userActivities
            .GroupBy(a => a.ActivityType)
            .ToDictionary(g => g.Key, g => g.Count());

        // Get top contributions (pages the user has contributed to most)
        await LoadTopContributions();
    }

    private async Task LoadTopContributions()
    {
        try
        {
            // Get pages this user has created or edited
            var pageActivities = await _activityService.GetUserActivitiesAsync(UserId, 1000);
            
            var pageContributions = pageActivities
                .Where(a => a.PageId.HasValue && (a.ActivityType == ActivityTypes.PageCreated || a.ActivityType == ActivityTypes.PageUpdated))
                .GroupBy(a => new { a.PageId, a.PageSlug, a.PageTitle })
                .Select(g => new
                {
                    PageId = g.Key.PageId!.Value,
                    PageSlug = g.Key.PageSlug,
                    PageTitle = g.Key.PageTitle,
                    EditCount = g.Count(),
                    LastEdited = g.Max(a => a.CreatedAt),
                    FirstContribution = g.Min(a => a.CreatedAt)
                })
                .OrderByDescending(p => p.EditCount)
                .Take(10);

            TopContributions = new List<UserContribution>();
            
            foreach (var contrib in pageContributions)
            {
                // Try to get page summary from database
                string summary = "";
                try
                {
                    var page = await _context.Pages
                        .Where(p => p.Id == contrib.PageId)
                        .Select(p => p.Summary)
                        .FirstOrDefaultAsync();
                    summary = page ?? "";
                }
                catch
                {
                    summary = "No summary available";
                }

                TopContributions.Add(new UserContribution
                {
                    PageSlug = contrib.PageSlug,
                    PageTitle = contrib.PageTitle,
                    EditCount = contrib.EditCount,
                    LastEdited = contrib.LastEdited,
                    Summary = summary
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading top contributions: {ex.Message}");
            TopContributions = new List<UserContribution>();
        }
    }
}

public class UserContribution
{
    public string PageSlug { get; set; } = "";
    public string PageTitle { get; set; } = "";
    public int EditCount { get; set; }
    public DateTimeOffset LastEdited { get; set; }
    public string Summary { get; set; } = "";
}