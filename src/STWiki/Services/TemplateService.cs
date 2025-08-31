using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;
using STWiki.Extensions;
using System.Text.RegularExpressions;

namespace STWiki.Services;

public class TemplateService
{
    private readonly AppDbContext _context;
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(AppDbContext context, ILogger<TemplateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> ProcessTemplatesAsync(string content, Page? currentPage = null)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        // Process {{recent-pages}} template
        var recentPagesPattern = @"\{\{recent-pages(?:\s+limit=(\d+))?\}\}";
        content = await RegexExtensions.ReplaceAsync(content, recentPagesPattern, async (match) =>
        {
            var limitGroup = match.Groups[1];
            var limit = limitGroup.Success && int.TryParse(limitGroup.Value, out var l) ? l : 10;
            return await RenderRecentPagesAsync(limit);
        });

        // Process {{child-pages}} template
        var childPagesPattern = @"\{\{child-pages(?:\s+limit=(\d+))?\}\}";
        content = await RegexExtensions.ReplaceAsync(content, childPagesPattern, async (match) =>
        {
            var limitGroup = match.Groups[1];
            var limit = limitGroup.Success && int.TryParse(limitGroup.Value, out var l) ? l : 20;
            return await RenderChildPagesAsync(currentPage, limit);
        });

        // Process {{popular-pages}} template
        var popularPagesPattern = @"\{\{popular-pages(?:\s+limit=(\d+))?(?:\s+timeframe=(\w+))?\}\}";
        content = await RegexExtensions.ReplaceAsync(content, popularPagesPattern, async (match) =>
        {
            var limitGroup = match.Groups[1];
            var timeframeGroup = match.Groups[2];
            var limit = limitGroup.Success && int.TryParse(limitGroup.Value, out var l) ? l : 10;
            var timeframe = timeframeGroup.Success ? timeframeGroup.Value : "month";
            return await RenderPopularPagesAsync(limit, timeframe);
        });

        // Process {{top-contributors}} template  
        var topContributorsPattern = @"\{\{top-contributors(?:\s+limit=(\d+))?\}\}";
        content = await RegexExtensions.ReplaceAsync(content, topContributorsPattern, async (match) =>
        {
            var limitGroup = match.Groups[1];
            var limit = limitGroup.Success && int.TryParse(limitGroup.Value, out var l) ? l : 10;
            return await RenderTopContributorsAsync(limit);
        });

        // Process {{wiki-statistics}} template
        var wikiStatsPattern = @"\{\{wiki-statistics\}\}";
        content = await RegexExtensions.ReplaceAsync(content, wikiStatsPattern, async (match) =>
        {
            return await RenderWikiStatisticsAsync();
        });

        // Process {{recently-edited}} template
        var recentlyEditedPattern = @"\{\{recently-edited(?:\s+limit=(\d+))?(?:\s+show-users=(true|false))?\}\}";
        content = await RegexExtensions.ReplaceAsync(content, recentlyEditedPattern, async (match) =>
        {
            var limitGroup = match.Groups[1];
            var showUsersGroup = match.Groups[2];
            var limit = limitGroup.Success && int.TryParse(limitGroup.Value, out var l) ? l : 8;
            var showUsers = showUsersGroup.Success && bool.TryParse(showUsersGroup.Value, out var s) ? s : true;
            return await RenderRecentlyEditedAsync(limit, showUsers);
        });

        // Process [[page-slug]] wiki-style links
        var wikiLinkPattern = @"\[\[([^\]]+)\]\]";
        content = await RegexExtensions.ReplaceAsync(content, wikiLinkPattern, async (match) =>
        {
            var pageSlug = match.Groups[1].Value.Trim();
            return await RenderWikiLinkAsync(pageSlug);
        });

        return content;
    }

