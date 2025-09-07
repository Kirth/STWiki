using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;
using STWiki.Services;

namespace STWiki.Pages.User;

public class ContributionsModel : PageModel
{
    private readonly ActivityService _activityService;
    private readonly AppDbContext _context;

    public ContributionsModel(ActivityService activityService, AppDbContext context)
    {
        _activityService = activityService;
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public string Username { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string? Filter { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Offset { get; set; } = 0;

    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public List<Activity> Activities { get; set; } = new();
    public bool HasMore { get; set; }
    public int TotalContributions { get; set; }
    public int PagesCreated { get; set; }
    public int PagesEdited { get; set; }
    public int SearchesPerformed { get; set; }
    public int DaysActive { get; set; }
    public DateTimeOffset? LastContribution { get; set; }
    public List<ContributedPage> TopContributedPages { get; set; } = new();

    private const int PageSize = 50;

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrEmpty(Username))
        {
            return NotFound();
        }

        try
        {
            DisplayName = Username;
            UserId = Username; // For simplicity

            // Map filter parameter to activity type
            string? activityType = Filter switch
            {
                "edits" => ActivityTypes.PageUpdated,
                "creates" => ActivityTypes.PageCreated,
                _ => null
            };

            // Load user activities with pagination
            await LoadUserContributions(activityType);
            
            // Load summary statistics
            await LoadContributionStatistics();
            
            // Load top contributed pages
            await LoadTopContributedPages();

            return Page();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading user contributions: {ex.Message}");
            return Page(); // Show page with empty data
        }
    }

    private async Task LoadUserContributions(string? activityType)
    {
        // Get user activities (load more than needed to check if there are more)
        var allUserActivities = await _activityService.GetUserActivitiesAsync(UserId, Offset + PageSize + 1, activityType);
        
        // Take only the page size for display
        Activities = allUserActivities.Skip(Offset).Take(PageSize).ToList();
        
        // Check if there are more activities beyond current page
        HasMore = allUserActivities.Count > Offset + PageSize;
        
        // Update total count (this is an approximation)
        TotalContributions = allUserActivities.Count;
    }

    private async Task LoadContributionStatistics()
    {
        try
        {
            // Get all user activities for statistics
            var allActivities = await _activityService.GetUserActivitiesAsync(UserId, 1000);
            
            if (!allActivities.Any())
            {
                return;
            }

            // Calculate statistics
            TotalContributions = allActivities.Count;
            PagesCreated = allActivities.Count(a => a.ActivityType == ActivityTypes.PageCreated);
            
            // Count unique pages edited (pages with update activities)
            PagesEdited = allActivities
                .Where(a => a.ActivityType == ActivityTypes.PageUpdated)
                .Select(a => a.PageId)
                .Where(id => id.HasValue)
                .Distinct()
                .Count();
            
            SearchesPerformed = allActivities.Count(a => a.ActivityType == ActivityTypes.SearchPerformed);
            
            // Calculate days active
            var activeDates = allActivities
                .Select(a => a.CreatedAt.Date)
                .Distinct()
                .ToList();
            DaysActive = activeDates.Count;
            
            // Get last contribution
            LastContribution = allActivities.FirstOrDefault()?.CreatedAt;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading contribution statistics: {ex.Message}");
        }
    }

    private async Task LoadTopContributedPages()
    {
        try
        {
            // Get all page-related activities
            var pageActivities = await _activityService.GetUserActivitiesAsync(UserId, 1000);
            
            var pageContributions = pageActivities
                .Where(a => a.PageId.HasValue && 
                           (a.ActivityType == ActivityTypes.PageCreated || 
                            a.ActivityType == ActivityTypes.PageUpdated))
                .GroupBy(a => new { a.PageId, a.PageSlug, a.PageTitle })
                .Select(g => new
                {
                    PageId = g.Key.PageId!.Value,
                    PageSlug = g.Key.PageSlug,
                    PageTitle = g.Key.PageTitle,
                    ContributionCount = g.Count(),
                    LastContribution = g.Max(a => a.CreatedAt),
                    FirstContribution = g.Min(a => a.CreatedAt)
                })
                .OrderByDescending(p => p.ContributionCount)
                .Take(6) // Show top 6 in grid
                .ToList();

            TopContributedPages = new List<ContributedPage>();
            
            foreach (var contrib in pageContributions)
            {
                // Get page summary from database
                string summary = "";
                try
                {
                    var page = await _context.Pages
                        .Where(p => p.Id == contrib.PageId)
                        .Select(p => new { p.Summary, p.Title })
                        .FirstOrDefaultAsync();
                    
                    summary = page?.Summary ?? "No summary available";
                    
                    // Use database title if available (activity might have outdated title)
                    var title = page?.Title ?? contrib.PageTitle;
                    
                    TopContributedPages.Add(new ContributedPage
                    {
                        Slug = contrib.PageSlug,
                        Title = title,
                        Summary = TruncateSummary(summary, 100),
                        ContributionCount = contrib.ContributionCount,
                        LastContribution = contrib.LastContribution
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading page details for {contrib.PageSlug}: {ex.Message}");
                    
                    // Add with activity data only
                    TopContributedPages.Add(new ContributedPage
                    {
                        Slug = contrib.PageSlug,
                        Title = contrib.PageTitle,
                        Summary = "Unable to load summary",
                        ContributionCount = contrib.ContributionCount,
                        LastContribution = contrib.LastContribution
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading top contributed pages: {ex.Message}");
            TopContributedPages = new List<ContributedPage>();
        }
    }

    private string TruncateSummary(string summary, int maxLength)
    {
        if (string.IsNullOrEmpty(summary) || summary.Length <= maxLength)
        {
            return summary;
        }

        var truncated = summary.Substring(0, maxLength);
        var lastSpace = truncated.LastIndexOf(' ');
        
        if (lastSpace > 0)
        {
            truncated = truncated.Substring(0, lastSpace);
        }
        
        return truncated + "...";
    }
}

public class ContributedPage
{
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public int ContributionCount { get; set; }
    public DateTimeOffset LastContribution { get; set; }
}