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
    private readonly IMediaService _mediaService;

    public TemplateService(AppDbContext context, ILogger<TemplateService> logger, IMediaService mediaService)
    {
        _context = context;
        _logger = logger;
        _mediaService = mediaService;
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
            // Get top contributors from Activities table - join with Users to get current display names
            var topContributors = await _context.Activities
                .Where(a => a.ActivityType == ActivityTypes.PageUpdated || a.ActivityType == ActivityTypes.PageCreated)
                .Include(a => a.User)
                .Where(a => a.User != null) // Only include activities with valid user references
                .GroupBy(a => a.User!.Id) // Group by User ID to handle display name changes
                .Select(g => new
                {
                    User = g.First().User!,
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
                var user = contributor.User;
                var userDisplayName = user?.DisplayName ?? "Anonymous";
                var userSlug = user != null ? GetUserSlugFromUser(user) : "";
                var userLink = user != null 
                    ? $"<a href=\"/user/{Uri.EscapeDataString(userSlug)}\" class=\"text-decoration-none fw-medium text-truncate\">{System.Web.HttpUtility.HtmlEncode(userDisplayName)}</a>"
                    : $"<div class=\"fw-medium text-truncate\">{System.Web.HttpUtility.HtmlEncode(userDisplayName)}</div>";
                    
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
          {userLink}
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
            // Get recently edited pages with user information - join with Users table to get current display names
            var recentlyEdited = await _context.Activities
                .Where(a => a.ActivityType == ActivityTypes.PageUpdated)
                .Include(a => a.User)
                .Include(a => a.Page)
                .OrderByDescending(a => a.CreatedAt)
                .Take(limit)
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

            foreach (var activity in recentlyEdited)
            {
                var title = System.Web.HttpUtility.HtmlEncode(activity.Page?.Title ?? "(Untitled)");
                var user = activity.User;
                var userDisplayName = user?.DisplayName ?? "Anonymous";
                var userLink = user != null 
                    ? $"<a href=\"/user/{Uri.EscapeDataString(GetUserSlugFromUser(user))}\" class=\"text-decoration-none\">{System.Web.HttpUtility.HtmlEncode(userDisplayName)}</a>"
                    : "<span class=\"text-muted\">Anonymous</span>";
                var iso = activity.CreatedAt.ToString("O");
                var display = activity.CreatedAt.ToString("MMM dd, yyyy · HH:mm");

                sb.Append($@"
    <li class=""py-2 px-1 border-top"">
      <div class=""d-flex justify-content-between align-items-start"">
        <div class=""flex-grow-1 min-w-0"">
          <a class=""text-decoration-none fw-medium d-block text-truncate"" href=""/{activity.Page?.Slug}"" title=""{title}"">
            <i class=""bi bi-pencil me-2 text-muted""></i>{title}
          </a>");

                if (showUsers)
                {
                    sb.Append($@"
          <div class=""text-muted small mt-1"">
            <i class=""bi bi-person-fill me-1""></i>Edited by {userLink}
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

            // Handle media links: [[media:filename|param=value|...]]
            if (pageSlug.StartsWith("media:", StringComparison.OrdinalIgnoreCase))
            {
                var mediaTemplate = pageSlug.Substring(6).Trim();
                return await RenderMediaLinkAsync(mediaTemplate);
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

    private async Task<string> RenderMediaLinkAsync(string mediaTemplate)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(mediaTemplate))
            {
                return "[[media:]]";
            }

            // Parse display options from template
            var options = MediaDisplayOptions.Parse(mediaTemplate);
            
            if (string.IsNullOrWhiteSpace(options.FileName))
            {
                return "[[media:]]";
            }

            var mediaFile = await _mediaService.GetMediaFileByNameAsync(options.FileName);
            
            if (mediaFile == null)
            {
                return $"<span class=\"text-danger\" title=\"Media file '{options.FileName}' not found\">[[media:{System.Web.HttpUtility.HtmlEncode(mediaTemplate)}]]</span>";
            }

            if (IsImage(mediaFile.ContentType))
            {
                return await RenderImageAsync(mediaFile, options);
            }
            else
            {
                return await RenderFileAsync(mediaFile, options);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render media link for: {MediaTemplate}", mediaTemplate);
            return $"<span class=\"text-danger\">[[media:{System.Web.HttpUtility.HtmlEncode(mediaTemplate)}]]</span>";
        }
    }

    private async Task<string> RenderImageAsync(MediaFile mediaFile, MediaDisplayOptions options)
    {
        var effectiveSize = options.GetEffectiveSize();
        var thumbnailUrl = await _mediaService.GetMediaUrlAsync(mediaFile.Id, effectiveSize);
        var fullUrl = options.Display == "full" ? 
            await _mediaService.GetMediaUrlAsync(mediaFile.Id) : 
            thumbnailUrl;
        
        // Determine alt text
        var altText = !string.IsNullOrEmpty(options.Alt) ? options.Alt :
                     (!string.IsNullOrEmpty(mediaFile.AltText) ? mediaFile.AltText : mediaFile.OriginalFileName);
        
        // Determine caption
        var caption = !string.IsNullOrEmpty(options.Caption) ? options.Caption :
                     (!string.IsNullOrEmpty(mediaFile.Description) ? mediaFile.Description : "");
        
        var sizeStyles = options.GetSizeStyles();
        var displayClasses = options.GetDisplayClasses();
        var alignmentClass = options.GetAlignmentClass();
        
        // Build image HTML
        var imageHtml = $@"<img src=""{thumbnailUrl}"" 
                                alt=""{System.Web.HttpUtility.HtmlEncode(altText)}"" 
                                class=""{displayClasses}""
                                {(!string.IsNullOrEmpty(sizeStyles) ? $"style=\"{sizeStyles}\"" : "")}>";
        
        // For inline display, return just the image
        if (options.Display == "inline")
        {
            return imageHtml;
        }
        
        // For block display, wrap in container
        var containerClass = $"media-embed image-embed mb-3 {alignmentClass}".Trim();
        
        var result = $@"<div class=""{containerClass}"">";
        
        // Add link wrapper if not full display or if it's a thumbnail
        if (options.Display != "full" || effectiveSize < MediaDisplayOptions.DefaultThumbnailSize)
        {
            result += $@"<a href=""{fullUrl}"" target=""_blank"" class=""text-decoration-none"">";
            result += imageHtml;
            result += "</a>";
        }
        else
        {
            result += imageHtml;
        }
        
        // Add caption if present
        if (!string.IsNullOrEmpty(caption))
        {
            result += $@"<small class=""text-muted d-block mt-1"">{System.Web.HttpUtility.HtmlEncode(caption)}</small>";
        }
        
        result += "</div>";
        
        return result;
    }

    private async Task<string> RenderFileAsync(MediaFile mediaFile, MediaDisplayOptions options)
    {
        var fileUrl = await _mediaService.GetMediaUrlAsync(mediaFile.Id);
        var fileSize = FormatFileSize(mediaFile.FileSize);
        var icon = GetFileIcon(mediaFile.ContentType);
        
        // Determine caption/description
        var description = !string.IsNullOrEmpty(options.Caption) ? options.Caption :
                         (!string.IsNullOrEmpty(mediaFile.Description) ? mediaFile.Description : "");
        
        var alignmentClass = options.GetAlignmentClass();
        var containerClass = $"media-embed file-embed mb-2 {alignmentClass}".Trim();
        
        return $@"
            <div class=""{containerClass}"">
                <a href=""{fileUrl}"" class=""text-decoration-none d-flex align-items-center p-2 border rounded"" target=""_blank"">
                    <i class=""bi bi-{icon} fs-4 text-primary me-3""></i>
                    <div class=""flex-grow-1"">
                        <div class=""fw-medium"">{System.Web.HttpUtility.HtmlEncode(mediaFile.OriginalFileName)}</div>
                        <small class=""text-muted"">{fileSize}</small>
                        {(!string.IsNullOrEmpty(description) ? 
                            $"<div class=\"text-muted small\">{System.Web.HttpUtility.HtmlEncode(description)}</div>" : 
                            "")}
                    </div>
                    <i class=""bi bi-download ms-2 text-muted""></i>
                </a>
            </div>";
    }

    private static bool IsImage(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private static string FormatFileSize(long bytes)
    {
        if (bytes == 0) return "0 Bytes";
        const int k = 1024;
        string[] sizes = { "Bytes", "KB", "MB", "GB" };
        int i = (int)Math.Floor(Math.Log(bytes) / Math.Log(k));
        return $"{Math.Round(bytes / Math.Pow(k, i), 2)} {sizes[i]}";
    }

    private static string GetFileIcon(string contentType)
    {
        if (contentType.StartsWith("image/")) return "file-earmark-image";
        if (contentType.Contains("pdf")) return "file-earmark-pdf";
        if (contentType.Contains("word") || contentType.Contains("document")) return "file-earmark-word";
        if (contentType.Contains("excel") || contentType.Contains("spreadsheet")) return "file-earmark-excel";
        if (contentType.Contains("powerpoint") || contentType.Contains("presentation")) return "file-earmark-ppt";
        if (contentType.StartsWith("text/")) return "file-earmark-text";
        return "file-earmark";
    }

    private string GetUserSlugFromUser(STWiki.Data.Entities.User user)
    {
        if (!string.IsNullOrEmpty(user.PreferredUsername))
            return user.PreferredUsername;
        if (!string.IsNullOrEmpty(user.DisplayName))
            return user.DisplayName;
        return user.UserId;
    }

    private bool IsUserFriendlyIdentifier(string? identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return false;
            
        // Check if it's a long base64-like string (sub claim)
        if (identifier.Length > 40 && !identifier.Contains(" "))
            return false;
        
        // Check if it's an email
        if (identifier.Contains("@"))
            return false;
        
        // Assume it's user-friendly if it's short and contains spaces or looks like a name
        return identifier.Length <= 30;
    }
}

public class MediaDisplayOptions
{
    public string FileName { get; set; } = "";
    public int? Size { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string Align { get; set; } = ""; // left, center, right
    public string Display { get; set; } = "block"; // inline, block, thumb, full
    public string Alt { get; set; } = "";
    public string Caption { get; set; } = "";
    public string Class { get; set; } = "";
    
    // Default values
    public const int DefaultThumbnailSize = 600;
    public const int ThumbSize = 150;
    
    public static MediaDisplayOptions Parse(string mediaTemplate)
    {
        var options = new MediaDisplayOptions();
        
        if (string.IsNullOrWhiteSpace(mediaTemplate))
            return options;
        
        // Split by pipe delimiter
        var parts = mediaTemplate.Split('|');
        
        // First part is always the filename
        options.FileName = parts[0].Trim();
        
        // Parse additional parameters
        for (int i = 1; i < parts.Length; i++)
        {
            var param = parts[i].Trim();
            if (string.IsNullOrEmpty(param)) continue;
            
            var equalIndex = param.IndexOf('=');
            if (equalIndex == -1) continue;
            
            var key = param.Substring(0, equalIndex).Trim().ToLowerInvariant();
            var value = param.Substring(equalIndex + 1).Trim();
            
            switch (key)
            {
                case "size":
                    if (int.TryParse(value, out var size) && size > 0 && size <= 2000)
                        options.Size = size;
                    break;
                    
                case "width":
                    if (int.TryParse(value, out var width) && width > 0 && width <= 2000)
                        options.Width = width;
                    break;
                    
                case "height":
                    if (int.TryParse(value, out var height) && height > 0 && height <= 2000)
                        options.Height = height;
                    break;
                    
                case "align":
                    if (new[] { "left", "center", "right" }.Contains(value.ToLowerInvariant()))
                        options.Align = value.ToLowerInvariant();
                    break;
                    
                case "display":
                    if (new[] { "inline", "block", "thumb", "full" }.Contains(value.ToLowerInvariant()))
                        options.Display = value.ToLowerInvariant();
                    break;
                    
                case "alt":
                    options.Alt = value;
                    break;
                    
                case "caption":
                    options.Caption = value;
                    break;
                    
                case "class":
                    options.Class = value;
                    break;
            }
        }
        
        return options;
    }
    
    public int GetEffectiveSize()
    {
        if (Display == "thumb")
            return ThumbSize;
        if (Size.HasValue)
            return Size.Value;
        if (Width.HasValue)
            return Width.Value;
        return DefaultThumbnailSize;
    }
    
    public string GetSizeStyles()
    {
        var styles = new List<string>();
        
        if (Width.HasValue)
            styles.Add($"width: {Width}px");
        else if (Size.HasValue)
            styles.Add($"max-width: {Size}px");
        else if (Display == "thumb")
            styles.Add($"max-width: {ThumbSize}px");
        
        if (Height.HasValue)
            styles.Add($"height: {Height}px");
        else
            styles.Add("height: auto");
        
        return styles.Count > 0 ? string.Join("; ", styles) : "";
    }
    
    public string GetAlignmentClass()
    {
        return Align switch
        {
            "left" => "text-start",
            "center" => "text-center", 
            "right" => "text-end",
            _ => ""
        };
    }
    
    public string GetDisplayClasses()
    {
        var classes = new List<string> { "img-fluid", "rounded", "shadow-sm" };
        
        if (Display == "inline")
            classes.Add("d-inline");
        
        if (!string.IsNullOrEmpty(Class))
            classes.Add(Class);
        
        return string.Join(" ", classes);
    }
}
