using System.Text;

namespace STWiki.Services;

public class DiffService
{
    public enum ChangeType
    {
        Unchanged,
        Added,
        Deleted,
        Modified
    }

    public class DiffLine
    {
        public ChangeType Type { get; set; }
        public string Content { get; set; } = "";
        public int LineNumber { get; set; }
    }

    public class DiffResult
    {
        public List<DiffLine> Lines { get; set; } = new();
        public int AddedLines { get; set; }
        public int DeletedLines { get; set; }
        public int ModifiedLines { get; set; }
    }

    public DiffResult GenerateLineDiff(string oldText, string newText)
    {
        var oldLines = SplitIntoLines(oldText);
        var newLines = SplitIntoLines(newText);
        
        var result = new DiffResult();
        var diffLines = new List<DiffLine>();

        // Simple line-by-line diff algorithm
        var maxLines = Math.Max(oldLines.Count, newLines.Count);
        
        for (int i = 0; i < maxLines; i++)
        {
            var oldLine = i < oldLines.Count ? oldLines[i] : null;
            var newLine = i < newLines.Count ? newLines[i] : null;

            if (oldLine != null && newLine != null)
            {
                if (oldLine == newLine)
                {
                    // Unchanged line
                    diffLines.Add(new DiffLine
                    {
                        Type = ChangeType.Unchanged,
                        Content = newLine,
                        LineNumber = i + 1
                    });
                }
                else
                {
                    // Modified line - show both old and new
                    diffLines.Add(new DiffLine
                    {
                        Type = ChangeType.Deleted,
                        Content = oldLine,
                        LineNumber = i + 1
                    });
                    diffLines.Add(new DiffLine
                    {
                        Type = ChangeType.Added,
                        Content = newLine,
                        LineNumber = i + 1
                    });
                    result.ModifiedLines++;
                }
            }
            else if (oldLine != null)
            {
                // Deleted line
                diffLines.Add(new DiffLine
                {
                    Type = ChangeType.Deleted,
                    Content = oldLine,
                    LineNumber = i + 1
                });
                result.DeletedLines++;
            }
            else if (newLine != null)
            {
                // Added line
                diffLines.Add(new DiffLine
                {
                    Type = ChangeType.Added,
                    Content = newLine,
                    LineNumber = i + 1
                });
                result.AddedLines++;
            }
        }

        result.Lines = diffLines;
        return result;
    }

    public string GenerateHtmlDiff(string oldText, string newText)
    {
        var diff = GenerateLineDiff(oldText, newText);
        var html = new StringBuilder();
        
        html.AppendLine("<div class=\"diff-container\">");
        html.AppendLine("<div class=\"diff-stats mb-3\">");
        html.AppendLine($"<span class=\"badge bg-success\">+{diff.AddedLines}</span> ");
        html.AppendLine($"<span class=\"badge bg-danger\">-{diff.DeletedLines}</span> ");
        if (diff.ModifiedLines > 0)
        {
            html.AppendLine($"<span class=\"badge bg-warning\">~{diff.ModifiedLines}</span>");
        }
        html.AppendLine("</div>");
        
        html.AppendLine("<div class=\"diff-content\" style=\"font-family: monospace; font-size: 14px;\">");
        
        foreach (var line in diff.Lines)
        {
            var cssClass = line.Type switch
            {
                ChangeType.Added => "diff-line-added",
                ChangeType.Deleted => "diff-line-deleted", 
                ChangeType.Modified => "diff-line-modified",
                _ => "diff-line-unchanged"
            };
            
            var prefix = line.Type switch
            {
                ChangeType.Added => "+ ",
                ChangeType.Deleted => "- ",
                _ => "  "
            };
            
            var style = line.Type switch
            {
                ChangeType.Added => "background-color: #d4edda; border-left: 3px solid #28a745;",
                ChangeType.Deleted => "background-color: #f8d7da; border-left: 3px solid #dc3545;",
                _ => "background-color: #f8f9fa;"
            };
            
            html.AppendLine($"<div class=\"{cssClass}\" style=\"{style} padding: 2px 8px; margin: 1px 0;\">");
            html.AppendLine($"<span class=\"line-number\" style=\"color: #6c757d; margin-right: 10px; user-select: none;\">{line.LineNumber:D3}</span>");
            html.AppendLine($"<span class=\"line-prefix\" style=\"color: #6c757d; margin-right: 5px;\">{prefix}</span>");
            html.AppendLine($"<span class=\"line-content\">{System.Web.HttpUtility.HtmlEncode(line.Content)}</span>");
            html.AppendLine("</div>");
        }
        
        html.AppendLine("</div>");
        html.AppendLine("</div>");
        
        return html.ToString();
    }

    private List<string> SplitIntoLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new List<string>();
            
        return text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
    }
}