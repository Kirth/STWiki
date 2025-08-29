using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;
using System.Text.RegularExpressions;

namespace STWiki.Pages;

public class SearchResult
{
    public STWiki.Data.Entities.Page Page { get; set; } = null!;
    public string HighlightedTitle { get; set; } = "";
    public string HighlightedSummary { get; set; } = "";
    public string BodySnippet { get; set; } = "";
    public string MatchType { get; set; } = ""; // "title", "summary", "body"
}

public class SearchModel : PageModel
{
    private readonly AppDbContext _context;

    public SearchModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }
    
    [BindProperty(SupportsGet = true, Name = "page")]
    public int Page { get; set; } = 1;
    
    public List<SearchResult> Results { get; set; } = new();
    
    public int TotalResults { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 10;

    public async Task OnGetAsync()
    {
        if (!string.IsNullOrWhiteSpace(Query))
        {
            var searchTerm = Query.Trim();
            
            try
            {
                // Get total count for pagination
                TotalResults = await _context.Pages
                    .Where(p => 
                        EF.Functions.ILike(p.Title, $"%{searchTerm}%") ||
                        EF.Functions.ILike(p.Body, $"%{searchTerm}%") ||
                        EF.Functions.ILike(p.Summary, $"%{searchTerm}%"))
                    .CountAsync();
                    
                TotalPages = (int)Math.Ceiling((double)TotalResults / PageSize);
                
                // Get paginated results
                var pages = await _context.Pages
                    .Where(p => 
                        EF.Functions.ILike(p.Title, $"%{searchTerm}%") ||
                        EF.Functions.ILike(p.Body, $"%{searchTerm}%") ||
                        EF.Functions.ILike(p.Summary, $"%{searchTerm}%"))
                    .OrderByDescending(p => p.UpdatedAt)
                    .Skip((Page - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();
                    
                // Create enhanced search results with highlighting
                Results = pages.Select(page => CreateSearchResult(page, searchTerm)).ToList();
                
            }
            catch (Exception ex)
            {
                // Log error but don't expose to user
                Results = new List<SearchResult>();
                TotalResults = 0;
                TotalPages = 0;
            }
        }
    }
    
    private SearchResult CreateSearchResult(STWiki.Data.Entities.Page page, string searchTerm)
    {
        var result = new SearchResult 
        { 
            Page = page,
            HighlightedTitle = HighlightText(page.Title, searchTerm),
            HighlightedSummary = HighlightText(page.Summary, searchTerm)
        };
        
        // Determine match type and create snippet
        if (page.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
        {
            result.MatchType = "title";
        }
        else if (!string.IsNullOrEmpty(page.Summary) && page.Summary.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
        {
            result.MatchType = "summary";
        }
        else
        {
            result.MatchType = "body";
            result.BodySnippet = CreateBodySnippet(page.Body, searchTerm);
        }
        
        return result;
    }
    
    private string HighlightText(string text, string searchTerm)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchTerm))
            return text;
            
        // Case-insensitive highlighting
        var regex = new Regex(Regex.Escape(searchTerm), RegexOptions.IgnoreCase);
        return regex.Replace(text, match => $"<mark>{match.Value}</mark>");
    }
    
    private string CreateBodySnippet(string body, string searchTerm)
    {
        if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(searchTerm))
            return "";
            
        const int snippetLength = 150;
        var index = body.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase);
        
        if (index == -1)
            return body.Length > snippetLength ? body.Substring(0, snippetLength) + "..." : body;
            
        // Find the best position to start the snippet
        var start = Math.Max(0, index - 50);
        var length = Math.Min(snippetLength, body.Length - start);
        
        var snippet = body.Substring(start, length);
        
        // Add ellipsis if we truncated
        if (start > 0) snippet = "..." + snippet;
        if (start + length < body.Length) snippet = snippet + "...";
        
        // Highlight the search term in the snippet
        return HighlightText(snippet, searchTerm);
    }
}