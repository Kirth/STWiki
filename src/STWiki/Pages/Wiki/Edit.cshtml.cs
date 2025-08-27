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
public class EditModel : PageModel
{
    private readonly AppDbContext _context;

    public EditModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    [Required, StringLength(255)]
    public string Title { get; set; } = string.Empty;

    [BindProperty]
    [StringLength(255)]
    public string? Slug { get; set; }

    [BindProperty]
    [StringLength(500)]
    public string? Summary { get; set; }

    [BindProperty]
    [Required]
    public string Body { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    public string BodyFormat { get; set; } = "markdown";

    public bool IsNew { get; set; }
    public Guid? PageId { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public async Task<IActionResult> OnGetAsync(string? slug)
    {
        if (string.IsNullOrEmpty(slug) || slug.Equals("new", StringComparison.OrdinalIgnoreCase))
        {
            // New page mode
            IsNew = true;
            Body = ""; // Initialize with empty string instead of null
            BodyFormat = "markdown";
            return Page();
        }

        // Edit existing page
        var existingPage = await _context.Pages
            .FirstOrDefaultAsync(p => p.Slug.ToLower() == slug.ToLower());

        if (existingPage == null)
        {
            // Page doesn't exist - redirect to create it
            IsNew = true;
            Slug = slug;
            Title = slug.Replace("-", " ").ToTitleCase();
            Body = ""; // Initialize with empty string instead of null
            BodyFormat = "markdown";
            return Page();
        }

        // Load existing page for editing
        IsNew = false;
        PageId = existingPage.Id;
        Title = existingPage.Title;
        Slug = existingPage.Slug;
        Summary = existingPage.Summary;
        Body = existingPage.Body;
        BodyFormat = existingPage.BodyFormat;
        CreatedAt = existingPage.CreatedAt;
        UpdatedAt = existingPage.UpdatedAt;
        UpdatedBy = existingPage.UpdatedBy;

        // Debug logging for character analysis
        Console.WriteLine("=== EDIT PAGE MODEL - OnGetAsync ===");
        Console.WriteLine($"Slug: '{slug}'");
        Console.WriteLine($"PageId: {PageId}");
        Console.WriteLine($"Body length: {Body?.Length ?? 0}");
        Console.WriteLine($"BodyFormat: {BodyFormat}");
        
        if (!string.IsNullOrEmpty(Body))
        {
            var hasNonAscii = Body.Any(c => c > 127);
            var hasControlChars = Body.Any(c => char.IsControl(c) && c != '\t' && c != '\n' && c != '\r');
            Console.WriteLine($"Body has non-ASCII: {hasNonAscii}");
            Console.WriteLine($"Body has control chars: {hasControlChars}");
            
            if (hasNonAscii || hasControlChars)
            {
                var problematicChars = Body.Where(c => c > 127 || (char.IsControl(c) && c != '\t' && c != '\n' && c != '\r'))
                                          .Take(10)
                                          .Select(c => $"U+{(int)c:X4} ({c})")
                                          .ToArray();
                Console.WriteLine($"Problematic characters in Body: {string.Join(", ", problematicChars)}");
            }
        }
        
        Console.WriteLine("=== END EDIT PAGE MODEL DEBUG ===");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? slug)
    {
        if (!ModelState.IsValid)
        {
            IsNew = string.IsNullOrEmpty(slug) || slug.Equals("new", StringComparison.OrdinalIgnoreCase);
            return Page();
        }

        var currentUser = User.Identity?.Name ?? "Unknown";
        var now = DateTime.UtcNow;

        // Generate slug if creating new page (slug is empty or "new")
        if (string.IsNullOrEmpty(slug) || slug.Equals("new", StringComparison.OrdinalIgnoreCase))
        {
            IsNew = true;
            if (string.IsNullOrWhiteSpace(Slug))
            {
                Slug = SlugService.GenerateSlug(Title);
            }

            if (string.IsNullOrWhiteSpace(Slug))
            {
                ModelState.AddModelError(nameof(Slug), "Unable to generate valid slug from title");
                return Page();
            }

            // Check if slug already exists
            var existingPage = await _context.Pages
                .FirstOrDefaultAsync(p => p.Slug.ToLower() == Slug.ToLower());

            if (existingPage != null)
            {
                ModelState.AddModelError(nameof(Slug), "A page with this URL already exists");
                return Page();
            }

            // Create new page
            var newPage = new STWiki.Data.Entities.Page
            {
                Id = Guid.NewGuid(),
                Slug = Slug,
                Title = Title,
                Summary = Summary ?? string.Empty,
                Body = Body,
                BodyFormat = BodyFormat,
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
                Snapshot = Body,
                Format = BodyFormat,
                CreatedAt = now
            };
            
            _context.Revisions.Add(initialRevision);
            await _context.SaveChangesAsync();

            // Redirect to the actual generated slug, not "new"
            return RedirectToPage("/Wiki/View", new { slug = Slug });
        }
        else
        {
            // Update existing page
            var existingPage = await _context.Pages
                .FirstOrDefaultAsync(p => p.Slug.ToLower() == slug.ToLower());

            if (existingPage == null)
            {
                return NotFound();
            }

            // Create revision before updating the page
            var revision = new Revision
            {
                PageId = existingPage.Id,
                Author = currentUser,
                Note = Summary ?? "Updated page content",
                Snapshot = Body,
                Format = BodyFormat,
                CreatedAt = now
            };
            
            _context.Revisions.Add(revision);
            
            // Update page
            existingPage.Title = Title;
            existingPage.Summary = Summary ?? string.Empty;
            existingPage.Body = Body;
            existingPage.BodyFormat = BodyFormat;
            existingPage.UpdatedAt = now;
            existingPage.UpdatedBy = currentUser;

            await _context.SaveChangesAsync();

            return RedirectToPage("/Wiki/View", new { slug });
        }
    }
}

public static class StringExtensions
{
    public static string ToTitleCase(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i][1..].ToLower();
            }
        }
        return string.Join(" ", words);
    }
}