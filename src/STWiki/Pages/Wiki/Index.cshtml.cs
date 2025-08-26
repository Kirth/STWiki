using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;

namespace STWiki.Pages.Wiki;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public List<STWiki.Data.Entities.Page> RecentPages { get; set; } = new();

    public async Task OnGetAsync()
    {
        RecentPages = await _context.Pages
            .OrderByDescending(p => p.UpdatedAt)
            .Take(10)
            .ToListAsync();
    }
}