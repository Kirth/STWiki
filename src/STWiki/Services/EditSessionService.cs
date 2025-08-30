using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using STWiki.Models;
using STWiki.Models.Collaboration;

namespace STWiki.Services;

public interface IEditSessionService
{
    Task<EditSession> GetOrCreateSessionAsync(string pageId, string initialContent);
    Task<EditSession?> GetSessionAsync(string pageId);
    Task RemoveSessionAsync(string pageId);
    Task<UserState> AddUserToSessionAsync(string pageId, string userId, string displayName, string email, string connectionId);
    Task RemoveUserFromSessionAsync(string pageId, string userId);
    Task<TextOperation> ApplyOperationAsync(string pageId, TextOperation operation);
    Task<List<TextOperation>> QueueAndProcessOperationAsync(string pageId, TextOperation operation);
    Task UpdateUserCursorAsync(string pageId, string userId, CursorPosition cursor);
    Task CleanupIdleSessionsAsync();
    IEnumerable<EditSession> GetAllSessions();
}

public class EditSessionService : IEditSessionService
{
    private readonly ConcurrentDictionary<string, EditSession> _sessions = new();
    private readonly OperationalTransform _operationalTransform = new();
    private readonly ILogger<EditSessionService> _logger;
    private readonly CollaborationOptions _options;
    
    private static readonly string[] UserColors = new[]
    {
        "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7", 
        "#DDA0DD", "#98D8C8", "#F7DC6F", "#BB8FCE", "#85C1E9"
    };
    
    public EditSessionService(ILogger<EditSessionService> logger, IOptions<CollaborationOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }
    
    public Task<EditSession> GetOrCreateSessionAsync(string pageId, string initialContent)
    {
        var session = _sessions.GetOrAdd(pageId, _ => new EditSession
        {
            PageId = pageId,
            CurrentContent = initialContent,
            CreatedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow
        });
        
        // Update content if session already exists but content is different
        if (session.CurrentContent != initialContent && session.ConnectedUsers.IsEmpty)
        {
            session.CurrentContent = initialContent;
            session.OperationHistory.Clear();
            session.OperationCounter = 0;
        }
        
        _logger.LogInformation("Edit session for page {PageId} retrieved/created with {UserCount} users", 
            pageId, session.ConnectedUsers.Count);
            
        return Task.FromResult(session);
    }
    
    public Task<EditSession?> GetSessionAsync(string pageId)
    {
        _sessions.TryGetValue(pageId, out var session);
        return Task.FromResult(session);
    }
    
    public Task RemoveSessionAsync(string pageId)
    {
        if (_sessions.TryRemove(pageId, out var session))
        {
            _logger.LogInformation("Edit session for page {PageId} removed", pageId);
        }
        return Task.CompletedTask;
    }
    
    public async Task<UserState> AddUserToSessionAsync(string pageId, string userId, string displayName, string email, string connectionId)
    {
        var session = await GetSessionAsync(pageId);
        if (session == null)
        {
            throw new InvalidOperationException($"No edit session found for page {pageId}");
        }
        
        var userColor = UserColors[Math.Abs(userId.GetHashCode()) % UserColors.Length];
        
        var userState = new UserState
        {
            UserId = userId,
            DisplayName = displayName,
            Email = email,
            ConnectionId = connectionId,
            Color = userColor,
            LastSeen = DateTime.UtcNow
        };
        
        session.AddUser(userState);
        
        _logger.LogInformation("User {UserId} ({DisplayName}) joined edit session for page {PageId}", 
            userId, displayName, pageId);
            
        return userState;
    }
    
    public async Task RemoveUserFromSessionAsync(string pageId, string userId)
    {
        var session = await GetSessionAsync(pageId);
        if (session != null)
        {
            session.RemoveUser(userId);
            _logger.LogInformation("User {UserId} left edit session for page {PageId}", userId, pageId);
            
            // Remove session if no users left and it's been idle
            if (!session.ConnectedUsers.Any() && session.IsIdle)
            {
                await RemoveSessionAsync(pageId);
            }
        }
    }
    
