using STWiki.Services.Interfaces;
using STWiki.Models.Collaboration.Events;
using STWiki.Models.Collaboration.Operations;

namespace STWiki.Services.Implementation;

/// <summary>
/// Implementation of editor service for content management
/// </summary>
public class EditorService : IEditorService
{
    private readonly HttpClient _httpClient;
    private readonly ICollaborationService _collaborationService;
    private readonly ILogger<EditorService> _logger;
    
    public event EventHandler<ContentChangedEventArgs>? ContentChanged;
    public event EventHandler<SaveCompletedEventArgs>? SaveCompleted;
    
    public EditorService(
        HttpClient httpClient,
        ICollaborationService collaborationService,
        ILogger<EditorService> logger)
    {
        _httpClient = httpClient;
        _collaborationService = collaborationService;
        _logger = logger;
        
        // Subscribe to collaboration events
        _collaborationService.ContentChanged += OnCollaborationContentChanged;
    }
    
    public async Task<string> GetContentAsync(Guid pageId)
    {
        try
        {
            _logger.LogDebug("Getting content for page {PageId}", pageId);
            
            var response = await _httpClient.GetAsync($"/api/wiki/{pageId}/content");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Retrieved content for page {PageId}: {Length} characters", pageId, content.Length);
                return content;
            }
            
            _logger.LogWarning("Failed to get content for page {PageId}: {StatusCode}", pageId, response.StatusCode);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting content for page {PageId}", pageId);
            return string.Empty;
        }
    }
    
    public async Task SaveContentAsync(Guid pageId, string content, bool isDraft = true)
    {
        try
        {
            _logger.LogDebug("Saving content for page {PageId}: {IsDraft}, {Length} characters", pageId, isDraft, content.Length);
            
            var endpoint = isDraft ? $"/api/wiki/{pageId}/autosave" : $"/api/wiki/{pageId}/commit";
            var request = new { Content = content, Summary = isDraft ? "Auto-save" : "Manual save" };
            
            var response = await _httpClient.PostAsJsonAsync(endpoint, request);
            var success = response.IsSuccessStatusCode;
            
            if (success)
            {
                _logger.LogDebug("Successfully saved content for page {PageId}", pageId);
            }
            else
            {
                _logger.LogWarning("Failed to save content for page {PageId}: {StatusCode}", pageId, response.StatusCode);
            }
            
            SaveCompleted?.Invoke(this, new SaveCompletedEventArgs(pageId, success, isDraft, 
                success ? null : $"HTTP {response.StatusCode}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving content for page {PageId}", pageId);
            SaveCompleted?.Invoke(this, new SaveCompletedEventArgs(pageId, false, isDraft, ex.Message));
        }
    }
    
    public async Task ProcessTextChangeAsync(TextChangeEventArgs args)
    {
        try
        {
            _logger.LogDebug("Processing text change for page {PageId}: {ChangeType} at {Position}", 
                args.PageId, args.ChangeType, args.Position);
            
            // Convert the text change to an operation
            var operation = args.ToOperation();
            
            // Send through collaboration service
            await _collaborationService.SendOperationAsync(operation);
            
            _logger.LogDebug("Text change processed and sent to collaboration service");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing text change for page {PageId}", args.PageId);
            throw;
        }
    }
    
    private void OnCollaborationContentChanged(object? sender, ContentChangedEventArgs e)
    {
        // Forward collaboration content changes
        ContentChanged?.Invoke(this, e);
    }
}