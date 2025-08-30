using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Services;

namespace STWiki.Pages.Wiki;

public class BreadcrumbPart
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class ViewModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly MarkdownService _markdownService;
    private readonly TemplateService _templateService;
    private readonly IRedirectService _redirectService;
    private readonly ActivityService _activityService;

    public ViewModel(AppDbContext context, MarkdownService markdownService, TemplateService templateService, IRedirectService redirectService, ActivityService activityService)
    {
        _context = context;
        _markdownService = markdownService;
        _templateService = templateService;
        _redirectService = redirectService;
        _activityService = activityService;
    }

    public new STWiki.Data.Entities.Page? Page { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string RenderedContent { get; set; } = string.Empty;
    public List<BreadcrumbPart> BreadcrumbParts { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        Slug = slug;
        
        Page = await _context.Pages
            .FirstOrDefaultAsync(p => p.Slug.ToLower() == slug.ToLower());

        // If page not found, check for redirects
        if (Page == null)
        {
            var redirectTarget = await _redirectService.GetRedirectTargetAsync(slug);
            if (!string.IsNullOrEmpty(redirectTarget))
            {
                // Redirect to the target slug with a 301 Moved Permanently
                return RedirectPermanent($"/{redirectTarget}");
            }
        }

        if (Page != null)
        {
            // Build breadcrumbs for hierarchical pages
            await BuildBreadcrumbsAsync(slug);
            
            // Render content based on format
            RenderedContent = Page.BodyFormat switch
            {
                "markdown" => await _markdownService.RenderToHtmlAsync(Page.Body, _templateService),
                "html" => await _templateService.ProcessTemplatesAsync(Page.Body), // Process templates in HTML too
                _ => $"<pre>{Page.Body}</pre>" // Plain text fallback
            };

            // Log page view activity (only for authenticated users to avoid spam)
            if (User.Identity?.IsAuthenticated == true)
            {
                var currentUser = User.Identity.Name ?? "Unknown";
                await _activityService.LogPageViewedAsync(
                    currentUser, 
                    currentUser, 
                    Page, 
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", 
                    HttpContext.Request.Headers.UserAgent.ToString()
                );
            }
        }

        return Page();
    }
    
    private async Task BuildBreadcrumbsAsync(string slug)
    {
        var parts = slug.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1) return; // No breadcrumbs needed for top-level pages
        
        BreadcrumbParts.Clear();
        
        var currentPath = "";
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0) currentPath += "/";
            currentPath += parts[i];
            
            var page = await _context.Pages
                .FirstOrDefaultAsync(p => p.Slug.ToLower() == currentPath.ToLower());
            
            BreadcrumbParts.Add(new BreadcrumbPart
            {
                Slug = currentPath,
                Title = page?.Title ?? parts[i].Replace("-", " ").Replace("_", " ")
            });
        }
    }
}