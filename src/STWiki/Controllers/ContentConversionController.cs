using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STWiki.Services;
using System.ComponentModel.DataAnnotations;

namespace STWiki.Controllers;

[ApiController]
[Route("api/convert-content")]
[Authorize(Policy = "RequireEditor")]
public class ContentConversionController : ControllerBase
{
    private readonly MarkdownService _markdownService;
    private readonly ILogger<ContentConversionController> _logger;

    public ContentConversionController(MarkdownService markdownService, ILogger<ContentConversionController> logger)
    {
        _markdownService = markdownService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> ConvertContent([FromBody] ConvertContentRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Content?.Trim()))
            {
                return Ok(new ConvertContentResponse
                {
                    Success = true,
                    ConvertedContent = "",
                    Message = "Empty content - no conversion needed"
                });
            }

            if (request.FromFormat == request.ToFormat)
            {
                return Ok(new ConvertContentResponse
                {
                    Success = true,
                    ConvertedContent = request.Content,
                    Message = "Same format - no conversion needed"
                });
            }

            string convertedContent;

            if (request.FromFormat == "markdown" && request.ToFormat == "html")
            {
                // Convert Markdown to HTML
                convertedContent = await _markdownService.RenderToHtmlAsync(request.Content);
            }
            else if (request.FromFormat == "html" && request.ToFormat == "markdown")
            {
                // Convert HTML to Markdown
                convertedContent = await _markdownService.ConvertHtmlToMarkdownAsync(request.Content);
            }
            else
            {
                return BadRequest(new ConvertContentResponse
                {
                    Success = false,
                    Error = $"Unsupported conversion from {request.FromFormat} to {request.ToFormat}"
                });
            }

            _logger.LogInformation("Successfully converted content from {FromFormat} to {ToFormat} ({Length} chars)", 
                request.FromFormat, request.ToFormat, request.Content.Length);

            return Ok(new ConvertContentResponse
            {
                Success = true,
                ConvertedContent = convertedContent,
                Message = $"Successfully converted from {request.FromFormat} to {request.ToFormat}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert content from {FromFormat} to {ToFormat}", 
                request.FromFormat, request.ToFormat);

            return Ok(new ConvertContentResponse
            {
                Success = false,
                Error = "Content conversion failed: " + ex.Message
            });
        }
    }
}

public class ConvertContentRequest
{
    [Required]
    public string Content { get; set; } = "";

    [Required]
    public string FromFormat { get; set; } = "";

    [Required]
    public string ToFormat { get; set; } = "";
}

public class ConvertContentResponse
{
    public bool Success { get; set; }
    public string ConvertedContent { get; set; } = "";
    public string? Message { get; set; }
    public string? Error { get; set; }
}