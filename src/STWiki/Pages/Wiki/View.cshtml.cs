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

    public ViewModel(AppDbContext context, MarkdownService markdownService)
    {
        _context = context;
        _markdownService = markdownService;
    }

    public new STWiki.Data.Entities.Page? Page { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string RenderedContent { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        Slug = slug;
        
        Page = await _context.Pages
            .FirstOrDefaultAsync(p => p.Slug.ToLower() == slug.ToLower());

        if (Page != null)
        {
            // Render content based on format
            RenderedContent = Page.BodyFormat switch
            {
                "markdown" => _markdownService.RenderToHtml(Page.Body),
                "html" => Page.Body, // Raw HTML (should be sanitized in production)
                "prosemirror" => Page.Body, // ProseMirror JSON - for now treat as raw
                _ => $"<pre>{Page.Body}</pre>" // Plain text fallback
            };
        }

        return Page();
    }
}