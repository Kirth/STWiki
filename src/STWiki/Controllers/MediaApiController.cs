using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STWiki.Data.Entities;
using STWiki.Services;
using System.ComponentModel.DataAnnotations;

namespace STWiki.Controllers;

[ApiController]
[Route("api/media")]
[Authorize]
public class MediaApiController : ControllerBase
{
    private readonly IMediaService _mediaService;
    private readonly ILogger<MediaApiController> _logger;

    public MediaApiController(IMediaService mediaService, ILogger<MediaApiController> logger)
    {
        _mediaService = mediaService;
        _logger = logger;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(52428800)] // 50MB
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<MediaUploadResponse>> UploadFile(
        IFormFile file,
        [FromForm] string? description = null,
        [FromForm] string? altText = null)
    {
        if (file == null)
            return BadRequest(new { error = "No file provided" });

        var userId = User.Identity?.Name;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var result = await _mediaService.UploadFileAsync(file, description, altText, userId);

            if (!result.Success)
            {
                return BadRequest(new 
                { 
                    error = result.ErrorMessage ?? "Upload failed",
                    validationErrors = result.ValidationErrors
                });
            }

            return Ok(new MediaUploadResponse
            {
                Id = result.MediaFile!.Id,
                FileName = result.MediaFile.OriginalFileName,
                Url = Url.Action("GetFile", new { id = result.MediaFile.Id })!,
                ContentType = result.MediaFile.ContentType,
                FileSize = result.MediaFile.FileSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file {FileName}", file.FileName);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet]
    public async Task<ActionResult<MediaListResponse>> GetMedia(
        int page = 1,
        int pageSize = 20,
        string? search = null)
    {
        var userId = User.Identity?.Name;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            List<MediaFile> mediaFiles;
            
            if (!string.IsNullOrEmpty(search))
            {
                mediaFiles = await _mediaService.SearchMediaAsync(search, page, pageSize);
            }
            else
            {
                mediaFiles = await _mediaService.GetUserMediaAsync(userId, page, pageSize);
            }

            var response = new MediaListResponse
            {
                Items = mediaFiles.Select(m => new MediaItemResponse
                {
                    Id = m.Id,
                    FileName = m.OriginalFileName,
                    Description = m.Description,
                    ContentType = m.ContentType,
                    FileSize = m.FileSize,
                    UploadedAt = m.UploadedAt,
                    Url = Url.Action("GetFile", new { id = m.Id })!,
                    ThumbnailUrl = IsImage(m.ContentType) 
                        ? Url.Action("GetThumbnail", new { id = m.Id, size = 300 })
                        : null,
                    Width = m.Width,
                    Height = m.Height
                }).ToList(),
                Page = page,
                PageSize = pageSize,
                HasMore = mediaFiles.Count == pageSize
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get media files for user {UserId}", userId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetFile(Guid id, int? size = null)
    {
        try
        {
            var mediaFile = await _mediaService.GetMediaFileAsync(id);
            
            if (mediaFile == null)
                return NotFound();

            if (!mediaFile.IsPublic && !User.Identity?.IsAuthenticated == true)
                return Unauthorized();

            if (size.HasValue && IsImage(mediaFile.ContentType))
            {
                // Return thumbnail
                var thumbnailUrl = await _mediaService.GetMediaUrlAsync(id, size);
                if (!string.IsNullOrEmpty(thumbnailUrl))
                {
                    return Redirect(thumbnailUrl);
                }
            }

            // Return presigned URL for direct download
            var fileUrl = await _mediaService.GetMediaUrlAsync(id);
            if (string.IsNullOrEmpty(fileUrl))
                return NotFound();

            return Redirect(fileUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get media file {MediaFileId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("{id}/thumbnail")]
    [AllowAnonymous]
    public async Task<ActionResult> GetThumbnail(Guid id, int size = 300)
    {
        return await GetFile(id, size);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult> DeleteFile(Guid id)
    {
        var userId = User.Identity?.Name;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var success = await _mediaService.DeleteMediaAsync(id, userId);
            
            if (!success)
                return NotFound();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete media file {MediaFileId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult> UpdateFile(Guid id, [FromBody] MediaUpdateRequest request)
    {
        var userId = User.Identity?.Name;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var success = await _mediaService.UpdateMediaAsync(id, request.Description, request.AltText, userId);
            
            if (!success)
                return NotFound();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update media file {MediaFileId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("usage")]
    public async Task<ActionResult<MediaUsageReport>> GetUsage()
    {
        var userId = User.Identity?.Name;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var usage = await _mediaService.GetMediaUsageAsync(userId);
            return Ok(usage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get media usage for user {UserId}", userId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private static bool IsImage(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}

// Response DTOs
public class MediaUploadResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = "";
    public string Url { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long FileSize { get; set; }
}

public class MediaListResponse
{
    public List<MediaItemResponse> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}

public class MediaItemResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = "";
    public string Description { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long FileSize { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public string Url { get; set; } = "";
    public string? ThumbnailUrl { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}

public class MediaUpdateRequest
{
    public string? Description { get; set; }
    public string? AltText { get; set; }
}