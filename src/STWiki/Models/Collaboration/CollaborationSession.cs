using STWiki.Models.Collaboration.Operations;

namespace STWiki.Models.Collaboration;

/// <summary>
/// Immutable record representing a collaboration session for a page
/// </summary>
public sealed record CollaborationSession(
    Guid PageId,
    string CurrentContent,
    long CurrentSequenceNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    IReadOnlyDictionary<string, UserPresence> ConnectedUsers,
    IReadOnlyList<ITextOperation> RecentOperations
)
{
    /// <summary>
    /// Maximum number of operations to keep in history
    /// </summary>
    public const int MaxOperationHistory = 1000;
    
    /// <summary>
    /// Whether the session is active (has connected users)
    /// </summary>
    public bool IsActive => ConnectedUsers.Count > 0;
    
    /// <summary>
    /// Number of currently connected users
    /// </summary>
    public int UserCount => ConnectedUsers.Count;
    
    /// <summary>
    /// Whether the session is inactive for cleanup
    /// </summary>
    public bool IsInactive(TimeSpan threshold) => DateTimeOffset.UtcNow - LastActivityAt > threshold;
    
    /// <summary>
    /// Create a new collaboration session
    /// </summary>
    public static CollaborationSession Create(Guid pageId, string initialContent)
    {
        var now = DateTimeOffset.UtcNow;
        return new CollaborationSession(
            PageId: pageId,
            CurrentContent: initialContent,
            CurrentSequenceNumber: 0,
            CreatedAt: now,
            LastActivityAt: now,
            ConnectedUsers: new Dictionary<string, UserPresence>(),
            RecentOperations: new List<ITextOperation>()
        );
    }
    
    /// <summary>
    /// Add a user to the session
    /// </summary>
    public CollaborationSession WithUser(UserPresence user)
    {
        var updatedUsers = new Dictionary<string, UserPresence>(ConnectedUsers)
        {
            [user.UserId] = user
        };
        
        return this with 
        { 
            ConnectedUsers = updatedUsers,
            LastActivityAt = DateTimeOffset.UtcNow
        };
    }
    
    /// <summary>
    /// Remove a user from the session
    /// </summary>
    public CollaborationSession WithoutUser(string userId)
    {
        var updatedUsers = new Dictionary<string, UserPresence>(ConnectedUsers);
        updatedUsers.Remove(userId);
        
        return this with 
        { 
            ConnectedUsers = updatedUsers,
            LastActivityAt = DateTimeOffset.UtcNow
        };
    }
    
    /// <summary>
    /// Apply an operation to the session
    /// </summary>
    public CollaborationSession WithAppliedOperation(ITextOperation operation)
    {
        // Apply the operation to get new content
        var newContent = operation.Apply(CurrentContent);
        
        // Add to operation history (keeping only recent ones)
        var updatedOperations = new List<ITextOperation>(RecentOperations) { operation };
        if (updatedOperations.Count > MaxOperationHistory)
        {
            updatedOperations.RemoveRange(0, updatedOperations.Count - MaxOperationHistory);
        }
        
        return this with 
        {
            CurrentContent = newContent,
            CurrentSequenceNumber = CurrentSequenceNumber + 1,
            LastActivityAt = DateTimeOffset.UtcNow,
            RecentOperations = updatedOperations
        };
    }
    
    /// <summary>
    /// Update cursor position for a user
    /// </summary>
    public CollaborationSession WithUpdatedCursor(string userId, CursorPosition cursor)
    {
        if (!ConnectedUsers.TryGetValue(userId, out var user))
            return this;
            
        var updatedUser = user.WithCursorPosition(cursor);
        return WithUser(updatedUser);
    }
    
    /// <summary>
    /// Get operations since a specific sequence number
    /// </summary>
    public IEnumerable<ITextOperation> GetOperationsSince(long sequenceNumber)
    {
        return RecentOperations
            .Where(op => op.ServerSequenceNumber > sequenceNumber)
            .OrderBy(op => op.ServerSequenceNumber);
    }
    
    /// <summary>
    /// Compute content hash for state verification
    /// </summary>
    public string ComputeContentHash()
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(CurrentContent ?? "");
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}