using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;
using System.ComponentModel.DataAnnotations;

namespace STWiki.Controllers;

[ApiController]
[Route("api/wiki")]
[Authorize]
public class WikiApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<WikiApiController> _logger;

    public WikiApiController(AppDbContext context, ILogger<WikiApiController> logger)
    {
        _context = context;
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

            // For autosave, we don't create a revision - just update the page body
            page.Body = request.Content;
            page.UpdatedAt = DateTimeOffset.UtcNow;
            page.UpdatedBy = User.Identity?.Name ?? "Anonymous";

            await _context.SaveChangesAsync();

            _logger.LogInformation("Autosaved page {PageId} by {User}", id, page.UpdatedBy);

            return Ok(new { message = "Draft saved", timestamp = page.UpdatedAt });
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

            // Create a new revision
            var revision = new Revision
            {
                PageId = page.Id,
                Author = User.Identity?.Name ?? "Anonymous",
                Note = request.Summary ?? "",
                Snapshot = request.Content,
                Format = page.BodyFormat,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.Revisions.Add(revision);

            // Update the page
            if (request.Title != null)
                page.Title = request.Title;
            page.Body = revision.Snapshot;
            page.Summary = revision.Note;
            page.UpdatedAt = revision.CreatedAt;
            page.UpdatedBy = revision.Author;

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