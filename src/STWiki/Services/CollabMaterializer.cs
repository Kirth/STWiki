using System.Text;
using System.Text.Json;

namespace STWiki.Services;

public class CollabMaterializer : ICollabMaterializer
{
    private readonly ILogger<CollabMaterializer> _logger;

    public CollabMaterializer(ILogger<CollabMaterializer> logger)
    {
        _logger = logger;
    }

    public (string Title, string Summary, string Body, string BodyFormat) Materialize(byte[] snapshotBytes)
    {
        try
        {
            // For now, implement a simple JSON-based block structure
            // TODO: In production, this would decode Yjs document format
            
            if (snapshotBytes == null || snapshotBytes.Length == 0)
            {
                return ("", "", "", "markdown");
            }

            var json = Encoding.UTF8.GetString(snapshotBytes);
            var document = JsonSerializer.Deserialize<JsonElement>(json);
            
            var blocks = new List<string>();
            string title = "";
            string summary = "";

            if (document.TryGetProperty("blocks", out var blocksElement) && blocksElement.ValueKind == JsonValueKind.Array)
            {
                bool isFirstBlock = true;
                foreach (var block in blocksElement.EnumerateArray())
                {
                    if (block.TryGetProperty("type", out var typeElement) && 
                        block.TryGetProperty("text", out var textElement))
                    {
                        var blockType = typeElement.GetString();
                        var text = textElement.GetString() ?? "";
                        
                        if (isFirstBlock && blockType == "heading")
                        {
                            title = text;
                            isFirstBlock = false;
                        }
                        else if (string.IsNullOrEmpty(summary) && blockType == "paragraph" && !string.IsNullOrEmpty(text))
                        {
                            summary = text.Length > 500 ? text.Substring(0, 500) : text;
                        }

                        switch (blockType)
                        {
                            case "heading":
                                blocks.Add($"## {text}");
                                break;
                            case "paragraph":
                                blocks.Add(text);
                                break;
                            case "code":
                                blocks.Add($"```\n{text}\n```");
                                break;
                            default:
                                blocks.Add(text);
                                break;
                        }
                    }
                }
            }
            else
            {
                // Fallback: treat as plain text
                blocks.Add(json);
            }

            var body = string.Join("\n\n", blocks);
            
            return (title, summary, body, "markdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to materialize CRDT snapshot");
            
            // Fallback to raw bytes as text
            var fallback = Encoding.UTF8.GetString(snapshotBytes);
            return ("", "", fallback, "markdown");
        }
    }
}