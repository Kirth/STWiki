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
    private readonly IEditSessionService _editSessionService;
    private readonly ILogger<WikiApiController> _logger;

    public WikiApiController(AppDbContext context, MarkdownService markdownService, IEditSessionService editSessionService, ILogger<WikiApiController> logger)
    {
        _context = context;
        _markdownService = markdownService;
        _editSessionService = editSessionService;
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

            var pageIdStr = id.ToString();
            
            // Check if there's an active collaborative session
            var editSession = await _editSessionService.GetSessionAsync(pageIdStr);
            string contentToSave;
            
            if (editSession != null && editSession.ConnectedUsers.Any())
            {
                // Use the authoritative content from the collaboration session
                contentToSave = editSession.CurrentContent;
                _logger.LogInformation("Using collaborative session content for autosave on page {PageId}", id);
            }
            else
            {
                // Use the content from the request
                contentToSave = request.Content;
            }

            // For autosave, we don't create a revision - just update the page body
            page.Body = contentToSave;
            page.UpdatedAt = DateTimeOffset.UtcNow;
            page.UpdatedBy = User.Identity?.Name ?? "Anonymous";

            await _context.SaveChangesAsync();

            var message = editSession?.ConnectedUsers.Any() == true ? "Draft saved (collaborative)" : "Draft saved";
            _logger.LogInformation("Autosaved page {PageId} by {User}", id, page.UpdatedBy);

            return Ok(new { message = message, timestamp = page.UpdatedAt });
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

            var pageIdStr = id.ToString();
            var currentUser = User.Identity?.Name ?? "Anonymous";
            
            // Check if there's an active collaborative session
            var editSession = await _editSessionService.GetSessionAsync(pageIdStr);
            
            Revision revision;
            string contentToCommit;
            
            if (editSession != null && editSession.ConnectedUsers.Count > 1)
            {
                // This is a collaborative revision - use CollaborativeRevision
                contentToCommit = editSession.CurrentContent;
                
                var collaborativeRevision = new CollaborativeRevision
                {
                    PageId = page.Id,
                    Author = currentUser,
                    Note = request.Summary ?? "Collaborative edit",
                    Snapshot = contentToCommit,
                    Format = page.BodyFormat,
                    CreatedAt = DateTimeOffset.UtcNow,
                    OperationCount = editSession.OperationCounter,
                    CollaborationStart = editSession.CreatedAt,
                    CollaborationEnd = DateTimeOffset.UtcNow,
                    IsCollaborative = true
                };
                
                // Add all contributors
                var contributors = editSession.ConnectedUsers.Values
                    .Select(u => u.DisplayName)
                    .Distinct()
                    .ToList();
                
                // Also include the primary author if not already in the list
                if (!contributors.Contains(currentUser, StringComparer.OrdinalIgnoreCase))
                {
                    contributors.Add(currentUser);
                }
                
                collaborativeRevision.SetContributors(contributors);
                revision = collaborativeRevision;
                
                _logger.LogInformation("Creating collaborative revision for page {PageId} with {ContributorCount} contributors: {Contributors}", 
                    id, contributors.Count, string.Join(", ", contributors));
            }
            else
            {
                // Regular single-user revision
                contentToCommit = request.Content;
                
                revision = new Revision
                {
                    PageId = page.Id,
                    Author = currentUser,
                    Note = request.Summary ?? "",
                    Snapshot = contentToCommit,
                    Format = page.BodyFormat,
                    CreatedAt = DateTimeOffset.UtcNow
                };
            }

            _context.Revisions.Add(revision);

            // Update the page
            if (request.Title != null)
                page.Title = request.Title;
            page.Body = revision.Snapshot;
            page.Summary = revision.Note;
            page.UpdatedAt = revision.CreatedAt;
            page.UpdatedBy = revision.Author;

            await _context.SaveChangesAsync();

            // Clean up the collaborative session after successful commit
            if (editSession != null && editSession.ConnectedUsers.Count > 1)
            {
                await _editSessionService.RemoveSessionAsync(pageIdStr);
                _logger.LogInformation("Cleaned up collaborative session for page {PageId} after commit", id);
            }

            var message = revision is CollaborativeRevision ? "Collaborative changes committed" : "Changes committed";
            _logger.LogInformation("Committed changes to page {PageId} by {User}", id, page.UpdatedBy);

            return Ok(new { 
                message = message, 
                revisionId = revision.Id,
                timestamp = page.UpdatedAt,
                isCollaborative = revision is CollaborativeRevision
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