using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;
using STWiki.Services;
using System.ComponentModel.DataAnnotations;

namespace STWiki.Controllers;

[ApiController]
[Route("api/wiki")]
[Authorize]
public class WikiApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly MarkdownService _markdownService;
    private readonly ILogger<WikiApiController> _logger;

    public WikiApiController(AppDbContext context, MarkdownService markdownService, ILogger<WikiApiController> logger)
    {
        _context = context;
        _markdownService = markdownService;
        _logger = logger;
    }

    [HttpPost("{id}/autosave")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> AutoSave(Guid id, [FromBody] AutoSaveRequest request)
    {
        try
        {
            var page = await _context.Pages.FindAsync(id);
            if (page == null)
                return NotFound(new { error = "Page not found" });

            if (page.IsLocked)
                return BadRequest(new { error = "Page is locked for editing" });

            var currentUserId = User.Identity?.Name ?? "Anonymous";
            var contentToSave = request.Content;

            // Find or create user-specific draft
            var existingDraft = await _context.Drafts
                .FirstOrDefaultAsync(d => d.UserId == currentUserId && d.PageId == id);

            if (existingDraft != null)
            {
                // Update existing draft
                existingDraft.Content = contentToSave;
                existingDraft.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                // Create new draft for this user
                var newDraft = new Draft
                {
                    PageId = id,
                    UserId = currentUserId,
                    Content = contentToSave,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    BaseContent = page.Body // Store what the draft is based on
                };
                _context.Drafts.Add(newDraft);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Autosaved draft for page {PageId} by user {User}", id, currentUserId);

            return Ok(new { message = "Draft saved", timestamp = DateTimeOffset.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to autosave page {PageId}", id);
            return StatusCode(500, new { error = "Failed to save draft" });
        }
    }

    [HttpPost("{id}/commit")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> CommitChanges(Guid id, [FromBody] CommitRequest request)
    {
        try
        {
            var page = await _context.Pages
                .Include(p => p.Revisions)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (page == null)
                return NotFound(new { error = "Page not found" });

            if (page.IsLocked)
                return BadRequest(new { error = "Page is locked for editing" });

            var currentUserId = User.Identity?.Name ?? "Anonymous";
            var contentToCommit = request.Content;
            
            // Create a regular single-user revision
            var revision = new Revision
            {
                PageId = page.Id,
                Author = currentUserId,
                Note = request.Summary ?? "",
                Snapshot = contentToCommit,
                Format = page.BodyFormat,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.Revisions.Add(revision);

            // Update the page
            if (request.Title != null)
                page.Title = request.Title;
            page.Body = revision.Snapshot;  // This commits the content to live page
            page.Summary = revision.Note;
            page.UpdatedAt = revision.CreatedAt;
            page.UpdatedBy = revision.Author;
            
            // Delete user's draft since content is now committed
            var userDraft = await _context.Drafts
                .FirstOrDefaultAsync(d => d.UserId == currentUserId && d.PageId == id);
            if (userDraft != null)
            {
                _context.Drafts.Remove(userDraft);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Committed changes to page {PageId} by {User}", id, page.UpdatedBy);

            return Ok(new { 
                message = "Changes committed", 
                revisionId = revision.Id,
                timestamp = page.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit changes to page {PageId}", id);
            return StatusCode(500, new { error = "Failed to commit changes" });
        }
    }

    [HttpPost("{id}/discard-draft")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> DiscardDraft(Guid id)
    {
        try
        {
            var page = await _context.Pages.FindAsync(id);
            if (page == null)
                return NotFound(new { error = "Page not found" });

            if (page.IsLocked)
                return BadRequest(new { error = "Page is locked for editing" });

            var currentUserId = User.Identity?.Name ?? "Anonymous";

            // Find and delete user's draft
            var userDraft = await _context.Drafts
                .FirstOrDefaultAsync(d => d.UserId == currentUserId && d.PageId == id);

            if (userDraft == null)
                return BadRequest(new { error = "No draft to discard" });

            _context.Drafts.Remove(userDraft);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Discarded draft for page {PageId} by user {User}", id, currentUserId);

            return Ok(new { 
                message = "Draft discarded successfully", 
                timestamp = DateTimeOffset.UtcNow,
                content = page.Body  // Return the committed content (not the draft)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discard draft for page {PageId}", id);
            return StatusCode(500, new { error = "Failed to discard draft" });
        }
    }

    [HttpGet("lookup")]
    public async Task<IActionResult> LookupPages([FromQuery] string? slugs)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(slugs))
                return BadRequest(new { error = "slugs parameter is required" });

            var slugList = slugs.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (!slugList.Any())
                return BadRequest(new { error = "No valid slugs provided" });

            var pages = await _context.Pages
                .Where(p => slugList.Contains(p.Slug))
                .Select(p => new { p.Slug, p.Title })
                .ToDictionaryAsync(p => p.Slug, p => p.Title);

            return Ok(new { pages = pages });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to lookup pages");
            return StatusCode(500, new { error = "Failed to lookup pages" });
        }
    }

    [HttpPost("{id}/set-render-mode")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> SetRenderMode(Guid id, [FromBody] RenderModeRequest request)
    {
        try
        {
            var page = await _context.Pages.FindAsync(id);
            if (page == null)
                return NotFound(new { error = "Page not found" });

            if (!IsValidRenderMode(request.RenderMode))
                return BadRequest(new { error = "Invalid render mode. Allowed values: markdown, html" });

            page.BodyFormat = request.RenderMode;
            page.UpdatedAt = DateTimeOffset.UtcNow;
            page.UpdatedBy = User.Identity?.Name ?? "Anonymous";

            await _context.SaveChangesAsync();

            _logger.LogInformation("Changed render mode of page {PageId} to {RenderMode} by {User}", 
                id, request.RenderMode, page.UpdatedBy);

            return Ok(new { 
                message = "Render mode updated", 
                renderMode = page.BodyFormat,
                timestamp = page.UpdatedAt 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set render mode for page {PageId}", id);
            return StatusCode(500, new { error = "Failed to update render mode" });
        }
    }

    private static bool IsValidRenderMode(string renderMode)
    {
        return renderMode is "markdown" or "html";
    }
}

public class AutoSaveRequest
{
    [Required]
    public string Content { get; set; } = "";
}

public class CommitRequest
{
    [Required]
    public string Content { get; set; } = "";
    
    public string? Title { get; set; }
    
    [MaxLength(500)]
    public string? Summary { get; set; }
}

public class RenderModeRequest
{
    [Required]
    public string RenderMode { get; set; } = "";
}