using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;
using STWiki.Services;

namespace STWiki.Pages.Wiki;

public class HistoryModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly MarkdownService _markdownService;
    private readonly DiffService _diffService;

    public HistoryModel(AppDbContext context, MarkdownService markdownService, DiffService diffService)
    {
        _context = context;
        _markdownService = markdownService;
        _diffService = diffService;
    }

    public new STWiki.Data.Entities.Page? Page { get; set; }
    public string Slug { get; set; } = string.Empty;
    public List<Revision> Revisions { get; set; } = new();
    public string RenderedContent { get; set; } = string.Empty;
    
    // For revision viewing
    public Revision? SelectedRevision { get; set; }
    public string SelectedRevisionContent { get; set; } = string.Empty;
    
    // For diff viewing
    public Revision? CompareFromRevision { get; set; }
    public Revision? CompareToRevision { get; set; }
    public string DiffHtml { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        Slug = slug;
        
        Page = await _context.Pages
            .FirstOrDefaultAsync(p => p.Slug.ToLower() == slug.ToLower());

        if (Page != null)
        {
            // Load revisions for this page
            Revisions = await _context.Revisions
                .Where(r => r.PageId == Page.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // Render current content
            RenderedContent = Page.BodyFormat switch
            {
                "markdown" => _markdownService.RenderToHtml(Page.Body),
                "html" => Page.Body,
                _ => $"<pre>{Page.Body}</pre>"
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnGetViewRevisionAsync(string slug, long revisionId)
    {
        Slug = slug;
        
        Page = await _context.Pages
            .FirstOrDefaultAsync(p => p.Slug.ToLower() == slug.ToLower());

        if (Page == null)
            return NotFound();

        // Load all revisions for navigation
        Revisions = await _context.Revisions
            .Where(r => r.PageId == Page.Id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        // Load selected revision
        SelectedRevision = await _context.Revisions
            .FirstOrDefaultAsync(r => r.Id == revisionId && r.PageId == Page.Id);

        if (SelectedRevision != null)
        {
            // Render the revision content based on format
            SelectedRevisionContent = SelectedRevision.Format switch
            {
                "markdown" => _markdownService.RenderToHtml(SelectedRevision.Snapshot),
                "html" => SelectedRevision.Snapshot,
                _ => $"<pre>{System.Web.HttpUtility.HtmlEncode(SelectedRevision.Snapshot)}</pre>"
            };
        }

        // Render current content for comparison
        RenderedContent = Page.BodyFormat switch
        {
            "markdown" => _markdownService.RenderToHtml(Page.Body),
            "html" => Page.Body,
            _ => $"<pre>{Page.Body}</pre>"
        };

        return Page();
    }

    public async Task<IActionResult> OnPostRestoreAsync(string slug, long revisionId)
    {
        // Check authorization - only editors/admins can restore revisions
        if (!User.Identity?.IsAuthenticated == true || 
            (!User.HasClaim("groups", "stwiki-editor") && 
             !User.HasClaim("groups", "stwiki-admin") &&
             !User.HasClaim("email", "admin@example.com") &&
             !User.HasClaim("email", "editor@example.com")))
        {
            return Forbid();
        }

        var page = await _context.Pages
            .FirstOrDefaultAsync(p => p.Slug.ToLower() == slug.ToLower());

        if (page == null)
            return NotFound();

        var revision = await _context.Revisions
            .FirstOrDefaultAsync(r => r.Id == revisionId && r.PageId == page.Id);

        if (revision == null)
            return NotFound();

        var currentUser = User.Identity?.Name ?? "Unknown";
        var now = DateTimeOffset.UtcNow;

        // Create a new revision for the restore action
        var restoreRevision = new Revision
        {
            PageId = page.Id,
            Author = currentUser,
            Note = $"Restored from revision {revisionId} ({revision.CreatedAt:yyyy-MM-dd HH:mm})",
            Snapshot = revision.Snapshot,
            Format = revision.Format,
            CreatedAt = now
        };

        _context.Revisions.Add(restoreRevision);

        // Update the page with restored content
        page.Body = revision.Snapshot;
        page.BodyFormat = revision.Format;
        page.UpdatedAt = now;
        page.UpdatedBy = currentUser;

        await _context.SaveChangesAsync();

        return RedirectToPage("/Wiki/View", new { slug });
    }

    public async Task<IActionResult> OnGetDiffAsync(string slug, long fromRevisionId, long toRevisionId)
    {
        Slug = slug;
        
        Page = await _context.Pages
            .FirstOrDefaultAsync(p => p.Slug.ToLower() == slug.ToLower());

        if (Page == null)
            return NotFound();

        // Load all revisions for navigation
        Revisions = await _context.Revisions
            .Where(r => r.PageId == Page.Id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        // Load the two revisions to compare
        CompareFromRevision = await _context.Revisions
            .FirstOrDefaultAsync(r => r.Id == fromRevisionId && r.PageId == Page.Id);

        CompareToRevision = await _context.Revisions
            .FirstOrDefaultAsync(r => r.Id == toRevisionId && r.PageId == Page.Id);

        if (CompareFromRevision != null && CompareToRevision != null)
        {
            // Generate diff HTML
            DiffHtml = _diffService.GenerateHtmlDiff(
                CompareFromRevision.Snapshot, 
                CompareToRevision.Snapshot);
        }

        // Render current content for reference
        RenderedContent = Page.BodyFormat switch
        {
            "markdown" => _markdownService.RenderToHtml(Page.Body),
            "html" => Page.Body,
            _ => $"<pre>{Page.Body}</pre>"
        };

        return Page();
    }
}