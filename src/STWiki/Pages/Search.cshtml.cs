using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;

namespace STWiki.Pages;

public class SearchModel : PageModel
{
    private readonly AppDbContext _context;

    public SearchModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }
    
    public List<STWiki.Data.Entities.Page> Results { get; set; } = new();

    public async Task OnGetAsync()
    {
        if (!string.IsNullOrWhiteSpace(Query))
        {
            var searchTerm = Query.Trim();
            
            // Search in both title and content using LIKE (case-insensitive)
            Results = await _context.Pages
                .Where(p => 
                    EF.Functions.ILike(p.Title, $"%{searchTerm}%") ||
                    EF.Functions.ILike(p.Body, $"%{searchTerm}%") ||
                    EF.Functions.ILike(p.Summary, $"%{searchTerm}%"))
                .OrderByDescending(p => p.UpdatedAt)
                .Take(50) // Limit results to prevent performance issues
                .ToListAsync();
        }
    }
}