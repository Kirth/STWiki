using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;
using System.Text.RegularExpressions;

namespace STWiki.Services;

public class AdvancedSearchService
{
    private readonly AppDbContext _context;
    private readonly ILogger<AdvancedSearchService> _logger;

    public AdvancedSearchService(AppDbContext context, ILogger<AdvancedSearchService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AdvancedSearchResult> SearchAsync(string query, AdvancedSearchOptions? options = null)
    {
        options ??= new AdvancedSearchOptions();
        
        try
        {
            var parsedQuery = ParseAdvancedQuery(query);
            var results = new List<SearchResultItem>();

            // Search in different content areas based on filters
            if (parsedQuery.SearchInTitle || parsedQuery.SearchInAll)
            {
                var titleResults = await SearchInTitlesAsync(parsedQuery, options);
                results.AddRange(titleResults);
            }

            if (parsedQuery.SearchInContent || parsedQuery.SearchInAll)
            {
                var contentResults = await SearchInContentAsync(parsedQuery, options);
                results.AddRange(contentResults);
            }

            if (parsedQuery.SearchInCode)
            {
                var codeResults = await SearchInCodeBlocksAsync(parsedQuery, options);
                results.AddRange(codeResults);
            }

            // Remove duplicates and rank results
            var uniqueResults = results
                .GroupBy(r => r.PageId)
                .Select(g => g.OrderByDescending(r => r.Score).First())
                .OrderByDescending(r => r.Score)
                .Take(options.MaxResults)
                .ToList();

            return new AdvancedSearchResult
            {
                Query = query,
                Results = uniqueResults,
                TotalResults = uniqueResults.Count,
                SearchTime = 0 // TODO: Add timing
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing advanced search for query: {Query}", query);
            return new AdvancedSearchResult
            {
                Query = query,
                Results = new List<SearchResultItem>(),
                TotalResults = 0,
                SearchTime = 0,
                Error = ex.Message
            };
        }
    }

    private ParsedQuery ParseAdvancedQuery(string query)
    {
        var parsed = new ParsedQuery
        {
            OriginalQuery = query,
            SearchInAll = true // Default to searching everywhere
        };

        if (string.IsNullOrWhiteSpace(query))
            return parsed;

        // Parse special filters
        var filters = new List<string>();
        var cleanQuery = query;

        _logger.LogInformation("Parsing advanced query: {Query}", query);

        // Extract in: filters
        var inMatches = Regex.Matches(query, @"in:(\w+)", RegexOptions.IgnoreCase);
        _logger.LogInformation("Found {Count} in: filters", inMatches.Count);
        
        foreach (Match match in inMatches)
        {
            var target = match.Groups[1].Value.ToLower();
            _logger.LogInformation("Processing in:{Target} filter", target);
            
            switch (target)
            {
                case "title":
                    parsed.SearchInTitle = true;
                    parsed.SearchInAll = false;
                    break;
                case "content":
                    parsed.SearchInContent = true;
                    parsed.SearchInAll = false;
                    break;
                case "code":
                    parsed.SearchInCode = true;
                    parsed.SearchInAll = false;
                    _logger.LogInformation("Code search enabled");
                    break;
                case "summary":
                    parsed.SearchInSummary = true;
                    parsed.SearchInAll = false;
                    break;
            }
            cleanQuery = cleanQuery.Replace(match.Value, "").Trim();
        }

        // Extract title: filters (shorthand for in:title)
        var titleMatches = Regex.Matches(query, @"title:(\S+)", RegexOptions.IgnoreCase);
        foreach (Match match in titleMatches)
        {
            parsed.SearchInTitle = true;
            parsed.SearchInAll = false;
            // Add the term after "title:" as a search term
            parsed.SearchTerms.Add(match.Groups[1].Value);
            cleanQuery = cleanQuery.Replace(match.Value, "").Trim();
        }

        // Extract lang: filters
        var langMatches = Regex.Matches(query, @"lang:(\w+)", RegexOptions.IgnoreCase);
        foreach (Match match in langMatches)
        {
            parsed.Languages.Add(match.Groups[1].Value.ToLower());
            cleanQuery = cleanQuery.Replace(match.Value, "").Trim();
        }

        // Extract author: filters
        var authorMatches = Regex.Matches(query, @"author:(\S+)", RegexOptions.IgnoreCase);
        foreach (Match match in authorMatches)
        {
            parsed.Authors.Add(match.Groups[1].Value);
            cleanQuery = cleanQuery.Replace(match.Value, "").Trim();
        }

        // Extract quoted phrases
        var phraseMatches = Regex.Matches(cleanQuery, @"""([^""]+)""");
        foreach (Match match in phraseMatches)
        {
            parsed.ExactPhrases.Add(match.Groups[1].Value);
            cleanQuery = cleanQuery.Replace(match.Value, "").Trim();
        }

        // Remaining words as search terms
        parsed.SearchTerms.AddRange(cleanQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return parsed;
    }

    private async Task<List<SearchResultItem>> SearchInTitlesAsync(ParsedQuery query, AdvancedSearchOptions options)
    {
        var results = new List<SearchResultItem>();
        var searchTerms = query.SearchTerms.Concat(query.ExactPhrases);

        if (!searchTerms.Any())
            return results;

        var pagesQuery = _context.Pages
            .Where(p => searchTerms.Any(term => p.Title.ToLower().Contains(term.ToLower())));
        
        // Apply author filter if specified
        if (query.Authors.Any())
        {
            pagesQuery = pagesQuery.Where(p => query.Authors.Any(author => 
                p.UpdatedBy.ToLower().Contains(author.ToLower())));
        }
        
        var pages = await pagesQuery.Take(options.MaxResults).ToListAsync();

        foreach (var page in pages)
        {
            var score = CalculateTitleScore(page.Title, searchTerms);
            results.Add(new SearchResultItem
            {
                PageId = page.Id,
                Title = page.Title,
                Slug = page.Slug,
                Summary = page.Summary,
                MatchType = "Title",
                Score = score,
                Snippet = page.Title
            });
        }

        return results;
    }

    private async Task<List<SearchResultItem>> SearchInContentAsync(ParsedQuery query, AdvancedSearchOptions options)
    {
        var results = new List<SearchResultItem>();
        var searchTerms = query.SearchTerms.Concat(query.ExactPhrases);

        if (!searchTerms.Any())
            return results;

        var pagesQuery = _context.Pages
            .Where(p => searchTerms.Any(term => 
                p.Body.ToLower().Contains(term.ToLower()) || 
                p.Summary.ToLower().Contains(term.ToLower())));
        
        // Apply author filter if specified
        if (query.Authors.Any())
        {
            pagesQuery = pagesQuery.Where(p => query.Authors.Any(author => 
                p.UpdatedBy.ToLower().Contains(author.ToLower())));
        }
        
        var pages = await pagesQuery.Take(options.MaxResults).ToListAsync();

        foreach (var page in pages)
        {
            var score = CalculateContentScore(page.Body + " " + page.Summary, searchTerms);
            var snippet = ExtractSnippet(page.Body, searchTerms, 200);
            
            results.Add(new SearchResultItem
            {
                PageId = page.Id,
                Title = page.Title,
                Slug = page.Slug,
                Summary = page.Summary,
                MatchType = "Content",
                Score = score,
                Snippet = snippet
            });
        }

        return results;
    }

    private async Task<List<SearchResultItem>> SearchInCodeBlocksAsync(ParsedQuery query, AdvancedSearchOptions options)
    {
        var results = new List<SearchResultItem>();
        var searchTerms = query.SearchTerms.Concat(query.ExactPhrases);

        if (!searchTerms.Any())
            return results;

        var pagesQuery = _context.Pages
            .Where(p => p.BodyFormat == "markdown");
        
        // Apply author filter if specified
        if (query.Authors.Any())
        {
            pagesQuery = pagesQuery.Where(p => query.Authors.Any(author => 
                p.UpdatedBy.ToLower().Contains(author.ToLower())));
        }
        
        var pages = await pagesQuery.ToListAsync();

        foreach (var page in pages)
        {
            var codeBlocks = ExtractCodeBlocks(page.Body);
            
            foreach (var codeBlock in codeBlocks)
            {
                // Filter by language if specified
                if (query.Languages.Any() && !query.Languages.Contains(codeBlock.Language.ToLower()))
                    continue;

                var hasMatch = searchTerms.Any(term => codeBlock.Code.ToLower().Contains(term.ToLower()));
                
                if (hasMatch)
                {
                    var score = CalculateCodeScore(codeBlock.Code, searchTerms);
                    var snippet = ExtractSnippet(codeBlock.Code, searchTerms, 150);
                    
                    results.Add(new SearchResultItem
                    {
                        PageId = page.Id,
                        Title = page.Title,
                        Slug = page.Slug,
                        Summary = page.Summary,
                        MatchType = $"Code ({codeBlock.Language})",
                        Score = score,
                        Snippet = snippet,
                        CodeLanguage = codeBlock.Language
                    });
                }
            }
        }

        return results;
    }

    private List<CodeBlock> ExtractCodeBlocks(string markdownContent)
    {
        var codeBlocks = new List<CodeBlock>();
        
        if (string.IsNullOrEmpty(markdownContent))
            return codeBlocks;

        _logger.LogInformation("Extracting code blocks from content of length: {Length}", markdownContent.Length);

        // Pattern for fenced code blocks: ```language\ncode\n```
        // Updated pattern to handle \r\n line endings
        var fencedPattern = @"```(\w+)?\r?\n(.*?)\r?\n```";
        var fencedMatches = Regex.Matches(markdownContent, fencedPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        _logger.LogInformation("Found {Count} fenced code blocks", fencedMatches.Count);
        
        foreach (Match match in fencedMatches)
        {
            var language = string.IsNullOrEmpty(match.Groups[1].Value) ? "text" : match.Groups[1].Value;
            var code = match.Groups[2].Value.Trim();
            
            _logger.LogInformation("Extracted code block - Language: {Language}, Code length: {Length}", language, code.Length);
            
            codeBlocks.Add(new CodeBlock
            {
                Language = language,
                Code = code,
                StartPosition = match.Index,
                EndPosition = match.Index + match.Length
            });
        }

        // Pattern for indented code blocks (4+ spaces)
        var indentedPattern = @"(?:^|\n)((?:    .+\n?)+)";
        var indentedMatches = Regex.Matches(markdownContent, indentedPattern, RegexOptions.Multiline);
        
        foreach (Match match in indentedMatches)
        {
            var code = match.Groups[1].Value.Trim();
            // Remove 4-space indentation
            code = Regex.Replace(code, @"^    ", "", RegexOptions.Multiline);
            
            codeBlocks.Add(new CodeBlock
            {
                Language = "text",
                Code = code,
                StartPosition = match.Index,
                EndPosition = match.Index + match.Length
            });
        }

        return codeBlocks;
    }

    private double CalculateTitleScore(string title, IEnumerable<string> searchTerms)
    {
        var score = 0.0;
        var lowerTitle = title.ToLower();
        
        foreach (var term in searchTerms)
        {
            var lowerTerm = term.ToLower();
            if (lowerTitle == lowerTerm) score += 10; // Exact match
            else if (lowerTitle.StartsWith(lowerTerm)) score += 8; // Starts with
            else if (lowerTitle.Contains(lowerTerm)) score += 5; // Contains
        }
        
        return score;
    }

    private double CalculateContentScore(string content, IEnumerable<string> searchTerms)
    {
        var score = 0.0;
        var lowerContent = content.ToLower();
        
        foreach (var term in searchTerms)
        {
            var lowerTerm = term.ToLower();
            var count = Regex.Matches(lowerContent, Regex.Escape(lowerTerm)).Count;
            score += count * 2; // 2 points per occurrence
        }
        
        return score;
    }

    private double CalculateCodeScore(string code, IEnumerable<string> searchTerms)
    {
        var score = 0.0;
        var lowerCode = code.ToLower();
        
        foreach (var term in searchTerms)
        {
            var lowerTerm = term.ToLower();
            var count = Regex.Matches(lowerCode, Regex.Escape(lowerTerm)).Count;
            score += count * 3; // 3 points per occurrence in code (higher weight)
        }
        
        return score;
    }

    private string ExtractSnippet(string content, IEnumerable<string> searchTerms, int maxLength = 200)
    {
        if (string.IsNullOrEmpty(content))
            return "";

        var lowerContent = content.ToLower();
        var bestPosition = 0;
        var bestScore = 0;

        // Find the position with the most search term matches
        foreach (var term in searchTerms)
        {
            var index = lowerContent.IndexOf(term.ToLower());
            if (index >= 0)
            {
                var score = CountTermsNear(lowerContent, index, searchTerms, 100);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPosition = Math.Max(0, index - 50);
                }
            }
        }

        var snippet = content.Substring(bestPosition, Math.Min(maxLength, content.Length - bestPosition));
        
        // Clean up snippet
        snippet = snippet.Trim();
        if (bestPosition > 0) snippet = "..." + snippet;
        if (bestPosition + maxLength < content.Length) snippet += "...";

        return snippet;
    }

    private int CountTermsNear(string content, int position, IEnumerable<string> searchTerms, int radius)
    {
        var count = 0;
        var start = Math.Max(0, position - radius);
        var end = Math.Min(content.Length, position + radius);
        var section = content.Substring(start, end - start);

        foreach (var term in searchTerms)
        {
            if (section.Contains(term.ToLower()))
                count++;
        }

        return count;
    }
}

public class AdvancedSearchOptions
{
    public int MaxResults { get; set; } = 50;
    public bool HighlightMatches { get; set; } = true;
    public string SortBy { get; set; } = "relevance"; // relevance, date, title
}

public class AdvancedSearchResult
{
    public string Query { get; set; } = "";
    public List<SearchResultItem> Results { get; set; } = new();
    public int TotalResults { get; set; }
    public double SearchTime { get; set; }
    public string? Error { get; set; }
}

public class SearchResultItem
{
    public Guid PageId { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Summary { get; set; } = "";
    public string MatchType { get; set; } = "";
    public double Score { get; set; }
    public string Snippet { get; set; } = "";
    public string? CodeLanguage { get; set; }
}

public class ParsedQuery
{
    public string OriginalQuery { get; set; } = "";
    public List<string> SearchTerms { get; set; } = new();
    public List<string> ExactPhrases { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public List<string> Authors { get; set; } = new();
    public bool SearchInAll { get; set; }
    public bool SearchInTitle { get; set; }
    public bool SearchInContent { get; set; }
    public bool SearchInCode { get; set; }
    public bool SearchInSummary { get; set; }
}

public class CodeBlock
{
    public string Language { get; set; } = "";
    public string Code { get; set; } = "";
    public int StartPosition { get; set; }
    public int EndPosition { get; set; }
}