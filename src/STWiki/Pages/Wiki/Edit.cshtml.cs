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
        Console.WriteLine($"============== EDIT ONGETASYNC START ==============");
        Console.WriteLine($"🎯 OnGetAsync called with slug: '{slug}'");
        Console.WriteLine($"============== THREAD: {System.Threading.Thread.CurrentThread.ManagedThreadId} ==============");
        
        if (string.IsNullOrEmpty(slug) || slug.Equals("new", StringComparison.OrdinalIgnoreCase))
        {
            // New page mode
            IsNew = true;
            Body = ""; // Initialize with empty string instead of null
            BodyFormat = "markdown";
            Slug = ""; // Ensure clean slug for new pages
            return Page();
        }

        // Edit existing page
        Console.WriteLine($"🎯 Querying database for slug: '{slug}'");
        var existingPage = await _context.Pages
            .FirstOrDefaultAsync(p => p.Slug.ToLower() == slug.ToLower());
        
        if (existingPage != null)
        {
            Console.WriteLine($"🎯 Raw from DB - Body length: {existingPage.Body?.Length}, Slug length: {existingPage.Slug?.Length}");
            Console.WriteLine($"🎯 Raw from DB - Body has BOM: {existingPage.Body?.StartsWith("\uFEFF") ?? false}");
            Console.WriteLine($"🎯 Raw from DB - Slug has BOM: {existingPage.Slug?.StartsWith("\uFEFF") ?? false}");
        }

        if (existingPage == null)
        {
            // Page doesn't exist - redirect to create it
            IsNew = true;
            Slug = CleanStringForBlazor(slug) ?? "";
            Title = slug.Replace("-", " ").ToTitleCase();
            Body = ""; // Initialize with empty string instead of null
            BodyFormat = "markdown";
            return Page();
        }

        // Load existing page for editing
        Console.WriteLine($"🎯 Loading existing page for editing: {existingPage.Slug}");
        IsNew = false;
        PageId = existingPage.Id;
        Title = existingPage.Title;
        Slug = CleanStringForBlazor(existingPage.Slug);
        Summary = existingPage.Summary;
        Body = CleanStringForBlazor(existingPage.Body);
        BodyFormat = existingPage.BodyFormat;
        CreatedAt = existingPage.CreatedAt;
        UpdatedAt = existingPage.UpdatedAt;
        UpdatedBy = existingPage.UpdatedBy;
        
        Console.WriteLine($"🎯 After cleaning - Body has BOM: {Body?.StartsWith("\uFEFF") ?? false}");
        Console.WriteLine($"🎯 After cleaning - Slug has BOM: {Slug?.StartsWith("\uFEFF") ?? false}");


        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? slug)
    {
        Console.WriteLine($"📝 FORM SUBMIT - OnPostAsync called with slug: '{slug}'");
        Console.WriteLine($"📝 FORM SUBMIT - Body length: {Body?.Length ?? -1}");
        Console.WriteLine($"📝 FORM SUBMIT - Title: '{Title}'");
        Console.WriteLine($"📝 FORM SUBMIT - BodyFormat: '{BodyFormat}'");
        
        if (Body?.Length > 50)
        {
            Console.WriteLine($"📝 FORM SUBMIT - Body preview: '{Body.Substring(0, Math.Min(50, Body.Length))}...'");
        }
        else
        {
            Console.WriteLine($"📝 FORM SUBMIT - Full Body: '{Body ?? "NULL"}'");
        }

        if (!ModelState.IsValid)
        {
            Console.WriteLine("❌ FORM SUBMIT - ModelState is invalid");
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

            Slug = CleanStringForBlazor(Slug);
            
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
                Body = CleanStringForBlazor(Body),
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
                Snapshot = CleanStringForBlazor(Body),
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
                Snapshot = CleanStringForBlazor(Body),
                Format = BodyFormat,
                CreatedAt = now
            };
            
            _context.Revisions.Add(revision);
            
            // Update page
            Console.WriteLine($"📝 FORM SUBMIT - Updating existing page ID: {existingPage.Id}");
            Console.WriteLine($"📝 FORM SUBMIT - Old body length: {existingPage.Body?.Length ?? -1}");
            Console.WriteLine($"📝 FORM SUBMIT - New body length: {Body?.Length ?? -1}");
            
            existingPage.Title = Title;
            existingPage.Summary = Summary ?? string.Empty;
            existingPage.Body = CleanStringForBlazor(Body);
            existingPage.BodyFormat = BodyFormat;
            existingPage.UpdatedAt = now;
            existingPage.UpdatedBy = currentUser;

            await _context.SaveChangesAsync();
            Console.WriteLine($"✅ FORM SUBMIT - Page updated successfully");

            return RedirectToPage("/Wiki/View", new { slug });
        }
    }

    /// <summary>
    /// Cleans string content to prevent Blazor HTML rendering issues.
    /// Removes BOM, null characters, and other problematic control characters.
    /// </summary>
    private static string? CleanStringForBlazor(string? input)
    {
        Console.WriteLine($"🧹 CleanStringForBlazor called with input length: {input?.Length ?? -1}");
        
        if (string.IsNullOrEmpty(input))
        {
            Console.WriteLine($"🧹 CleanStringForBlazor: input is null/empty, returning as-is");
            return input;
        }

        // Check for BOM before cleaning
        var hasBomBefore = input.StartsWith("\uFEFF");
        Console.WriteLine($"🧹 CleanStringForBlazor: input has BOM before cleaning: {hasBomBefore}");
        
        if (hasBomBefore)
        {
            var bomChars = input.Where(c => c == '\uFEFF').Count();
            Console.WriteLine($"🧹 CleanStringForBlazor: found {bomChars} BOM characters in input");
            
            // Show first few characters in hex
            var firstChars = input.Take(10).Select(c => $"U+{(int)c:X4}").ToArray();
            Console.WriteLine($"🧹 CleanStringForBlazor: first chars: [{string.Join(", ", firstChars)}]");
        }

        var cleaned = input
            .Replace("\uFEFF", "") // Remove BOM (Byte Order Mark)
            .Replace("\0", "")     // Remove null characters
            .Replace("\u0001", "") // Remove other problematic control chars
            .Replace("\u0002", "")
            .Replace("\u0003", "")
            .Replace("\u0004", "")
            .Replace("\u0005", "")
            .Replace("\u0006", "")
            .Replace("\u0007", "")
            .Replace("\u0008", ""); // Remove backspace, but keep \t, \n, \r

        // Check for BOM after cleaning
        var hasBomAfter = cleaned.StartsWith("\uFEFF");
        Console.WriteLine($"🧹 CleanStringForBlazor: output has BOM after cleaning: {hasBomAfter}");
        Console.WriteLine($"🧹 CleanStringForBlazor: input length {input.Length} -> output length {cleaned.Length}");
        
        if (hasBomAfter)
        {
            Console.WriteLine($"🧹 ❌ CleanStringForBlazor: BOM STILL PRESENT AFTER CLEANING!");
        }
        else if (hasBomBefore)
        {
            Console.WriteLine($"🧹 ✅ CleanStringForBlazor: Successfully removed BOM");
        }

        return cleaned;
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