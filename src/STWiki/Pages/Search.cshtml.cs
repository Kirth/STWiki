using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;
using STWiki.Services;
using STWiki.Helpers;
using System.Text.RegularExpressions;

namespace STWiki.Pages;

public class SearchResult
{
    public STWiki.Data.Entities.Page Page { get; set; } = null!;
    public string HighlightedTitle { get; set; } = "";
    public string HighlightedSummary { get; set; } = "";
    public string BodySnippet { get; set; } = "";
    public string MatchType { get; set; } = ""; // "title", "summary", "body", "code"
    public string? CodeLanguage { get; set; }
    public double Score { get; set; }
    public string UpdatedByDisplayName { get; set; } = "";
}

public class SearchModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly ActivityService _activityService;
    private readonly AdvancedSearchService _advancedSearchService;
    private readonly UserService _userService;

    public SearchModel(AppDbContext context, ActivityService activityService, AdvancedSearchService advancedSearchService, UserService userService)
    {
        _context = context;
        _activityService = activityService;
        _advancedSearchService = advancedSearchService;
        _userService = userService;
    }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }
    
    [BindProperty(SupportsGet = true, Name = "page")]
    public new int Page { get; set; } = 1;

    // Advanced search filters
    [BindProperty(SupportsGet = true)]
    public bool SearchInTitle { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool SearchInContent { get; set; } = true;

    [BindProperty(SupportsGet = true)]
    public bool SearchInCode { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool SearchInSummary { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Language { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Author { get; set; }
    
    public List<SearchResult> Results { get; set; } = new();
    
    public int TotalResults { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 10;
    public bool UseAdvancedSearch => HasAdvancedFilters();

    public async Task OnGetAsync()
    {
        if (!string.IsNullOrWhiteSpace(Query))
        {
            var searchTerm = Query.Trim();
            
            try
            {
                if (UseAdvancedSearch)
                {
                    await PerformAdvancedSearchAsync(searchTerm);
                }
                else
                {
                    await PerformLegacySearchAsync(searchTerm);
                }
                
                // Log search activity
                if (User.Identity?.IsAuthenticated == true)
                {
                    var currentUser = User.Identity.Name ?? "Unknown";
                    var currentUserDisplayName = UserLinkHelper.GetUserDisplayName(User);
                    await _activityService.LogSearchAsync(
                        currentUser, 
                        currentUserDisplayName, 
                        searchTerm, 
                        TotalResults, 
                        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", 
                        HttpContext.Request.Headers.UserAgent.ToString()
                    );
                }
                
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

    private async Task PerformAdvancedSearchAsync(string searchTerm)
    {
        // Build advanced query from filters
        var queryBuilder = new List<string> { searchTerm };
        
        // Add search scope filters
        var scopes = new List<string>();
        if (SearchInTitle) scopes.Add("title");
        if (SearchInContent) scopes.Add("content");
        if (SearchInCode) scopes.Add("code");
        if (SearchInSummary) scopes.Add("summary");

        // If specific scopes are selected, add them to query
        if (scopes.Any() && !(scopes.Count == 1 && scopes.Contains("content")))
        {
            foreach (var scope in scopes)
            {
                queryBuilder.Add($"in:{scope}");
            }
        }

        if (!string.IsNullOrWhiteSpace(Language))
            queryBuilder.Add($"lang:{Language}");

        if (!string.IsNullOrWhiteSpace(Author))
            queryBuilder.Add($"author:{Author}");

        var fullQuery = string.Join(" ", queryBuilder);
        
        var options = new AdvancedSearchOptions
        {
            MaxResults = 1000, // Get all results for pagination
            HighlightMatches = true
        };

        var advancedResults = await _advancedSearchService.SearchAsync(fullQuery, options);
        
        // Convert to legacy format with highlighting
        var convertedResults = new List<SearchResult>();
        foreach (var result in advancedResults.Results)
        {
            var page = await _context.Pages.FindAsync(result.PageId);
            if (page != null)
            {
                // Resolve user display name
                var updatedByUser = await _userService.GetUserByUserIdAsync(page.UpdatedBy ?? "");
                var updatedByDisplayName = updatedByUser?.DisplayName ?? page.UpdatedBy ?? "Unknown User";

                var searchResult = new SearchResult
                {
                    Page = page,
                    HighlightedTitle = HighlightText(page.Title, searchTerm),
                    HighlightedSummary = !string.IsNullOrWhiteSpace(page.Summary) 
                        ? HighlightText(page.Summary, searchTerm)
                        : "",
                    BodySnippet = result.MatchType.ToLower().Contains("code") 
                        ? $"<pre class=\"bg-light p-2 rounded mb-0\"><code>{HighlightText(result.Snippet, searchTerm)}</code></pre>"
                        : HighlightText(result.Snippet, searchTerm),
                    MatchType = result.MatchType.ToLower().Contains("code") ? "code" : result.MatchType.ToLower(),
                    CodeLanguage = result.CodeLanguage,
                    Score = result.Score,
                    UpdatedByDisplayName = updatedByDisplayName
                };

                convertedResults.Add(searchResult);
            }
        }

        TotalResults = convertedResults.Count;
        TotalPages = (int)Math.Ceiling((double)TotalResults / PageSize);
        
        // Apply pagination
        Results = convertedResults
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }

    private async Task PerformLegacySearchAsync(string searchTerm)
    {
        // Original search implementation
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
            
        // Create enhanced search results with highlighting and user resolution
        Results = new List<SearchResult>();
        foreach (var page in pages)
        {
            var result = await CreateSearchResultAsync(page, searchTerm);
            Results.Add(result);
        }
    }

    private bool HasAdvancedFilters()
    {
        // Check checkbox filters
        var hasCheckboxFilters = SearchInTitle || SearchInCode || SearchInSummary ||
               !string.IsNullOrWhiteSpace(Language) ||
               !string.IsNullOrWhiteSpace(Author) ||
               (!SearchInContent && (SearchInTitle || SearchInCode || SearchInSummary));
        
        // Check for advanced syntax in query string
        var hasAdvancedSyntax = !string.IsNullOrWhiteSpace(Query) && (
            Query.Contains("in:") ||
            Query.Contains("title:") ||
            Query.Contains("lang:") ||
            Query.Contains("author:") ||
            Query.Contains("\""));
        
        return hasCheckboxFilters || hasAdvancedSyntax;
    }
    
    private async Task<SearchResult> CreateSearchResultAsync(STWiki.Data.Entities.Page page, string searchTerm)
    {
        // Resolve user display name
        var updatedByUser = await _userService.GetUserByUserIdAsync(page.UpdatedBy ?? "");
        var updatedByDisplayName = updatedByUser?.DisplayName ?? page.UpdatedBy ?? "Unknown User";

        var result = new SearchResult 
        { 
            Page = page,
            HighlightedTitle = HighlightText(page.Title, searchTerm),
            HighlightedSummary = !string.IsNullOrWhiteSpace(page.Summary) 
                ? HighlightText(page.Summary, searchTerm)
                : "",
            UpdatedByDisplayName = updatedByDisplayName
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