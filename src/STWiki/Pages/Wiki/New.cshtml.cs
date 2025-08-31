using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;
using STWiki.Services;
using System.ComponentModel.DataAnnotations;

namespace STWiki.Pages.Wiki;

[Authorize(Policy = "RequireEditor")]
public class NewModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IPageHierarchyService _pageHierarchyService;

    public NewModel(AppDbContext context, IPageHierarchyService pageHierarchyService)
    {
        _context = context;
        _pageHierarchyService = pageHierarchyService;
    }

    [BindProperty]
    [Required, StringLength(255)]
    public string Title { get; set; } = string.Empty;

    [BindProperty]
    [StringLength(255)]
    public string? ParentPath { get; set; }

    [BindProperty]
    [StringLength(255)]
    public string? Slug { get; set; }

    [BindProperty]
    [StringLength(500)]
    public string? Summary { get; set; }

    [BindProperty]
    [Required]
    public string BodyFormat { get; set; } = "markdown";

    public IActionResult OnGet(string? parent = null)
    {
        // Initialize defaults
        BodyFormat = "markdown";
        
        // Set parent path if provided in query string
        if (!string.IsNullOrEmpty(parent))
        {
            ParentPath = parent;
        }
        
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var currentUser = User.Identity?.Name ?? "Unknown";
        var now = DateTime.UtcNow;

        // Generate slug if not provided
        if (string.IsNullOrWhiteSpace(Slug))
        {
            Slug = SlugService.GenerateSlug(Title);
        }

        if (string.IsNullOrWhiteSpace(Slug))
        {
            ModelState.AddModelError(nameof(Slug), "Unable to generate valid slug from title");
            return Page();
        }

        // Combine with parent path if provided
        string finalSlug = Slug;
        if (!string.IsNullOrWhiteSpace(ParentPath))
        {
            // Clean and validate parent path
            var cleanParentPath = ParentPath.Trim().Trim('/');
            if (!string.IsNullOrEmpty(cleanParentPath))
            {
                finalSlug = $"{cleanParentPath}/{Slug}";
            }
        }

        // Check if final slug already exists
        var existingPage = await _context.Pages
            .FirstOrDefaultAsync(p => p.Slug.ToLower() == finalSlug.ToLower());

        if (existingPage != null)
        {
            ModelState.AddModelError(nameof(Slug), "A page with this URL already exists");
            return Page();
        }

        // Get parent ID based on slug hierarchy
        var parentId = await _pageHierarchyService.GetParentIdFromSlugAsync(finalSlug);

        // Create new page with minimal placeholder content
        var placeholderContent = BodyFormat switch
        {
            "markdown" => $"# {Title}\n\nStart writing your content here...",
            "html" => $"<h1>{Title}</h1><p>Start writing your content here...</p>",
            _ => "Start writing your content here..."
        };

        var newPage = new STWiki.Data.Entities.Page
        {
            Id = Guid.NewGuid(),
            Slug = finalSlug,
            Title = Title,
            Summary = Summary ?? string.Empty,
            Body = placeholderContent,
            BodyFormat = BodyFormat,
            ParentId = parentId,
            CreatedAt = now,
            UpdatedAt = now,
            UpdatedBy = currentUser,
            IsLocked = false
        };

        _context.Pages.Add(newPage);
        
        // Create initial revision for new page
        var initialRevision = new Revision
        {
            PageId = newPage.Id,
            Author = currentUser,
            Note = "Initial page creation",
            Snapshot = placeholderContent,
            Format = BodyFormat,
            CreatedAt = now
        };
        
        _context.Revisions.Add(initialRevision);
        await _context.SaveChangesAsync();

        // Redirect to edit the content
        return Redirect($"/{finalSlug}/edit");
    }
}