    public async Task<TextOperation> ApplyOperationAsync(string pageId, TextOperation operation)
    {
        var session = await GetSessionAsync(pageId);
        if (session == null)
        {
            throw new InvalidOperationException($"No edit session found for page {pageId}");
        }
        
        // Transform operation against recent history
        var recentHistory = session.OperationHistory
            .Where(op => op.Timestamp > operation.Timestamp - 10000) // Last 10 seconds
            .OrderBy(op => op.Timestamp)
            .ToList();
            
        var transformedOps = OperationalTransform.TransformAgainstHistory(operation, recentHistory);
        
        // Apply the first valid transformed operation
        var finalOperation = transformedOps.FirstOrDefault();
        if (finalOperation != null)
        {
            session.AddOperation(finalOperation);
            
            // Limit operation history based on configuration
            if (session.OperationHistory.Count > _options.MaxOperationHistorySize)
            {
                var excess = session.OperationHistory.Count - _options.MaxOperationHistorySize;
                session.OperationHistory.RemoveRange(0, excess);
            }
            
            _logger.LogDebug("Applied operation {OpType} at position {Position} by user {UserId} to page {PageId}", 
                finalOperation.OpType, finalOperation.Position, finalOperation.UserId, pageId);
        }
        
        return finalOperation ?? operation;
    }
    
    /// <summary>
    /// Queue operation and process all queued operations with strict sequencing
    /// </summary>
    public async Task<List<TextOperation>> QueueAndProcessOperationAsync(string pageId, TextOperation operation)
    {
        var session = await GetSessionAsync(pageId);
        if (session == null)
        {
            throw new InvalidOperationException($"No edit session found for page {pageId}");
        }
        
        // Queue the operation for sequential processing
        session.QueueOperation(operation);
        
        // Process all queued operations with proper sequencing and transformation
        var processedOperations = new List<TextOperation>();
        
        lock (session._operationLock)
        {
            // Get operations that need transformation
            var operationsToTransform = new Queue<TextOperation>(session._operationQueue);
            session._operationQueue.Clear();
            
            while (operationsToTransform.Count > 0)
            {
                var currentOp = operationsToTransform.Dequeue();
                
                // Transform against ALL operations that have been processed since this operation's conception
                var relevantHistory = session.OperationHistory
                    .Where(op => op.ServerSequenceNumber > 0) // Only server-sequenced operations
                    .OrderBy(op => op.ServerSequenceNumber)
                    .ToList();
                
                var transformedOps = relevantHistory.Any() 
                    ? OperationalTransform.TransformAgainstHistory(currentOp, relevantHistory)
                    : new List<TextOperation> { currentOp };
                
                // Apply the first valid transformed operation
                var finalOperation = transformedOps.FirstOrDefault();
                if (finalOperation != null)
                {
                    // Assign server sequence number for strict ordering
                    finalOperation.ServerSequenceNumber = ++session.GlobalSequenceNumber;
                    
                    // Add to session
                    session.AddOperation(finalOperation);
                    processedOperations.Add(finalOperation);
                    
                    _logger.LogDebug("Processed operation {OpType} with sequence {Seq} at position {Position} by user {UserId} to page {PageId}", 
                        finalOperation.OpType, finalOperation.ServerSequenceNumber, finalOperation.Position, finalOperation.UserId, pageId);
                }
            }
            
            // Cleanup operation history
            if (session.OperationHistory.Count > _options.MaxOperationHistorySize)
            {
                var excess = session.OperationHistory.Count - _options.MaxOperationHistorySize;
                session.OperationHistory.RemoveRange(0, excess);
            }
        }
        
        return processedOperations;
    }
    
    public async Task UpdateUserCursorAsync(string pageId, string userId, CursorPosition cursor)
    {
        var session = await GetSessionAsync(pageId);
        session?.UpdateUserCursor(userId, cursor);
    }
    
    public Task CleanupIdleSessionsAsync()
    {
        var timeoutMinutes = _options.SessionTimeoutMinutes;
        var cutoffTime = DateTime.UtcNow.AddMinutes(-timeoutMinutes);
        
        var idleSessions = _sessions
            .Where(kvp => kvp.Value.LastActivity < cutoffTime && !kvp.Value.ConnectedUsers.Any())
            .Select(kvp => kvp.Key)
            .ToList();
            
        foreach (var pageId in idleSessions)
        {
            _sessions.TryRemove(pageId, out _);
            _logger.LogInformation("Cleaned up idle session for page {PageId} (timeout: {TimeoutMinutes} minutes)", 
                pageId, timeoutMinutes);
        }
        
        return Task.CompletedTask;
    }
    
    public IEnumerable<EditSession> GetAllSessions()
    {
        return _sessions.Values;
    }
}