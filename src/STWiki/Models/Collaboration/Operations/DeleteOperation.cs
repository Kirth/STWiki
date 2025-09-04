namespace STWiki.Models.Collaboration.Operations;

/// <summary>
/// Immutable record representing a delete text operation
/// </summary>
public sealed record DeleteOperation(
    string OperationId,
    string UserId,
    DateTimeOffset ClientTimestamp,
    int Position,
    int Length,
    string? DeletedContent = null,
    long ServerSequenceNumber = 0,
    long ExpectedSequenceNumber = 0,
    DateTimeOffset? ServerTimestamp = null,
    int RetryCount = 0
) : BaseTextOperation(OperationId, UserId, ClientTimestamp, ServerSequenceNumber, ExpectedSequenceNumber, ServerTimestamp, RetryCount)
{
    public override TextOperationType OperationType => TextOperationType.Delete;
    
    public override string Apply(string content)
    {
        if (!CanApplyTo(content))
            throw new InvalidOperationException($"Cannot apply delete operation at position {Position} with length {Length} to content of length {content.Length}");
            
        return content.Remove(Position, Length);
    }
    
    public override bool CanApplyTo(string content)
    {
        return ValidateCommonProperties() &&
               Position >= 0 &&
               Length > 0 &&
               Position + Length <= content.Length;
    }
    
    public override ITextOperation WithServerInfo(long serverSequenceNumber, DateTimeOffset serverTimestamp)
    {
        return this with 
        { 
            ServerSequenceNumber = serverSequenceNumber, 
            ServerTimestamp = serverTimestamp 
        };
    }
    
    public override ITextOperation WithRetry()
    {
        return this with 
        { 
            RetryCount = RetryCount + 1,
            OperationId = Guid.NewGuid().ToString() // New ID for retry
        };
    }
    
    /// <summary>
    /// Create a new delete operation
    /// </summary>
    public static DeleteOperation Create(string userId, int position, int length, long expectedSequenceNumber = 0)
    {
        return new DeleteOperation(
            OperationId: Guid.NewGuid().ToString(),
            UserId: userId,
            ClientTimestamp: DateTimeOffset.UtcNow,
            Position: position,
            Length: length,
            ExpectedSequenceNumber: expectedSequenceNumber
        );
    }
    
    /// <summary>
    /// Create a delete operation with the actual deleted content (for better conflict resolution)
    /// </summary>
    public static DeleteOperation CreateWithContent(string userId, int position, string deletedContent, long expectedSequenceNumber = 0)
    {
        return new DeleteOperation(
            OperationId: Guid.NewGuid().ToString(),
            UserId: userId,
            ClientTimestamp: DateTimeOffset.UtcNow,
            Position: position,
            Length: deletedContent.Length,
            DeletedContent: deletedContent,
            ExpectedSequenceNumber: expectedSequenceNumber
        );
    }
    
    /// <summary>
    /// Transform this delete operation based on another operation
    /// </summary>
    public DeleteOperation WithTransformedPosition(int newPosition, int newLength)
    {
        return this with 
        { 
            Position = Math.Max(0, newPosition),
            Length = Math.Max(0, newLength)
        };
    }
    
    /// <summary>
    /// Set the deleted content (useful when applying the operation)
    /// </summary>
    public DeleteOperation WithDeletedContent(string deletedContent)
    {
        return this with { DeletedContent = deletedContent };
    }
}