using Microsoft.EntityFrameworkCore;
using STWiki.Data;
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

    public async Task<string> ProcessTemplatesAsync(string content)
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

            if (!recentPages.Any())
            {
                return @"<div class=""alert alert-info"">
                    <i class=""bi bi-info-circle""></i> No recent pages found.
                </div>";
            }

            var html = @"<div class=""card shadow-sm mb-4"">
    <div class=""card-header bg-light"">
        <h3 class=""card-title mb-0"">
            <i class=""bi bi-clock-history text-primary""></i> Recent Activity
        </h3>
    </div>
    <div class=""card-body p-0"">
        <div class=""list-group list-group-flush"">";

            foreach (var page in recentPages)
            {
                html += $@"
            <div class=""list-group-item list-group-item-action d-flex justify-content-between align-items-center"">
                <div>
                    <h6 class=""mb-1"">
                        <a href=""/{page.Slug}"" class=""text-decoration-none"">
                            <i class=""bi bi-file-text text-muted me-2""></i>{System.Web.HttpUtility.HtmlEncode(page.Title)}
                        </a>
                    </h6>
                    <small class=""text-muted"">
                        <i class=""bi bi-calendar3""></i> Updated {page.UpdatedAt:MMM dd, yyyy 'at' HH:mm}
                    </small>
                </div>
                <span class=""badge bg-primary rounded-pill"">View</span>
            </div>";
            }

            html += @"
        </div>
    </div>
</div>";

            return html;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render recent pages template");
            return @"<div class=""alert alert-danger"">
                <i class=""bi bi-exclamation-triangle""></i> Error loading recent pages.
            </div>";
        }
    }
}