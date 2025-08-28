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

    public ViewModel(AppDbContext context, MarkdownService markdownService, TemplateService templateService)
    {
        _context = context;
        _markdownService = markdownService;
        _templateService = templateService;
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
                "markdown" => await _markdownService.RenderToHtmlAsync(Page.Body, _templateService),
                "html" => await _templateService.ProcessTemplatesAsync(Page.Body), // Process templates in HTML too
                _ => $"<pre>{Page.Body}</pre>" // Plain text fallback
            };
        }

        return Page();
    }
}