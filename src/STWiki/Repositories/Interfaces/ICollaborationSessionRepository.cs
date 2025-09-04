using STWiki.Models.Collaboration;

namespace STWiki.Repositories.Interfaces;

/// <summary>
/// Repository interface for managing collaboration sessions
/// </summary>
public interface ICollaborationSessionRepository
{
    /// <summary>
    /// Get a collaboration session by page ID
    /// </summary>
    Task<CollaborationSession?> GetSessionAsync(Guid pageId);
    
    /// <summary>
    /// Save a collaboration session
    /// </summary>
    Task SaveSessionAsync(CollaborationSession session);
    
    /// <summary>
    /// Remove a collaboration session
    /// </summary>
    Task RemoveSessionAsync(Guid pageId);
    
    /// <summary>
    /// Get all active collaboration sessions
    /// </summary>
    Task<IEnumerable<CollaborationSession>> GetActiveSessionsAsync();
    
    /// <summary>
    /// Check if a session exists
    /// </summary>
    Task<bool> ExistsAsync(Guid pageId);
}