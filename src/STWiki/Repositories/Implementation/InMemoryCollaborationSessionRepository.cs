using STWiki.Models.Collaboration;
using STWiki.Repositories.Interfaces;
using System.Collections.Concurrent;

namespace STWiki.Repositories.Implementation;

/// <summary>
/// In-memory implementation of collaboration session repository
/// </summary>
public class InMemoryCollaborationSessionRepository : ICollaborationSessionRepository
{
    private readonly ConcurrentDictionary<Guid, CollaborationSession> _sessions = new();
    private readonly ILogger<InMemoryCollaborationSessionRepository> _logger;
    
    public InMemoryCollaborationSessionRepository(ILogger<InMemoryCollaborationSessionRepository> logger)
    {
        _logger = logger;
    }
    
    public Task<CollaborationSession?> GetSessionAsync(Guid pageId)
    {
        _sessions.TryGetValue(pageId, out var session);
        _logger.LogDebug("Retrieved session for page {PageId}: {Found}", pageId, session != null);
        return Task.FromResult(session);
    }
    
    public Task SaveSessionAsync(CollaborationSession session)
    {
        _sessions.AddOrUpdate(session.PageId, session, (key, existing) => session);
        _logger.LogDebug("Saved session for page {PageId}: {UserCount} users, sequence {Sequence}", 
            session.PageId, session.UserCount, session.CurrentSequenceNumber);
        return Task.CompletedTask;
    }
    
    public Task RemoveSessionAsync(Guid pageId)
    {
        var removed = _sessions.TryRemove(pageId, out var session);
        _logger.LogDebug("Removed session for page {PageId}: {Removed}", pageId, removed);
        return Task.CompletedTask;
    }
    
    public Task<IEnumerable<CollaborationSession>> GetActiveSessionsAsync()
    {
        var sessions = _sessions.Values.Where(s => s.IsActive).ToList();
        _logger.LogDebug("Retrieved {Count} active sessions", sessions.Count);
        return Task.FromResult<IEnumerable<CollaborationSession>>(sessions);
    }
    
    public Task<bool> ExistsAsync(Guid pageId)
    {
        var exists = _sessions.ContainsKey(pageId);
        return Task.FromResult(exists);
    }
}