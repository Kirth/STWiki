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
    private readonly IRedirectService _redirectService;
    private readonly ActivityService _activityService;

    public EditModel(AppDbContext context, IRedirectService redirectService, ActivityService activityService)
    {
        _context = context;
        _redirectService = redirectService;
        _activityService = activityService;
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

    // Original slug for tracking changes
    public string? OriginalSlug { get; set; }

    // Page locking status
    public bool IsLocked { get; set; }

    public async Task<IActionResult> OnGetAsync(string? slug)
    {
        Console.WriteLine($"============== EDIT ONGETASYNC START ==============");
        Console.WriteLine($"🎯 OnGetAsync called with slug: '{slug}'");
        Console.WriteLine($"============== THREAD: {System.Threading.Thread.CurrentThread.ManagedThreadId} ==============");

        // Handle /edit suffix
        if (!string.IsNullOrEmpty(slug) && slug.EndsWith("/edit"))
        {
            slug = slug.Substring(0, slug.Length - 5); // Remove "/edit"
            Console.WriteLine($"🎯 Processed slug after removing /edit: '{slug}'");
        }


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
        OriginalSlug = existingPage.Slug; // Store original slug for change detection
        Summary = existingPage.Summary;
        Body = CleanStringForBlazor(existingPage.Body);
        BodyFormat = existingPage.BodyFormat;
        CreatedAt = existingPage.CreatedAt;
        UpdatedAt = existingPage.UpdatedAt;
        UpdatedBy = existingPage.UpdatedBy;
        IsLocked = existingPage.IsLocked;

        // Check if page is locked - still show the form but with warnings
        if (existingPage.IsLocked)
        {
            ViewData["IsPageLocked"] = true;
            ViewData["LockWarning"] = "This page is currently locked. You can view the content but cannot save changes.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? slug)
    {
        Console.WriteLine($"📝 FORM SUBMIT - OnPostAsync called with slug: '{slug}'");
        Console.WriteLine($"📝 FORM SUBMIT - Body length: {Body?.Length ?? -1}");
        Console.WriteLine($"📝 FORM SUBMIT - Title: '{Title}'");
        Console.WriteLine($"📝 FORM SUBMIT - BodyFormat: '{BodyFormat}'");
        Console.WriteLine($"📝 FORM SUBMIT - Form Slug field: '{Slug}'");
        Console.WriteLine($"📝 FORM SUBMIT - OriginalSlug field: '{OriginalSlug}'");

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
            // Debug: List validation errors
            foreach (var key in ModelState.Keys)
            {
                var state = ModelState[key];
                if (state != null && state.Errors.Count > 0)
                {
                    Console.WriteLine($"  🔍 Validation error for {key}: {string.Join(", ", state.Errors.Select(e => e.ErrorMessage))}");
                }
            }
            
            IsNew = string.IsNullOrEmpty(slug) || slug.Equals("new", StringComparison.OrdinalIgnoreCase);
            return Page();
        }

        var currentUser = User.Identity?.Name ?? "Unknown";
        var now = DateTime.UtcNow;


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

            // Log page creation activity
            await _activityService.LogPageCreatedAsync(
                currentUser, 
                currentUser, 
                newPage, 
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", 
                HttpContext.Request.Headers.UserAgent.ToString()
            );

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
                // Page doesn't exist, so create it using the slug from the URL
                var newPage = new STWiki.Data.Entities.Page
                {
                    Id = Guid.NewGuid(),
                    Slug = slug!,
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

                // Log page creation activity
                await _activityService.LogPageCreatedAsync(
                    currentUser, 
                    currentUser, 
                    newPage, 
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", 
                    HttpContext.Request.Headers.UserAgent.ToString()
                );

                // Redirect to the new page
                return RedirectToPage("/Wiki/View", new { slug = slug });
            }

            // Check if page is locked
            if (existingPage.IsLocked)
            {
                ModelState.AddModelError("", "This page is currently locked and cannot be edited.");
                IsNew = false;
                OriginalSlug = existingPage.Slug;
                return Page();
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

            // Check if slug has changed and create redirect if needed
            Console.WriteLine($"🔍 Slug comparison: existing='{existingPage.Slug}', new='{Slug}'");
            var slugHasChanged = !string.IsNullOrEmpty(Slug) &&
                                !string.Equals(existingPage.Slug, Slug, StringComparison.OrdinalIgnoreCase);
            
            Console.WriteLine($"🔍 Slug has changed: {slugHasChanged}");

            if (slugHasChanged)
            {
                // Validate new slug doesn't already exist
                var slugExists = await _context.Pages
                    .AnyAsync(p => p.Slug.ToLower() == Slug!.ToLower() && p.Id != existingPage.Id);

                if (slugExists)
                {
                    ModelState.AddModelError(nameof(Slug), "A page with this URL already exists");
                    IsNew = false;
                    OriginalSlug = existingPage.Slug;
                    return Page();
                }

                Console.WriteLine($"📝 Slug changed from '{existingPage.Slug}' to '{Slug}' - creating redirect");

                // Create redirect from old slug to new slug
                await _redirectService.CreateRedirectAsync(existingPage.Slug, Slug!);

                // Clean up any existing redirects that pointed to the old slug
                await _redirectService.CleanupRedirectChainAsync(existingPage.Slug);

                // Update the page slug
                existingPage.Slug = Slug!;
            }

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

            // Log page update activity
            await _activityService.LogPageUpdatedAsync(
                currentUser, 
                currentUser, 
                existingPage, 
                Summary ?? "Updated page content",
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", 
                HttpContext.Request.Headers.UserAgent.ToString()
            );

            // Redirect to the new slug if it changed, otherwise the original slug
            var targetSlug = slugHasChanged ? Slug : slug;
            return RedirectToPage("/Wiki/View", new { slug = targetSlug });
        }
    }

    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> OnPostToggleLockAsync(string slug)
    {
        var page = await _context.Pages
            .FirstOrDefaultAsync(p => p.Slug.ToLower() == slug.ToLower());

        if (page == null)
        {
            return NotFound();
        }

        // Toggle the lock status
        page.IsLocked = !page.IsLocked;
        page.UpdatedAt = DateTimeOffset.UtcNow;
        page.UpdatedBy = User.Identity?.Name ?? "Unknown";

        await _context.SaveChangesAsync();

        // Redirect back to edit page with status message
        TempData["StatusMessage"] = page.IsLocked
            ? "Page has been locked successfully."
            : "Page has been unlocked successfully.";

        return RedirectToPage("/Wiki/Edit", new { slug });
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
