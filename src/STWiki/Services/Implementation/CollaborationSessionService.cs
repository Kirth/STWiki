using STWiki.Services.Interfaces;
using STWiki.Models.Collaboration;
using STWiki.Models.Collaboration.Operations;
using STWiki.Repositories.Interfaces;

namespace STWiki.Services.Implementation;

/// <summary>
/// Implementation of collaboration session service
/// </summary>
public class CollaborationSessionService : ICollaborationSessionService
{
    private readonly ICollaborationSessionRepository _repository;
    private readonly IOperationTransformService _transformService;
    private readonly ILogger<CollaborationSessionService> _logger;
    
    public CollaborationSessionService(
        ICollaborationSessionRepository repository,
        IOperationTransformService transformService,
        ILogger<CollaborationSessionService> logger)
    {
        _repository = repository;
        _transformService = transformService;
        _logger = logger;
    }
    
    private async Task<CollaborationSession> GetOrCreateSessionWithUserAsync(Guid pageId, string userId)
    {
        try
        {
            var session = await _repository.GetSessionAsync(pageId);
            if (session != null)
            {
                _logger.LogDebug("Found existing session for page {PageId}", pageId);
                
                // Add user to session if not already present
                if (!session.ConnectedUsers.ContainsKey(userId))
                {
                    // TODO: Get user details from user service
                    var newUserPresence = UserPresence.Create(userId, $"User {userId[..8]}", "user@example.com", "#007acc");
                    session = session.WithUser(newUserPresence);
                    await _repository.SaveSessionAsync(session);
                }
                
                return session;
            }
            
            _logger.LogDebug("Creating new session for page {PageId}", pageId);
            
            // TODO: Get initial content from page repository/service
            var initialContent = ""; // This should come from the actual page content
            
            session = CollaborationSession.Create(pageId, initialContent);
            // TODO: Get user details from user service
            var userPresence = UserPresence.Create(userId, $"User {userId[..8]}", "user@example.com", "#007acc");
            session = session.WithUser(userPresence);
            
            await _repository.SaveSessionAsync(session);
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting or creating session for page {PageId}", pageId);
            throw;
        }
    }
    
    public async Task<CollaborationSession> CreateSessionAsync(Guid pageId, string initialContent)
    {
        try
        {
            var session = CollaborationSession.Create(pageId, initialContent);
            await _repository.SaveSessionAsync(session);
            
            _logger.LogDebug("Created new session for page {PageId}", pageId);
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session for page {PageId}", pageId);
            throw;
        }
    }
    
    public async Task<CollaborationSession> GetOrCreateSessionAsync(Guid pageId, string initialContent)
    {
        var session = await _repository.GetSessionAsync(pageId);
        if (session != null)
        {
            return session;
        }
        
        return await CreateSessionAsync(pageId, initialContent);
    }
    
