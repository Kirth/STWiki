using Markdig;
using ReverseMarkdown;

namespace STWiki.Services;

public class MarkdownService
{
    private readonly MarkdownPipeline _pipeline;
    private readonly Converter _htmlToMarkdownConverter;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        _htmlToMarkdownConverter = new Converter();
    }

    public string RenderToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        return Markdown.ToHtml(markdown, _pipeline);
    }

    public async Task<string> RenderToHtmlAsync(string markdown, TemplateService? templateService = null)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        // Process templates first if template service is provided
        if (templateService != null)
        {
            markdown = await templateService.ProcessTemplatesAsync(markdown);
        }

        return Markdown.ToHtml(markdown, _pipeline);
    }

    public string ConvertHtmlToMarkdown(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        try
        {
            return _htmlToMarkdownConverter.Convert(html);
        }
        catch (Exception ex)
        {
            // Log the error and return a fallback
            Console.WriteLine($"HTML to Markdown conversion failed: {ex.Message}");
            return html; // Return original HTML as fallback
        }
    }

    public async Task<string> ConvertHtmlToMarkdownAsync(string html, TemplateService? templateService = null)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        // Process templates first if template service is provided
        string processedHtml = html;
        if (templateService != null)
        {
            processedHtml = await templateService.ProcessTemplatesAsync(html);
        }

        return ConvertHtmlToMarkdown(processedHtml);
    }
}