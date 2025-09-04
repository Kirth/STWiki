using STWiki.Models.Collaboration;
using STWiki.Models.Collaboration.Events;
using STWiki.Models.Collaboration.Operations;

namespace STWiki.Services.Interfaces;

/// <summary>
/// Service for managing real-time collaborative editing features
/// </summary>
public interface ICollaborationService : IAsyncDisposable
{
    /// <summary>
    /// Initialize collaboration for a specific page
    /// </summary>
    Task InitializeAsync(Guid pageId, string userId);
    
    /// <summary>
    /// Send a text operation to other collaborators
    /// </summary>
    Task SendOperationAsync(ITextOperation operation);
    
    /// <summary>
    /// Get the current collaboration session
    /// </summary>
    Task<CollaborationSession?> GetSessionAsync(Guid pageId);
    
    /// <summary>
    /// Get list of connected users for a page
    /// </summary>
    Task<IEnumerable<UserPresence>> GetConnectedUsersAsync(Guid pageId);
    
    /// <summary>
    /// Update cursor position for current user
    /// </summary>
    Task UpdateCursorPositionAsync(CursorPosition cursor);
    
    /// <summary>
    /// Get connection status
    /// </summary>
    CollaborationStatus Status { get; }
    
    /// <summary>
    /// Event fired when a remote operation is received
    /// </summary>
    event EventHandler<OperationReceivedEventArgs> OperationReceived;
    
    /// <summary>
    /// Event fired when content changes from remote operations
    /// </summary>
    event EventHandler<ContentChangedEventArgs> ContentChanged;
    
    /// <summary>
    /// Event fired when collaboration status changes
    /// </summary>
    event EventHandler<CollaborationStatusChangedEventArgs> StatusChanged;
    
    /// <summary>
    /// Event fired when user presence changes
    /// </summary>
    event EventHandler<UserPresenceChangedEventArgs> UserPresenceChanged;
    
    /// <summary>
    /// Event fired when cursor positions are updated
    /// </summary>
    event EventHandler<CursorPositionChangedEventArgs> CursorPositionChanged;
}