using STWiki.Models.Collaboration;
using STWiki.Models.Collaboration.Operations;

namespace STWiki.Services.Interfaces;

/// <summary>
/// Service for managing collaborative editing sessions
/// </summary>
public interface ICollaborationSessionService
{
    /// <summary>
    /// Create a new collaboration session for a page
    /// </summary>
    Task<CollaborationSession> CreateSessionAsync(Guid pageId, string initialContent);
    
    /// <summary>
    /// Get an existing session or create a new one
    /// </summary>
    Task<CollaborationSession> GetOrCreateSessionAsync(Guid pageId, string initialContent);
    
    /// <summary>
    /// Add a user to a collaboration session
    /// </summary>
    Task<UserPresence> AddUserAsync(Guid pageId, string userId, string displayName, string email);
    
    /// <summary>
    /// Remove a user from a collaboration session
    /// </summary>
    Task RemoveUserAsync(Guid pageId, string userId);
    
    /// <summary>
    /// Process a text operation within a session
    /// </summary>
    Task<OperationResult> ProcessOperationAsync(Guid pageId, ITextOperation operation);
    
    /// <summary>
    /// Get recent operations for a session (for new users joining)
    /// </summary>
    Task<IEnumerable<ITextOperation>> GetRecentOperationsAsync(Guid pageId, int count = 100);
    
    /// <summary>
    /// Update cursor position for a user in a session
    /// </summary>
    Task UpdateCursorAsync(Guid pageId, string userId, CursorPosition cursor);
    
    /// <summary>
    /// Get all active sessions (for monitoring/cleanup)
    /// </summary>
    Task<IEnumerable<CollaborationSession>> GetActiveSessionsAsync();
    
    /// <summary>
    /// Clean up inactive sessions
    /// </summary>
    Task CleanupInactiveSessionsAsync(TimeSpan inactiveThreshold);
}