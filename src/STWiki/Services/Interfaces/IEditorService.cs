using STWiki.Models.Collaboration.Events;

namespace STWiki.Services.Interfaces;

/// <summary>
/// Core editor service for content management and text operations
/// </summary>
public interface IEditorService
{
    /// <summary>
    /// Get the current content for a page
    /// </summary>
    Task<string> GetContentAsync(Guid pageId);
    
    /// <summary>
    /// Save content for a page (draft or commit)
    /// </summary>
    Task SaveContentAsync(Guid pageId, string content, bool isDraft = true);
    
    /// <summary>
    /// Process a text change from the editor
    /// </summary>
    Task ProcessTextChangeAsync(TextChangeEventArgs args);
    
    /// <summary>
    /// Event fired when content changes (local or remote)
    /// </summary>
    event EventHandler<ContentChangedEventArgs> ContentChanged;
    
    /// <summary>
    /// Event fired when save operations complete
    /// </summary>
    event EventHandler<SaveCompletedEventArgs> SaveCompleted;
}