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
    private readonly UserService _userService;

    public HistoryModel(AppDbContext context, MarkdownService markdownService, DiffService diffService, UserService userService)
    {
        _context = context;
        _markdownService = markdownService;
        _diffService = diffService;
        _userService = userService;
    }

    public new STWiki.Data.Entities.Page? Page { get; set; }
    public string Slug { get; set; } = string.Empty;
    public List<Revision> Revisions { get; set; } = new();
    public Dictionary<string, string> AuthorDisplayNames { get; set; } = new();
    public string RenderedContent { get; set; } = string.Empty;
    
    // Pagination properties
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalRevisions { get; set; }
    public int TotalPages { get; set; }
    
    // For revision viewing
    public Revision? SelectedRevision { get; set; }
    public string SelectedRevisionContent { get; set; } = string.Empty;
    
    // For diff viewing
    public Revision? CompareFromRevision { get; set; }
    public Revision? CompareToRevision { get; set; }
    public string DiffHtml { get; set; } = string.Empty;
    public string? DiffViewMode { get; set; }
    public string? DiffGranularity { get; set; }

    public async Task<IActionResult> OnGetAsync(string slug, int page = 1)
    {
        Slug = slug;
        CurrentPage = Math.Max(1, page);
        
        // Remove "/history" suffix from slug to get the actual page slug
        var pageSlug = slug.EndsWith("/history", StringComparison.OrdinalIgnoreCase) 
            ? slug.Substring(0, slug.Length - "/history".Length)
            : slug;
        
        Page = await _context.Pages
            .FirstOrDefaultAsync(p => p.Slug.ToLower() == pageSlug.ToLower());

        if (Page != null)
        {
            // Get total revision count for pagination
            TotalRevisions = await _context.Revisions
                .Where(r => r.PageId == Page.Id)
                .CountAsync();
                
            TotalPages = (int)Math.Ceiling((double)TotalRevisions / PageSize);
            
            // Ensure current page is valid
            if (CurrentPage > TotalPages && TotalPages > 0)
                CurrentPage = TotalPages;
            
            // Load paginated revisions for this page
            Revisions = await _context.Revisions
                .Where(r => r.PageId == Page.Id)
                .OrderByDescending(r => r.CreatedAt)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Render current content
            RenderedContent = Page.BodyFormat switch
            {
                "markdown" => _markdownService.RenderToHtml(Page.Body),
                "html" => Page.Body,
                _ => $"<pre>{Page.Body}</pre>"
            };

            // Load author display names
            await LoadAuthorDisplayNamesAsync();
        }

        return Page();
    }

    private async Task LoadAuthorDisplayNamesAsync()
    {
        if (!Revisions.Any()) return;

        var authorIds = Revisions.Select(r => r.Author).Distinct().ToList();
        AuthorDisplayNames.Clear();

        foreach (var authorId in authorIds)
        {
            var user = await _userService.GetUserByUserIdAsync(authorId);
            AuthorDisplayNames[authorId] = user?.DisplayName ?? authorId;
        }
    }

    public async Task<IActionResult> OnGetViewRevisionAsync(string slug, long revisionId, int page = 1)
    {
        Slug = slug;
        CurrentPage = Math.Max(1, page);
        
        // Remove "/history" suffix from slug to get the actual page slug
        var pageSlug = slug.EndsWith("/history", StringComparison.OrdinalIgnoreCase) 
            ? slug.Substring(0, slug.Length - "/history".Length)
            : slug;
        
        Page = await _context.Pages
            .FirstOrDefaultAsync(p => p.Slug.ToLower() == pageSlug.ToLower());

        if (Page == null)
            return NotFound();

        // Get total revision count for pagination
        TotalRevisions = await _context.Revisions
            .Where(r => r.PageId == Page.Id)
            .CountAsync();
            
        TotalPages = (int)Math.Ceiling((double)TotalRevisions / PageSize);
        
        // Ensure current page is valid
        if (CurrentPage > TotalPages && TotalPages > 0)
            CurrentPage = TotalPages;

        // Load paginated revisions for navigation
        Revisions = await _context.Revisions
            .Where(r => r.PageId == Page.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
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

        // Load author display names
        await LoadAuthorDisplayNamesAsync();

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

        // Remove "/history" suffix from slug to get the actual page slug
        var pageSlug = slug.EndsWith("/history", StringComparison.OrdinalIgnoreCase) 
            ? slug.Substring(0, slug.Length - "/history".Length)
            : slug;

        var page = await _context.Pages
            .FirstOrDefaultAsync(p => p.Slug.ToLower() == pageSlug.ToLower());

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

        return RedirectToPage("/Wiki/View", new { slug = pageSlug });
    }

    public async Task<IActionResult> OnGetDiffAsync(string slug, long fromRevisionId, long toRevisionId, int page = 1, 
        string viewMode = "unified", string granularity = "line", bool ignoreWhitespace = false, int contextLines = 3)
    {
        Slug = slug;
        CurrentPage = Math.Max(1, page);
        
        // Remove "/history" suffix from slug to get the actual page slug
        var pageSlug = slug.EndsWith("/history", StringComparison.OrdinalIgnoreCase) 
            ? slug.Substring(0, slug.Length - "/history".Length)
            : slug;
        
        Page = await _context.Pages
            .FirstOrDefaultAsync(p => p.Slug.ToLower() == pageSlug.ToLower());

        if (Page == null)
            return NotFound();

        // Get total revision count for pagination
        TotalRevisions = await _context.Revisions
            .Where(r => r.PageId == Page.Id)
            .CountAsync();
            
        TotalPages = (int)Math.Ceiling((double)TotalRevisions / PageSize);
        
        // Ensure current page is valid
        if (CurrentPage > TotalPages && TotalPages > 0)
            CurrentPage = TotalPages;

        // Load paginated revisions for navigation
        Revisions = await _context.Revisions
            .Where(r => r.PageId == Page.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        // Load the two revisions to compare
        CompareFromRevision = await _context.Revisions
            .FirstOrDefaultAsync(r => r.Id == fromRevisionId && r.PageId == Page.Id);

        CompareToRevision = await _context.Revisions
            .FirstOrDefaultAsync(r => r.Id == toRevisionId && r.PageId == Page.Id);

        if (CompareFromRevision != null && CompareToRevision != null)
        {
            DiffViewMode = viewMode;
            DiffGranularity = granularity;

            // Create diff options
            var options = new DiffOptions
            {
                Granularity = granularity switch
                {
                    "word" => STWiki.Services.DiffGranularity.Word,
                    "character" => STWiki.Services.DiffGranularity.Character,
                    _ => STWiki.Services.DiffGranularity.Line
                },
                ViewMode = viewMode switch
                {
                    "sidebyside" => STWiki.Services.DiffViewMode.SideBySide,
                    "inline" => STWiki.Services.DiffViewMode.Inline,
                    "stats" => STWiki.Services.DiffViewMode.Stats,
                    _ => STWiki.Services.DiffViewMode.Unified
                },
                IgnoreWhitespace = ignoreWhitespace,
                ContextLines = contextLines,
                ShowStats = true
            };

            try
            {
                // Try to get from cache first if available
                var cachedDiff = await (_diffService as IAdvancedDiffService)?.GetCachedDiffAsync(fromRevisionId, toRevisionId, options);
                
                STWiki.Services.DiffService.DiffResult? diffResult = null;
                if (cachedDiff != null && cachedDiff.Lines.Any())
                {
                    diffResult = cachedDiff;
                }
                else
                {
                    // Generate new diff
                    diffResult = await (_diffService as IAdvancedDiffService)?.GenerateAdvancedDiffAsync(
                        CompareFromRevision.Snapshot, 
                        CompareToRevision.Snapshot, 
                        options) ?? _diffService.GenerateLineDiff(CompareFromRevision.Snapshot, CompareToRevision.Snapshot);
                    
                    // Cache the result - handled internally by the service
                }

                // Render HTML based on view mode
                if (diffResult != null)
                {
                    DiffHtml = await (_diffService as IAdvancedDiffService)?.RenderDiffHtmlAsync(diffResult, options.ViewMode) 
                        ?? _diffService.GenerateHtmlDiff(CompareFromRevision.Snapshot, CompareToRevision.Snapshot);
                }
            }
            catch (Exception ex)
            {
                // Fallback to basic diff if advanced diff fails
                DiffHtml = _diffService.GenerateHtmlDiff(
                    CompareFromRevision.Snapshot, 
                    CompareToRevision.Snapshot);
            }
        }

        // Render current content for reference
        RenderedContent = Page.BodyFormat switch
        {
            "markdown" => _markdownService.RenderToHtml(Page.Body),
            "html" => Page.Body,
            _ => $"<pre>{Page.Body}</pre>"
        };

        // Load author display names
        await LoadAuthorDisplayNamesAsync();

        return Page();
    }
}