    private async Task<string> RenderRecentPagesAsync(int limit = 10)
    {
        try
        {
            var recentPages = await _context.Pages
                .OrderByDescending(p => p.UpdatedAt)
                .Take(limit)
                .ToListAsync();

            if (recentPages.Count == 0)
            {
                return @"<div class=""alert alert-info py-2 px-3 mb-3"">
  <i class=""bi bi-info-circle me-1""></i> No recent pages yet.
</div>";
            }

            var sb = new System.Text.StringBuilder();
            sb.Append(@"<section class=""mb-4 border rounded shadow-sm p-3 bg-white"">
 
<header class=""d-flex align-items-center mb-3"">
  <h3 class=""h6 mb-0 lh-sm"">Recent activity</h3>
</header>
  <ul class=""list-unstyled mb-0"">");

            foreach (var page in recentPages)
            {
                var title = System.Web.HttpUtility.HtmlEncode(page.Title ?? "(Untitled)");
                var iso = page.UpdatedAt.ToString("O");
                var display = page.UpdatedAt.ToString("MMM dd, yyyy · HH:mm");

                sb.Append($@"
    <li class=""d-flex justify-content-between align-items-baseline py-2 px-1 border-top"">
      <a class=""text-decoration-none fw-medium text-truncate me-3"" href=""/{page.Slug}"" title=""{title}"">
        <i class=""bi bi-file-text me-2 text-muted""></i>{title}
      </a>
      <time class=""text-muted small"" datetime=""{iso}"">{display}</time>
    </li>");
            }

            sb.Append(@"
  </ul>
</section>");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render recent pages template");
            return @"<div class=""alert alert-danger py-2 px-3 mb-3"">
  <i class=""bi bi-exclamation-triangle me-1""></i> Error loading recent pages.
</div>";
        }
    }

    private async Task<string> RenderChildPagesAsync(Page? currentPage, int limit = 20)
    {
        try
        {
            if (currentPage == null)
            {
                return @"<div class=""alert alert-warning py-2 px-3 mb-3"">
  <i class=""bi bi-info-circle me-1""></i> Child pages can only be displayed when viewing a page.
</div>";
            }

            var childPages = await _context.Pages
                .Where(p => p.ParentId == currentPage.Id)
                .OrderBy(p => p.Title)
                .Take(limit)
                .ToListAsync();

            if (childPages.Count == 0)
            {
                return @"<div class=""alert alert-info py-2 px-3 mb-3"">
  <i class=""bi bi-info-circle me-1""></i> No sub-pages yet.
</div>";
            }

            var sb = new System.Text.StringBuilder();
            sb.Append(@"<section class=""mb-4 border rounded shadow-sm p-3 bg-white"">
 
<header class=""d-flex align-items-center mb-3"">
  <h3 class=""h6 mb-0 lh-sm"">Sub-pages</h3>
</header>
  <ul class=""list-unstyled mb-0"">");

            foreach (var page in childPages)
            {
                var title = System.Web.HttpUtility.HtmlEncode(page.Title ?? "(Untitled)");
                var summary = !string.IsNullOrEmpty(page.Summary) ? System.Web.HttpUtility.HtmlEncode(page.Summary) : null;
                var iso = page.UpdatedAt.ToString("O");
                var display = page.UpdatedAt.ToString("MMM dd, yyyy");

                sb.Append($@"
    <li class=""py-2 px-1 border-top"">
      <div class=""d-flex justify-content-between align-items-start"">
        <div class=""flex-grow-1 min-w-0"">
          <a class=""text-decoration-none fw-medium d-block text-truncate"" href=""/{page.Slug}"" title=""{title}"">
            <i class=""bi bi-file-text me-2 text-muted""></i>{title}
          </a>");
          
                if (!string.IsNullOrEmpty(summary))
                {
                    sb.Append($@"
          <div class=""text-muted small mt-1"">{summary}</div>");
                }

                sb.Append($@"
        </div>
        <time class=""text-muted small ms-3"" datetime=""{iso}"">{display}</time>
      </div>
    </li>");
            }

            sb.Append(@"
  </ul>
</section>");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render child pages template for page {PageId}", currentPage?.Id);
            return @"<div class=""alert alert-danger py-2 px-3 mb-3"">
  <i class=""bi bi-exclamation-triangle me-1""></i> Error loading sub-pages.
</div>";
        }
    }

    private async Task<string> RenderPopularPagesAsync(int limit = 10, string timeframe = "month")
    {
        try
        {
            // Calculate date threshold based on timeframe
            var threshold = timeframe.ToLower() switch
            {
                "day" => DateTimeOffset.UtcNow.AddDays(-1),
                "week" => DateTimeOffset.UtcNow.AddDays(-7),
                "month" => DateTimeOffset.UtcNow.AddDays(-30),
                "year" => DateTimeOffset.UtcNow.AddDays(-365),
                _ => DateTimeOffset.UtcNow.AddDays(-30)
            };

            // Get pages with view counts from Activities table
            var popularPages = await _context.Activities
                .Where(a => a.ActivityType == ActivityTypes.PageViewed && a.CreatedAt >= threshold)
                .GroupBy(a => a.PageId)
                .Select(g => new
                {
                    PageId = g.Key,
                    ViewCount = g.Count(),
                    LastViewed = g.Max(x => x.CreatedAt)
                })
                .OrderByDescending(x => x.ViewCount)
                .Take(limit)
                .Join(_context.Pages, 
                      stat => stat.PageId, 
                      page => page.Id,
                      (stat, page) => new { Page = page, stat.ViewCount, stat.LastViewed })
                .ToListAsync();

            if (!popularPages.Any())
            {
                return $@"<div class=""alert alert-info py-2 px-3 mb-3"">
  <i class=""bi bi-info-circle me-1""></i> No popular pages in the last {timeframe}.
</div>";
            }

            var sb = new System.Text.StringBuilder();
            sb.Append($@"<section class=""mb-4 border rounded shadow-sm p-3 bg-white"">
 
<header class=""d-flex align-items-center mb-3"">
  <h3 class=""h6 mb-0 lh-sm"">Popular pages (last {timeframe})</h3>
</header>
  <ul class=""list-unstyled mb-0"">");

            foreach (var item in popularPages)
            {
                var title = System.Web.HttpUtility.HtmlEncode(item.Page.Title ?? "(Untitled)");
                var viewText = item.ViewCount == 1 ? "view" : "views";

                sb.Append($@"
    <li class=""d-flex justify-content-between align-items-center py-2 px-1 border-top"">
      <div class=""flex-grow-1 min-w-0"">
        <a class=""text-decoration-none fw-medium d-block text-truncate"" href=""/{item.Page.Slug}"" title=""{title}"">
          <i class=""bi bi-fire me-2 text-warning""></i>{title}
        </a>
      </div>
      <span class=""badge bg-primary ms-3"">{item.ViewCount} {viewText}</span>
    </li>");
            }

            sb.Append(@"
  </ul>
</section>");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render popular pages template");
            return @"<div class=""alert alert-danger py-2 px-3 mb-3"">
  <i class=""bi bi-exclamation-triangle me-1""></i> Error loading popular pages.
</div>";
        }
    }

    private async Task<string> RenderTopContributorsAsync(int limit = 10)
    {
        try
        {
            // Get top contributors from Activities table
            var topContributors = await _context.Activities
                .Where(a => a.ActivityType == ActivityTypes.PageUpdated || a.ActivityType == ActivityTypes.PageCreated)
                .GroupBy(a => a.UserDisplayName)
                .Select(g => new
                {
                    UserName = g.Key,
                    EditCount = g.Count(x => x.ActivityType == ActivityTypes.PageUpdated),
                    CreateCount = g.Count(x => x.ActivityType == ActivityTypes.PageCreated),
                    TotalContributions = g.Count(),
                    LastActivity = g.Max(x => x.CreatedAt)
                })
                .OrderByDescending(x => x.TotalContributions)
                .Take(limit)
                .ToListAsync();

            if (!topContributors.Any())
            {
                return @"<div class=""alert alert-info py-2 px-3 mb-3"">
  <i class=""bi bi-info-circle me-1""></i> No contributors found.
</div>";
            }

            var sb = new System.Text.StringBuilder();
            sb.Append(@"<section class=""mb-4 border rounded shadow-sm p-3 bg-white"">
 
<header class=""d-flex align-items-center mb-3"">
  <h3 class=""h6 mb-0 lh-sm"">Top contributors</h3>
</header>
  <ul class=""list-unstyled mb-0"">");

            var rank = 1;
            foreach (var contributor in topContributors)
            {
                var userName = System.Web.HttpUtility.HtmlEncode(contributor.UserName ?? "Anonymous");
                var rankIcon = rank <= 3 ? "bi-trophy-fill" : "bi-person-fill";
                var rankColor = rank switch
                {
                    1 => "text-warning",
                    2 => "text-secondary", 
                    3 => "text-warning",
                    _ => "text-muted"
                };

                sb.Append($@"
    <li class=""d-flex justify-content-between align-items-center py-2 px-1 border-top"">
      <div class=""d-flex align-items-center flex-grow-1 min-w-0"">
        <span class=""me-2"">#{rank}</span>
        <i class=""bi {rankIcon} {rankColor} me-2""></i>
        <div class=""flex-grow-1 min-w-0"">
          <div class=""fw-medium text-truncate"">{userName}</div>
          <small class=""text-muted"">{contributor.CreateCount} pages created, {contributor.EditCount} edits</small>
        </div>
      </div>
      <span class=""badge bg-success ms-3"">{contributor.TotalContributions}</span>
    </li>");
                rank++;
            }

            sb.Append(@"
  </ul>
</section>");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render top contributors template");
            return @"<div class=""alert alert-danger py-2 px-3 mb-3"">
  <i class=""bi bi-exclamation-triangle me-1""></i> Error loading top contributors.
</div>";
        }
    }

    private async Task<string> RenderWikiStatisticsAsync()
    {
        try
        {
            // Get comprehensive statistics
            var totalPages = await _context.Pages.CountAsync();
            var totalActivities = await _context.Activities.CountAsync();
            var uniqueUsers = await _context.Activities.Select(a => a.UserDisplayName).Distinct().CountAsync();
            
            var last30Days = DateTimeOffset.UtcNow.AddDays(-30);
            var recentViews = await _context.Activities
                .Where(a => a.ActivityType == ActivityTypes.PageViewed && a.CreatedAt >= last30Days)
                .CountAsync();
            var recentEdits = await _context.Activities
                .Where(a => a.ActivityType == ActivityTypes.PageUpdated && a.CreatedAt >= last30Days)
                .CountAsync();

            var mostActiveDay = await _context.Activities
                .Where(a => a.CreatedAt >= last30Days)
                .GroupBy(a => a.CreatedAt.Date)
                .OrderByDescending(g => g.Count())
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .FirstOrDefaultAsync();

            var sb = new System.Text.StringBuilder();
            sb.Append(@"<section class=""mb-4 border rounded shadow-sm p-3 bg-white"">
 
<header class=""d-flex align-items-center mb-3"">
  <h3 class=""h6 mb-0 lh-sm"">Wiki statistics</h3>
</header>

<div class=""row g-3"">
  <div class=""col-md-6"">
    <div class=""d-flex align-items-center p-2 bg-light rounded"">
      <i class=""bi bi-file-text fs-4 text-primary me-3""></i>
      <div>
        <div class=""fw-bold"">" + totalPages + @"</div>
        <small class=""text-muted"">Total pages</small>
      </div>
    </div>
  </div>
  
  <div class=""col-md-6"">
    <div class=""d-flex align-items-center p-2 bg-light rounded"">
      <i class=""bi bi-people fs-4 text-success me-3""></i>
      <div>
        <div class=""fw-bold"">" + uniqueUsers + @"</div>
        <small class=""text-muted"">Contributors</small>
      </div>
    </div>
  </div>
  
  <div class=""col-md-6"">
    <div class=""d-flex align-items-center p-2 bg-light rounded"">
      <i class=""bi bi-eye fs-4 text-info me-3""></i>
      <div>
        <div class=""fw-bold"">" + recentViews + @"</div>
        <small class=""text-muted"">Views (30 days)</small>
      </div>
    </div>
  </div>
  
  <div class=""col-md-6"">
    <div class=""d-flex align-items-center p-2 bg-light rounded"">
      <i class=""bi bi-pencil fs-4 text-warning me-3""></i>
      <div>
        <div class=""fw-bold"">" + recentEdits + @"</div>
        <small class=""text-muted"">Edits (30 days)</small>
      </div>
    </div>
  </div>
</div>");

            if (mostActiveDay != null)
            {
                sb.Append($@"

<div class=""mt-3 pt-3 border-top"">
  <small class=""text-muted"">
    <i class=""bi bi-calendar-event me-1""></i>
    Most active day: {mostActiveDay.Date:MMM dd, yyyy} ({mostActiveDay.Count} activities)
  </small>
</div>");
            }

            sb.Append(@"
</section>");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render wiki statistics template");
            return @"<div class=""alert alert-danger py-2 px-3 mb-3"">
  <i class=""bi bi-exclamation-triangle me-1""></i> Error loading wiki statistics.
</div>";
        }
    }

    private async Task<string> RenderRecentlyEditedAsync(int limit = 8, bool showUsers = true)
    {
        try
        {
            // Get recently edited pages with user information
            var recentlyEdited = await _context.Activities
                .Where(a => a.ActivityType == ActivityTypes.PageUpdated)
                .OrderByDescending(a => a.CreatedAt)
                .Take(limit)
                .Join(_context.Pages,
                      activity => activity.PageId,
                      page => page.Id,
                      (activity, page) => new { activity, page })
                .ToListAsync();

            if (!recentlyEdited.Any())
            {
                return @"<div class=""alert alert-info py-2 px-3 mb-3"">
  <i class=""bi bi-info-circle me-1""></i> No recent edits found.
</div>";
            }

            var sb = new System.Text.StringBuilder();
            sb.Append(@"<section class=""mb-4 border rounded shadow-sm p-3 bg-white"">
 
<header class=""d-flex align-items-center mb-3"">
  <h3 class=""h6 mb-0 lh-sm"">Recently edited</h3>
</header>
  <ul class=""list-unstyled mb-0"">");

            foreach (var item in recentlyEdited)
            {
                var title = System.Web.HttpUtility.HtmlEncode(item.page.Title ?? "(Untitled)");
                var userName = System.Web.HttpUtility.HtmlEncode(item.activity.UserDisplayName ?? "Anonymous");
                var iso = item.activity.CreatedAt.ToString("O");
                var display = item.activity.CreatedAt.ToString("MMM dd, yyyy · HH:mm");

                sb.Append($@"
    <li class=""py-2 px-1 border-top"">
      <div class=""d-flex justify-content-between align-items-start"">
        <div class=""flex-grow-1 min-w-0"">
          <a class=""text-decoration-none fw-medium d-block text-truncate"" href=""/{item.page.Slug}"" title=""{title}"">
            <i class=""bi bi-pencil me-2 text-muted""></i>{title}
          </a>");

                if (showUsers)
                {
                    sb.Append($@"
          <div class=""text-muted small mt-1"">
            <i class=""bi bi-person-fill me-1""></i>Edited by {userName}
          </div>");
                }

                sb.Append($@"
        </div>
        <time class=""text-muted small ms-3"" datetime=""{iso}"">{display}</time>
      </div>
    </li>");
            }

            sb.Append(@"
  </ul>
</section>");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render recently edited template");
            return @"<div class=""alert alert-danger py-2 px-3 mb-3"">
  <i class=""bi bi-exclamation-triangle me-1""></i> Error loading recently edited pages.
</div>";
        }
    }

    private async Task<string> RenderWikiLinkAsync(string pageSlug)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pageSlug))
            {
                return "[[]]";
            }

            // Handle custom display text: [[page-slug|Display Text]]
            var displayText = pageSlug;
            if (pageSlug.Contains('|'))
            {
                var parts = pageSlug.Split('|', 2);
                pageSlug = parts[0].Trim();
                displayText = parts[1].Trim();
            }

            // Look up the page to get its title and verify it exists
            var page = await _context.Pages
                .Where(p => p.Slug.ToLower() == pageSlug.ToLower())
                .Select(p => new { p.Slug, p.Title })
                .FirstOrDefaultAsync();

            if (page != null)
            {
                // Page exists - create link with page title (unless custom display text is provided)
                var linkText = pageSlug.Contains('|') ? displayText : page.Title;
                var encodedText = System.Web.HttpUtility.HtmlEncode(linkText);
                var encodedSlug = System.Web.HttpUtility.UrlEncode(page.Slug);
                
                return $@"<a href=""/{encodedSlug}"" class=""wiki-link"" title=""{System.Web.HttpUtility.HtmlAttributeEncode(page.Title)}"">{encodedText}</a>";
            }
            else
            {
                // Page doesn't exist - create a red link (broken link) with option to create
                var linkText = pageSlug.Contains('|') ? displayText : pageSlug.Replace("-", " ").Replace("_", " ");
                var encodedText = System.Web.HttpUtility.HtmlEncode(linkText);
                var encodedSlug = System.Web.HttpUtility.UrlEncode(pageSlug);
                
                return $@"<a href=""/{encodedSlug}"" class=""wiki-link wiki-link-missing"" title=""Page '{pageSlug}' does not exist - click to create"">{encodedText}</a>";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render wiki link for page slug: {PageSlug}", pageSlug);
            return $"[[{System.Web.HttpUtility.HtmlEncode(pageSlug)}]]";
        }
    }


}
