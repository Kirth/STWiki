using STWiki.Models.Collaboration.Operations;

namespace STWiki.Services.Interfaces;

/// <summary>
/// Service interface for JavaScript interop with the editor
/// </summary>
public interface IJavaScriptEditorService
{
    /// <summary>
    /// Initialize the JavaScript editor module
    /// </summary>
    Task InitializeAsync(Guid pageId, string elementId);
    
    /// <summary>
    /// Apply a remote text operation to the editor
    /// </summary>
    Task ApplyRemoteOperationAsync(ITextOperation operation);
    
    /// <summary>
    /// Get the current content from the editor
    /// </summary>
    Task<string> GetContentAsync();
    
    /// <summary>
    /// Set the content in the editor
    /// </summary>
    Task SetContentAsync(string content);
    
    /// <summary>
    /// Get the current cursor position
    /// </summary>
    Task<(int start, int end)> GetSelectionAsync();
    
    /// <summary>
    /// Set the cursor/selection position
    /// </summary>
    Task SetSelectionAsync(int start, int end);
    
    /// <summary>
    /// Show a remote user's cursor
    /// </summary>
    Task ShowRemoteCursorAsync(string userId, string userColor, int position, int selectionEnd);
    
    /// <summary>
    /// Hide a remote user's cursor
    /// </summary>
    Task HideRemoteCursorAsync(string userId);
    
    /// <summary>
    /// Dispose of the JavaScript editor resources
    /// </summary>
    ValueTask DisposeAsync();
}