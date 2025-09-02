using System.Text;
using STWiki.Services.Diff;

namespace STWiki.Services;

public interface IAdvancedDiffService
{
    Task<DiffService.DiffResult> GenerateAdvancedDiffAsync(string oldContent, string newContent, DiffOptions options);
    Task<DiffService.DiffResult> GetCachedDiffAsync(long fromRevisionId, long toRevisionId, DiffOptions options);
    Task<string> RenderDiffHtmlAsync(DiffService.DiffResult diff, DiffViewMode viewMode);
    void InvalidateCache(long pageId);
}

public class DiffService : IAdvancedDiffService
{
    private readonly IDiffCacheService? _cacheService;
    private readonly ILogger<DiffService>? _logger;

    public DiffService(IDiffCacheService? cacheService = null, ILogger<DiffService>? logger = null)
    {
        _cacheService = cacheService;
        _logger = logger;
    }
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

    public async Task<DiffService.DiffResult> GenerateAdvancedDiffAsync(string oldContent, string newContent, DiffOptions options)
    {
        var result = new DiffResult();
        
        try
        {
            switch (options.Granularity)
            {
                case DiffGranularity.Line:
                    return GenerateMyersDiff(oldContent, newContent, options);
                    
                case DiffGranularity.Word:
                    var wordDiff = WordLevelDiff.ComputeWordDiff(oldContent, newContent, options.ContextLines);
                    return ConvertWordDiffToResult(wordDiff);
                    
                case DiffGranularity.Character:
                    return GenerateCharacterDiff(oldContent, newContent, options);
                    
                default:
                    return GenerateLineDiff(oldContent, newContent);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error generating advanced diff");
            return GenerateLineDiff(oldContent, newContent);
        }
    }

    public async Task<DiffService.DiffResult> GetCachedDiffAsync(long fromRevisionId, long toRevisionId, DiffOptions options)
    {
        if (_cacheService == null)
            return new DiffResult();

        var cacheKey = _cacheService.GenerateDiffKey(fromRevisionId, toRevisionId, options);
        return await _cacheService.GetCachedDiffAsync<DiffResult>(cacheKey) ?? new DiffResult();
    }

    public async Task<string> RenderDiffHtmlAsync(DiffService.DiffResult diff, DiffViewMode viewMode)
    {
        return viewMode switch
        {
            DiffViewMode.Unified => RenderUnifiedDiffHtml(diff),
            DiffViewMode.SideBySide => RenderSideBySideDiffHtml(diff),
            DiffViewMode.Inline => RenderInlineDiffHtml(diff),
            DiffViewMode.Stats => RenderStatsDiffHtml(diff),
            _ => RenderUnifiedDiffHtml(diff)
        };
    }

    public void InvalidateCache(long pageId)
    {
        _cacheService?.InvalidateDiffCacheAsync(pageId);
    }

    private DiffService.DiffResult GenerateMyersDiff(string oldContent, string newContent, DiffOptions options)
    {
        var oldLines = SplitIntoLines(oldContent);
        var newLines = SplitIntoLines(newContent);
        
        var myersDiff = MyersDiffAlgorithm.Compute(oldLines.ToArray(), newLines.ToArray());
        
        var result = new DiffResult();
        var diffLines = new List<DiffLine>();
        
        foreach (var item in myersDiff)
        {
            var changeType = item.Operation switch
            {
                MyersDiffAlgorithm.DiffOperationType.Equal => ChangeType.Unchanged,
                MyersDiffAlgorithm.DiffOperationType.Delete => ChangeType.Deleted,
                MyersDiffAlgorithm.DiffOperationType.Insert => ChangeType.Added,
                _ => ChangeType.Unchanged
            };
            
            diffLines.Add(new DiffLine
            {
                Type = changeType,
                Content = item.Text,
                LineNumber = item.LineNumber
            });
            
            switch (changeType)
            {
                case ChangeType.Added:
                    result.AddedLines++;
                    break;
                case ChangeType.Deleted:
                    result.DeletedLines++;
                    break;
            }
        }
        
        result.Lines = diffLines;
        return result;
    }

    private DiffService.DiffResult ConvertWordDiffToResult(WordLevelDiff.WordDiffResult wordDiff)
    {
        var result = new DiffResult
        {
            AddedLines = wordDiff.AddedWords,
            DeletedLines = wordDiff.DeletedWords,
            ModifiedLines = wordDiff.ModifiedLines
        };
        
        var currentLine = new StringBuilder();
        var currentLineNumber = 1;
        var currentChangeType = ChangeType.Unchanged;
        
        foreach (var item in wordDiff.Items)
        {
            if (item.LineNumber != currentLineNumber)
            {
                if (currentLine.Length > 0)
                {
                    result.Lines.Add(new DiffLine
                    {
                        Type = currentChangeType,
                        Content = currentLine.ToString().TrimEnd(),
                        LineNumber = currentLineNumber
                    });
                }
                currentLine.Clear();
                currentLineNumber = item.LineNumber;
                currentChangeType = ChangeType.Unchanged;
            }
            
            currentLine.Append(item.Text);
            
            if (item.Operation == WordLevelDiff.WordDiffOperationType.Delete)
                currentChangeType = ChangeType.Deleted;
            else if (item.Operation == WordLevelDiff.WordDiffOperationType.Insert && currentChangeType != ChangeType.Deleted)
                currentChangeType = ChangeType.Added;
        }
        
        if (currentLine.Length > 0)
        {
            result.Lines.Add(new DiffLine
            {
                Type = currentChangeType,
                Content = currentLine.ToString().TrimEnd(),
                LineNumber = currentLineNumber
            });
        }
        
        return result;
    }

    private DiffService.DiffResult GenerateCharacterDiff(string oldContent, string newContent, DiffOptions options)
    {
        var oldChars = oldContent.ToCharArray().Select(c => c.ToString()).ToArray();
        var newChars = newContent.ToCharArray().Select(c => c.ToString()).ToArray();
        
        var charDiff = MyersDiffAlgorithm.Compute(oldChars, newChars);
        
        var result = new DiffResult();
        var currentLine = new StringBuilder();
        var currentLineNumber = 1;
        var currentChangeType = ChangeType.Unchanged;
        
        foreach (var item in charDiff)
        {
            if (item.Text == "\n")
            {
                result.Lines.Add(new DiffLine
                {
                    Type = currentChangeType,
                    Content = currentLine.ToString(),
                    LineNumber = currentLineNumber
                });
                currentLine.Clear();
                currentLineNumber++;
                currentChangeType = ChangeType.Unchanged;
            }
            else
            {
                currentLine.Append(item.Text);
                
                var changeType = item.Operation switch
                {
                    MyersDiffAlgorithm.DiffOperationType.Delete => ChangeType.Deleted,
                    MyersDiffAlgorithm.DiffOperationType.Insert => ChangeType.Added,
                    _ => ChangeType.Unchanged
                };
                
                if (changeType != ChangeType.Unchanged)
                    currentChangeType = changeType;
            }
        }
        
        if (currentLine.Length > 0)
        {
            result.Lines.Add(new DiffLine
            {
                Type = currentChangeType,
                Content = currentLine.ToString(),
                LineNumber = currentLineNumber
            });
        }
        
        return result;
    }

    private string RenderUnifiedDiffHtml(DiffService.DiffResult diff)
    {
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

    private string RenderSideBySideDiffHtml(DiffService.DiffResult diff)
    {
        var html = new StringBuilder();
        
        html.AppendLine("<div class=\"diff-container\">");
        html.AppendLine("<div class=\"diff-stats mb-3\">");
        html.AppendLine($"<span class=\"badge bg-success\">+{diff.AddedLines}</span> ");
        html.AppendLine($"<span class=\"badge bg-danger\">-{diff.DeletedLines}</span>");
        if (diff.ModifiedLines > 0)
        {
            html.AppendLine($" <span class=\"badge bg-warning\">~{diff.ModifiedLines}</span>");
        }
        html.AppendLine("</div>");
        
        html.AppendLine("<div class=\"row\">");
        html.AppendLine("<div class=\"col-6\">");
        html.AppendLine("<h6>Old Version</h6>");
        html.AppendLine("<div class=\"diff-content\" style=\"font-family: monospace; font-size: 14px; border-right: 1px solid #dee2e6;\">");
        
        foreach (var line in diff.Lines.Where(l => l.Type != ChangeType.Added))
        {
            var style = line.Type switch
            {
                ChangeType.Deleted => "background-color: #f8d7da; border-left: 3px solid #dc3545;",
                _ => "background-color: #f8f9fa;"
            };
            
            html.AppendLine($"<div style=\"{style} padding: 2px 8px; margin: 1px 0;\">");
            html.AppendLine($"<span style=\"color: #6c757d; margin-right: 10px;\">{line.LineNumber:D3}</span>");
            html.AppendLine($"<span>{System.Web.HttpUtility.HtmlEncode(line.Content)}</span>");
            html.AppendLine("</div>");
        }
        
        html.AppendLine("</div></div>");
        html.AppendLine("<div class=\"col-6\">");
        html.AppendLine("<h6>New Version</h6>");
        html.AppendLine("<div class=\"diff-content\" style=\"font-family: monospace; font-size: 14px;\">");
        
        foreach (var line in diff.Lines.Where(l => l.Type != ChangeType.Deleted))
        {
            var style = line.Type switch
            {
                ChangeType.Added => "background-color: #d4edda; border-left: 3px solid #28a745;",
                _ => "background-color: #f8f9fa;"
            };
            
            html.AppendLine($"<div style=\"{style} padding: 2px 8px; margin: 1px 0;\">");
            html.AppendLine($"<span style=\"color: #6c757d; margin-right: 10px;\">{line.LineNumber:D3}</span>");
            html.AppendLine($"<span>{System.Web.HttpUtility.HtmlEncode(line.Content)}</span>");
            html.AppendLine("</div>");
        }
        
        html.AppendLine("</div></div></div></div>");
        
        return html.ToString();
    }

    private string RenderInlineDiffHtml(DiffService.DiffResult diff)
    {
        var html = new StringBuilder();
        html.AppendLine("<div class=\"diff-container\">");
        html.AppendLine("<div class=\"diff-inline\" style=\"font-family: monospace; font-size: 14px;\">");
        
        foreach (var line in diff.Lines)
        {
            if (line.Type == ChangeType.Unchanged)
            {
                html.AppendLine($"<div style=\"padding: 2px 8px;\">{System.Web.HttpUtility.HtmlEncode(line.Content)}</div>");
            }
            else
            {
                var bgColor = line.Type == ChangeType.Added ? "#d4edda" : "#f8d7da";
                var textColor = line.Type == ChangeType.Added ? "#155724" : "#721c24";
                html.AppendLine($"<span style=\"background-color: {bgColor}; color: {textColor}; padding: 1px 3px;\">{System.Web.HttpUtility.HtmlEncode(line.Content)}</span>");
            }
        }
        
        html.AppendLine("</div></div>");
        return html.ToString();
    }

    private string RenderStatsDiffHtml(DiffService.DiffResult diff)
    {
        var html = new StringBuilder();
        html.AppendLine("<div class=\"diff-stats-view card\">");
        html.AppendLine("<div class=\"card-body\">");
        html.AppendLine("<h5>Diff Statistics</h5>");
        html.AppendLine($"<p><strong>Lines Added:</strong> <span class=\"text-success\">{diff.AddedLines}</span></p>");
        html.AppendLine($"<p><strong>Lines Deleted:</strong> <span class=\"text-danger\">{diff.DeletedLines}</span></p>");
        html.AppendLine($"<p><strong>Lines Modified:</strong> <span class=\"text-warning\">{diff.ModifiedLines}</span></p>");
        html.AppendLine($"<p><strong>Total Lines:</strong> {diff.Lines.Count}</p>");
        
        var unchanged = diff.Lines.Count(l => l.Type == ChangeType.Unchanged);
        html.AppendLine($"<p><strong>Unchanged Lines:</strong> {unchanged}</p>");
        
        html.AppendLine("</div></div>");
        return html.ToString();
    }

    private List<string> SplitIntoLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new List<string>();
            
        return text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
    }
}