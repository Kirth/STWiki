using Markdig;

namespace STWiki.Services;

public class MarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
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
}