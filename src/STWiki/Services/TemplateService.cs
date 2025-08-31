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


}