    public async Task<CollaborationSession?> GetSessionAsync(Guid pageId)
    {
        try
        {
            return await _repository.GetSessionAsync(pageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session for page {PageId}", pageId);
            throw;
        }
    }
    
    public async Task<OperationResult> ProcessOperationAsync(Guid pageId, ITextOperation operation)
    {
        try
        {
            _logger.LogDebug("Processing operation {OperationId} for page {PageId}", operation.OperationId, pageId);
            
            var session = await _repository.GetSessionAsync(pageId);
            if (session == null)
            {
                return OperationResult.Failure("Session not found", OperationErrorType.ValidationError, operation);
            }
            
            // Validate the operation against current content
            var validationResult = _transformService.ValidateOperation(operation, session.CurrentContent);
            if (!validationResult.IsValid)
            {
                return OperationResult.Failure($"Operation validation failed: {validationResult.ErrorMessage}", 
                    OperationErrorType.ValidationError, operation);
            }
            
            // Check if operation needs transformation (client is behind)
            var transformedOperation = operation;
            if (operation.ExpectedSequenceNumber < session.CurrentSequenceNumber)
            {
                _logger.LogDebug("Operation needs transformation: expected {Expected}, current {Current}", 
                    operation.ExpectedSequenceNumber, session.CurrentSequenceNumber);
                
                // Get operations that happened after the client's expected sequence
                var laterOperations = session.GetOperationsSince(operation.ExpectedSequenceNumber);
                
                // Transform against each later operation using the available interface method
                try
                {
                    transformedOperation = _transformService.TransformAgainstHistory(transformedOperation, laterOperations);
                }
                catch (Exception ex)
                {
                    return OperationResult.Failure($"Transform failed: {ex.Message}", 
                        OperationErrorType.Conflict, operation);
                }
            }
            
            // Set server sequence number
            var serverSequenceNumber = session.CurrentSequenceNumber + 1;
            transformedOperation = transformedOperation switch
            {
                InsertOperation op => op with { ServerSequenceNumber = serverSequenceNumber },
                DeleteOperation op => op with { ServerSequenceNumber = serverSequenceNumber },
                ReplaceOperation op => op with { ServerSequenceNumber = serverSequenceNumber },
                _ => transformedOperation
            };
            
            // Apply the operation to the session
            var updatedSession = session.WithAppliedOperation(transformedOperation);
            
            // Save the updated session
            await _repository.SaveSessionAsync(updatedSession);
            
            _logger.LogDebug("Successfully processed operation {OperationId}, new sequence: {Sequence}", 
                operation.OperationId, updatedSession.CurrentSequenceNumber);
            
            return OperationResult.CreateSuccess(transformedOperation, operation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing operation {OperationId} for page {PageId}", 
                operation.OperationId, pageId);
            return OperationResult.Failure($"Server error: {ex.Message}", OperationErrorType.ServerError, operation);
        }
    }
    
    public async Task<bool> AddUserToSessionAsync(Guid pageId, UserPresence user)
    {
        try
        {
            var session = await _repository.GetSessionAsync(pageId);
            if (session == null)
            {
                _logger.LogWarning("Attempted to add user {UserId} to non-existent session for page {PageId}", 
                    user.UserId, pageId);
                return false;
            }
            
            var updatedSession = session.WithUser(user);
            await _repository.SaveSessionAsync(updatedSession);
            
            _logger.LogDebug("Added user {UserId} to session for page {PageId}", user.UserId, pageId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user {UserId} to session for page {PageId}", user.UserId, pageId);
            return false;
        }
    }
    
    public async Task<UserPresence> AddUserAsync(Guid pageId, string userId, string displayName, string email)
    {
        try
        {
            var session = await _repository.GetSessionAsync(pageId);
            if (session == null)
            {
                throw new InvalidOperationException($"Session for page {pageId} not found");
            }
            
            // Generate a color for the user (simple hash-based approach)
            var color = $"#{userId.GetHashCode() & 0xFFFFFF:X6}";
            var userPresence = UserPresence.Create(userId, displayName, email, color);
            
            var updatedSession = session.WithUser(userPresence);
            await _repository.SaveSessionAsync(updatedSession);
            
            _logger.LogDebug("Added user {UserId} to session for page {PageId}", userId, pageId);
            return userPresence;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user {UserId} to session for page {PageId}", userId, pageId);
            throw;
        }
    }
    
    public async Task RemoveUserAsync(Guid pageId, string userId)
    {
        try
        {
            var session = await _repository.GetSessionAsync(pageId);
            if (session == null)
            {
                return;
            }
            
            var updatedSession = session.WithoutUser(userId);
            await _repository.SaveSessionAsync(updatedSession);
            
            _logger.LogDebug("Removed user {UserId} from session for page {PageId}", userId, pageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user {UserId} from session for page {PageId}", userId, pageId);
            throw;
        }
    }
    
    public async Task<IEnumerable<ITextOperation>> GetRecentOperationsAsync(Guid pageId, int count = 100)
    {
        try
        {
            var session = await _repository.GetSessionAsync(pageId);
            if (session == null)
            {
                return Enumerable.Empty<ITextOperation>();
            }
            
            return session.RecentOperations.TakeLast(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent operations for page {PageId}", pageId);
            return Enumerable.Empty<ITextOperation>();
        }
    }
    
    public async Task UpdateCursorAsync(Guid pageId, string userId, CursorPosition cursor)
    {
        try
        {
            var session = await _repository.GetSessionAsync(pageId);
            if (session == null || !session.ConnectedUsers.ContainsKey(userId))
            {
                _logger.LogWarning("Cannot update cursor for user {UserId} - session or user not found for page {PageId}", userId, pageId);
                return;
            }
            
            var updatedSession = session.WithUpdatedCursor(userId, cursor);
            await _repository.SaveSessionAsync(updatedSession);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cursor for user {UserId} in page {PageId}", userId, pageId);
            throw;
        }
    }
    
    public async Task<bool> UpdateUserCursorAsync(Guid pageId, string userId, CursorPosition cursor)
    {
        try
        {
            var session = await _repository.GetSessionAsync(pageId);
            if (session == null || !session.ConnectedUsers.ContainsKey(userId))
            {
                return false;
            }
            
            var updatedSession = session.WithUpdatedCursor(userId, cursor);
            await _repository.SaveSessionAsync(updatedSession);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cursor for user {UserId} in page {PageId}", userId, pageId);
            return false;
        }
    }
    
    public async Task<IEnumerable<CollaborationSession>> GetActiveSessionsAsync()
    {
        try
        {
            return await _repository.GetActiveSessionsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active sessions");
            return Enumerable.Empty<CollaborationSession>();
        }
    }
    
    public async Task CleanupInactiveSessionsAsync(TimeSpan inactiveThreshold)
    {
        try
        {
            _logger.LogDebug("Cleaning up inactive sessions older than {Threshold}", inactiveThreshold);
            
            var activeSessions = await _repository.GetActiveSessionsAsync();
            var inactiveSessions = activeSessions.Where(s => s.IsInactive(inactiveThreshold)).ToList();
            
            foreach (var session in inactiveSessions)
            {
                await _repository.RemoveSessionAsync(session.PageId);
                _logger.LogDebug("Removed inactive session for page {PageId}", session.PageId);
            }
            
            _logger.LogDebug("Cleaned up {Count} inactive sessions", inactiveSessions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up inactive sessions");
            throw;
        }
    }
}