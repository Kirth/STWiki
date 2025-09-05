using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Services;

namespace STWiki.Pages.Wiki;

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
    public STWiki.Data.Entities.User? UpdatedByUser { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string RenderedContent { get; set; } = string.Empty;
    
    // Draft status properties
    public bool HasDraft { get; set; }
    public DateTimeOffset? LastDraftAt { get; set; }
    public DateTimeOffset? LastCommittedAt { get; set; }

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
            // Set draft status properties
            HasDraft = Page.HasUncommittedChanges;
            LastDraftAt = Page.LastDraftAt;
            LastCommittedAt = Page.LastCommittedAt;
            
            // Look up the user who last updated this page
            if (!string.IsNullOrEmpty(Page.UpdatedBy))
            {
                UpdatedByUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserId == Page.UpdatedBy);
            }

            // Render content based on format
            RenderedContent = Page.BodyFormat switch
            {
                "markdown" => await _markdownService.RenderToHtmlAsync(Page.Body, _templateService, Page),
                "html" => await _templateService.ProcessTemplatesAsync(Page.Body, Page), // Process templates in HTML too
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
}