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
    private readonly IPageHierarchyService _pageHierarchyService;

    public EditModel(AppDbContext context, IRedirectService redirectService, ActivityService activityService, IPageHierarchyService pageHierarchyService)
    {
        _context = context;
        _redirectService = redirectService;
        _activityService = activityService;
        _pageHierarchyService = pageHierarchyService;
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
    public STWiki.Data.Entities.User? UpdatedByUser { get; set; }

    // Original slug for tracking changes
    [BindProperty]
    public string? OriginalSlug { get; set; }

    // Page locking status
    public bool IsLocked { get; set; }

    // Slug segment properties for hierarchical editing
    [BindProperty]
    public string? ParentSlugPath { get; set; }
    
    [BindProperty]
    [StringLength(100)]
    public string? PageSlugSegment { get; set; }
    
    // Draft status properties
    public bool HasDraft { get; set; }
    public DateTimeOffset? LastDraftAt { get; set; }
    public DateTimeOffset? LastCommittedAt { get; set; }

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

        // Ensure slug doesn't end with reserved suffixes
        if (!string.IsNullOrEmpty(slug))
        {
            if (slug.EndsWith("/edit", StringComparison.OrdinalIgnoreCase))
            {
                slug = slug.Substring(0, slug.Length - 5);
                Console.WriteLine($"🎯 Removed /edit suffix from slug: '{slug}'");
            }
            if (slug.EndsWith("/history", StringComparison.OrdinalIgnoreCase))
            {
                slug = slug.Substring(0, slug.Length - 8);
                Console.WriteLine($"🎯 Removed /history suffix from slug: '{slug}'");
            }
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
            
            // Use the cleaned slug (already processed above) to extract segments
            ParentSlugPath = _pageHierarchyService.GetParentSlugPath(slug);
            PageSlugSegment = _pageHierarchyService.ExtractPageSlugFromFullPath(slug);
            Slug = PageSlugSegment; // Show only the page segment in the form
            
            // Clear model state to prevent interference with URL parameter values
            ModelState.Clear();
            
            Console.WriteLine($"🔍 DEBUG - New page ParentSlugPath: '{ParentSlugPath}'");
            Console.WriteLine($"🔍 DEBUG - New page PageSlugSegment: '{PageSlugSegment}'");
            Console.WriteLine($"🔍 DEBUG - New page Slug property set to: '{Slug}'");
            
            Title = PageSlugSegment.Replace("-", " ").ToTitleCase();
            Body = ""; // Initialize with empty string instead of null
            BodyFormat = "markdown";
            return Page();
        }

        // Load existing page for editing
        Console.WriteLine($"🎯 Loading existing page for editing: {existingPage.Slug}");
        IsNew = false;
        PageId = existingPage.Id;
        Title = existingPage.Title;
        OriginalSlug = existingPage.Slug; // Store original slug for change detection
        
        // Extract parent path and page segment from the existing page slug (from database, not URL)
        Console.WriteLine($"🔍 DEBUG - About to extract segments from existingPage.Slug: '{existingPage.Slug}'");
        ParentSlugPath = _pageHierarchyService.GetParentSlugPath(existingPage.Slug);
        PageSlugSegment = _pageHierarchyService.ExtractPageSlugFromFullPath(existingPage.Slug);
        Console.WriteLine($"🔍 DEBUG - Extracted ParentSlugPath: '{ParentSlugPath}'");
        Console.WriteLine($"🔍 DEBUG - Extracted PageSlugSegment: '{PageSlugSegment}'");
        
        Slug = PageSlugSegment; // Show only the page segment in the form
        
        // Clear model state to prevent interference with URL parameter values
        ModelState.Clear();
        
        Console.WriteLine($"🔍 DEBUG - Final Slug property set to: '{Slug}'");
        Console.WriteLine($"🔍 DEBUG - Original slug from DB: '{OriginalSlug}'");
        
        Summary = existingPage.Summary;
        
        var currentUserId = User.Identity?.Name ?? "Anonymous";
        
        // Check for user-specific draft
        var userDraft = await _context.Drafts
            .FirstOrDefaultAsync(d => d.UserId == currentUserId && d.PageId == existingPage.Id);
        
        // Use draft content if user has a draft, otherwise use committed content
        var contentToEdit = userDraft != null ? userDraft.Content : existingPage.Body;
            
        Console.WriteLine($"🔍 EDIT LOAD - Raw body from DB length: {existingPage.Body?.Length ?? -1}");
        Console.WriteLine($"🔍 EDIT LOAD - User draft content length: {userDraft?.Content?.Length ?? -1}");
        Console.WriteLine($"🔍 EDIT LOAD - Using content for editing: {(userDraft != null ? "USER DRAFT" : "COMMITTED")}");
        
        Body = CleanStringForBlazor(contentToEdit);
        Console.WriteLine($"🔍 EDIT LOAD - Final cleaned content length: {Body?.Length ?? -1}");
        BodyFormat = existingPage.BodyFormat;
        CreatedAt = existingPage.CreatedAt;
        UpdatedAt = existingPage.UpdatedAt;
        UpdatedBy = existingPage.UpdatedBy;
        IsLocked = existingPage.IsLocked;
        
        // Look up the user who last updated this page
        if (!string.IsNullOrEmpty(existingPage.UpdatedBy))
        {
            UpdatedByUser = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == existingPage.UpdatedBy);
        }
        
        // Set user-specific draft status
        HasDraft = userDraft != null;
        LastDraftAt = userDraft?.UpdatedAt;
        LastCommittedAt = existingPage.UpdatedAt;

        // Check if page is locked - still show the form but with warnings
        if (existingPage.IsLocked)
        {
            ViewData["IsPageLocked"] = true;
            ViewData["LockWarning"] = "This page is currently locked. You can view the content but cannot save changes.";
        }

        // Final debug check before returning to view
        Console.WriteLine($"🔍 FINAL CHECK - About to return to view with Slug = '{Slug}'");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? slug)
    {
        // Use route slug with fallback to OriginalSlug
        var routeSlug = slug ?? OriginalSlug;
        Console.WriteLine($"📝 FORM SUBMIT - OnPostAsync called with slug: '{slug}', using routeSlug: '{routeSlug}'");
        Console.WriteLine($"📝 FORM SUBMIT - Body length: {Body?.Length ?? -1}");
        Console.WriteLine($"📝 FORM SUBMIT - Title: '{Title}'");
        Console.WriteLine($"📝 FORM SUBMIT - BodyFormat: '{BodyFormat}'");
        Console.WriteLine($"📝 FORM SUBMIT - Form Slug field: '{Slug}' (page segment)");
        Console.WriteLine($"📝 FORM SUBMIT - PageSlugSegment: '{PageSlugSegment}'");
        Console.WriteLine($"📝 FORM SUBMIT - ParentSlugPath: '{ParentSlugPath}'");
        Console.WriteLine($"📝 FORM SUBMIT - OriginalSlug field: '{OriginalSlug}'");

        // Reconstruct full slug from parent path and page segment
        // Use Slug field (which contains the edited page segment) to create the full path
        string reconstructedSlug;
        
        Console.WriteLine($"🔧 RECONSTRUCTION DEBUG:");
        Console.WriteLine($"  - Slug (user input): '{Slug}'");
        Console.WriteLine($"  - PageSlugSegment (original): '{PageSlugSegment}'");
        Console.WriteLine($"  - ParentSlugPath: '{ParentSlugPath}'");
        
        if (!string.IsNullOrEmpty(Slug))
        {
            // User edited the slug field, use that as the page segment
            Console.WriteLine($"🔧 Using Slug field for reconstruction");
            reconstructedSlug = _pageHierarchyService.ConstructFullSlugPath(ParentSlugPath, Slug);
        }
        else if (!string.IsNullOrEmpty(PageSlugSegment))
        {
            // Fallback to original page segment
            Console.WriteLine($"🔧 Using PageSlugSegment for reconstruction");
            reconstructedSlug = _pageHierarchyService.ConstructFullSlugPath(ParentSlugPath, PageSlugSegment);
        }
        else
        {
            // No slug provided, will need to generate from title
            Console.WriteLine($"🔧 No slug provided, will generate from title");
            reconstructedSlug = string.Empty;
        }

        Console.WriteLine($"📝 FORM SUBMIT - Reconstructed full slug: '{reconstructedSlug}'");

        // Safety check to prevent regressions
        if (string.IsNullOrWhiteSpace(routeSlug))
        {
            // bail: we don't know which page to update
            ModelState.AddModelError("", "Missing page identifier.");
            IsNew = true; // Fallback to new page mode for display
            return Page();
        }

        // Update the Slug property to contain the full reconstructed path for the rest of the method
        var originalSlugInput = Slug; // Save the user's input
        Slug = reconstructedSlug;

        if (Body?.Length > 50)
        {
            Console.WriteLine($"📝 FORM SUBMIT - Body preview: '{Body.Substring(0, Math.Min(50, Body.Length))}...'");
        }
        else
        {
            Console.WriteLine($"📝 FORM SUBMIT - Full Body: '{Body ?? "NULL"}'");
        }

        // Validate slug doesn't end with reserved suffixes
        if (!string.IsNullOrEmpty(Slug))
        {
            if (Slug.EndsWith("/edit", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(Slug), "Slug cannot end with '/edit' - this is a reserved routing suffix");
            }
            if (Slug.EndsWith("/history", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(Slug), "Slug cannot end with '/history' - this is a reserved routing suffix");
            }
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
            
            // Restore the page segment view in the form (not the full path)
            if (!string.IsNullOrEmpty(originalSlugInput))
            {
                Slug = originalSlugInput; // Show the user's input, not the full reconstructed path
            }
            else
            {
                Slug = _pageHierarchyService.ExtractPageSlugFromFullPath(reconstructedSlug);
            }
            
            IsNew = string.IsNullOrEmpty(routeSlug) || routeSlug.Equals("new", StringComparison.OrdinalIgnoreCase);
            return Page();
        }

        var currentUser = User.Identity?.Name ?? "Unknown";
        var now = DateTime.UtcNow;


        if (string.IsNullOrEmpty(routeSlug) || routeSlug.Equals("new", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(OriginalSlug))
        {
            IsNew = true;
            if (string.IsNullOrWhiteSpace(Slug))
            {
                // Generate slug from title and reconstruct full path
                var generatedSegment = SlugService.GenerateSlug(Title);
                Slug = _pageHierarchyService.ConstructFullSlugPath(ParentSlugPath, generatedSegment);
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

            // Get parent ID based on slug hierarchy
            var parentId = await _pageHierarchyService.GetParentIdFromSlugAsync(Slug);

            // Create new page
            var newPage = new STWiki.Data.Entities.Page
            {
                Id = Guid.NewGuid(),
                Slug = Slug,
                Title = Title,
                Summary = Summary ?? string.Empty,
                Body = CleanStringForBlazor(Body),
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
            return Redirect($"/{Slug}");
        }
        else
        {
            // Update existing page - use OriginalSlug to find the existing record
            var existingPage = await _context.Pages
                .FirstOrDefaultAsync(p => p.Slug.ToLower() == OriginalSlug.ToLower());

            if (existingPage == null)
            {
                // Get parent ID based on slug hierarchy
                var parentId = await _pageHierarchyService.GetParentIdFromSlugAsync(routeSlug!);

                // Page doesn't exist, so create it using the slug from the URL
                var newPage = new STWiki.Data.Entities.Page
                {
                    Id = Guid.NewGuid(),
                    Slug = routeSlug!,
                    Title = Title,
                    Summary = Summary ?? string.Empty,
                    Body = CleanStringForBlazor(Body),
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
                return Redirect($"/{routeSlug}");
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

                // Update child page slugs BEFORE creating redirects and updating parent
                await _pageHierarchyService.UpdateChildrenSlugsAsync(existingPage, existingPage.Slug, Slug!);
                Console.WriteLine($"📝 Updated child page slugs for parent: {existingPage.Slug}");

                // Create redirect from old slug to new slug
                await _redirectService.CreateRedirectAsync(existingPage.Slug, Slug!);

                // Update any redirects that pointed to child pages under the old slug
                await _redirectService.UpdateChildRedirectsAsync(existingPage.Slug, Slug!);

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

            // Redirect to the new slug if it changed, otherwise the existing page slug
            var targetSlug = slugHasChanged ? Slug : existingPage.Slug;
            return Redirect($"/{targetSlug}");
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
        
        if (input?.Length > 100)
        {
            Console.WriteLine($"🧹 CleanStringForBlazor input preview: '{input.Substring(0, Math.Min(100, input.Length))}...'");
        }
        else if (!string.IsNullOrEmpty(input))
        {
            Console.WriteLine($"🧹 CleanStringForBlazor input full: '{input}'");
        }

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

        // Be more conservative - only remove the most problematic characters
        var cleaned = input
            .Replace("\uFEFF", "") // Remove BOM (Byte Order Mark)
            .Replace("\0", "");    // Remove null characters
            // Keep other characters as they might be legitimate content

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
        
        Console.WriteLine($"🧹 CleanStringForBlazor output length: {cleaned?.Length ?? -1}");
        
        if (cleaned?.Length != input?.Length)
        {
            Console.WriteLine($"🧹 ⚠️ CONTENT LENGTH CHANGED: {input?.Length ?? -1} → {cleaned?.Length ?? -1}");
        }
        
        if (cleaned?.Length > 100)
        {
            Console.WriteLine($"🧹 CleanStringForBlazor output preview: '{cleaned.Substring(0, Math.Min(100, cleaned.Length))}...'");
        }
        else if (!string.IsNullOrEmpty(cleaned))
        {
            Console.WriteLine($"🧹 CleanStringForBlazor output full: '{cleaned}'");
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
