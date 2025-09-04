using STWiki.Models.Collaboration.Operations;

namespace STWiki.Models.Collaboration.Events;

/// <summary>
/// Event arguments for operation received events
/// </summary>
public class OperationReceivedEventArgs : EventArgs
{
    public Guid PageId { get; }
    public ITextOperation Operation { get; }
    public string SourceUserId { get; }
    
    public OperationReceivedEventArgs(Guid pageId, ITextOperation operation, string sourceUserId)
    {
        PageId = pageId;
        Operation = operation;
        SourceUserId = sourceUserId;
    }
}

/// <summary>
/// Event arguments for collaboration status changes
/// </summary>
public class CollaborationStatusChangedEventArgs : EventArgs
{
    public CollaborationStatus OldStatus { get; }
    public CollaborationStatus NewStatus { get; }
    public string? Reason { get; }
    
    public CollaborationStatusChangedEventArgs(CollaborationStatus oldStatus, CollaborationStatus newStatus, string? reason = null)
    {
        OldStatus = oldStatus;
        NewStatus = newStatus;
        Reason = reason;
    }
}

/// <summary>
/// Event arguments for user presence changes
/// </summary>
public class UserPresenceChangedEventArgs : EventArgs
{
    public Guid PageId { get; }
    public UserPresence User { get; }
    public UserPresenceChangeType ChangeType { get; }
    
    public UserPresenceChangedEventArgs(Guid pageId, UserPresence user, UserPresenceChangeType changeType)
    {
        PageId = pageId;
        User = user;
        ChangeType = changeType;
    }
}

/// <summary>
/// Event arguments for cursor position changes
/// </summary>
public class CursorPositionChangedEventArgs : EventArgs
{
    public Guid PageId { get; }
    public string UserId { get; }
    public CursorPosition Position { get; }
    
    public CursorPositionChangedEventArgs(Guid pageId, string userId, CursorPosition position)
    {
        PageId = pageId;
        UserId = userId;
        Position = position;
    }
}

/// <summary>
/// Event arguments for save completed events
/// </summary>
public class SaveCompletedEventArgs : EventArgs
{
    public Guid PageId { get; }
    public bool Success { get; }
    public bool IsDraft { get; }
    public string? ErrorMessage { get; }
    
    public SaveCompletedEventArgs(Guid pageId, bool success, bool isDraft, string? errorMessage = null)
    {
        PageId = pageId;
        Success = success;
        IsDraft = isDraft;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// Types of user presence changes
/// </summary>
public enum UserPresenceChangeType
{
    Joined,
    Left,
    Updated
}