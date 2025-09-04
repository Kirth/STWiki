namespace STWiki.Models.Collaboration;

/// <summary>
/// Immutable record representing a user's presence in a collaboration session
/// </summary>
public sealed record UserPresence(
    string UserId,
    string DisplayName,
    string Email,
    string Color,
    DateTimeOffset JoinedAt,
    DateTimeOffset LastSeenAt,
    string? ConnectionId = null,
    CursorPosition? LastCursorPosition = null
)
{
    /// <summary>
    /// Whether the user is currently active (seen within threshold)
    /// </summary>
    public bool IsActive(TimeSpan threshold) => DateTimeOffset.UtcNow - LastSeenAt < threshold;
    
    /// <summary>
    /// Update the last seen timestamp
    /// </summary>
    public UserPresence WithLastSeen(DateTimeOffset? timestamp = null)
    {
        return this with { LastSeenAt = timestamp ?? DateTimeOffset.UtcNow };
    }
    
    /// <summary>
    /// Update the cursor position
    /// </summary>
    public UserPresence WithCursorPosition(CursorPosition position)
    {
        return this with 
        { 
            LastCursorPosition = position,
            LastSeenAt = DateTimeOffset.UtcNow
        };
    }
    
    /// <summary>
    /// Update the connection ID
    /// </summary>
    public UserPresence WithConnection(string connectionId)
    {
        return this with 
        { 
            ConnectionId = connectionId,
            LastSeenAt = DateTimeOffset.UtcNow
        };
    }
    
    /// <summary>
    /// Create a new user presence
    /// </summary>
    public static UserPresence Create(string userId, string displayName, string email, string color)
    {
        var now = DateTimeOffset.UtcNow;
        return new UserPresence(
            UserId: userId,
            DisplayName: displayName,
            Email: email,
            Color: color,
            JoinedAt: now,
            LastSeenAt: now
        );
    }
}