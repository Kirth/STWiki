namespace STWiki.Models.Collaboration.Operations;

/// <summary>
/// Immutable record representing an insert text operation
/// </summary>
public sealed record InsertOperation(
    string OperationId,
    string UserId,
    DateTimeOffset ClientTimestamp,
    int Position,
    string Content,
    long ServerSequenceNumber = 0,
    long ExpectedSequenceNumber = 0,
    DateTimeOffset? ServerTimestamp = null,
    int RetryCount = 0
) : BaseTextOperation(OperationId, UserId, ClientTimestamp, ServerSequenceNumber, ExpectedSequenceNumber, ServerTimestamp, RetryCount)
{
    public override TextOperationType OperationType => TextOperationType.Insert;
    
    public override string Apply(string content)
    {
        if (!CanApplyTo(content))
            throw new InvalidOperationException($"Cannot apply insert operation at position {Position} to content of length {content.Length}");
            
        return content.Insert(Position, Content);
    }
    
    public override bool CanApplyTo(string content)
    {
        return ValidateCommonProperties() &&
               !string.IsNullOrEmpty(Content) &&
               Position >= 0 &&
               Position <= content.Length;
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
    /// Create a new insert operation
    /// </summary>
    public static InsertOperation Create(string userId, int position, string content, long expectedSequenceNumber = 0)
    {
        return new InsertOperation(
            OperationId: Guid.NewGuid().ToString(),
            UserId: userId,
            ClientTimestamp: DateTimeOffset.UtcNow,
            Position: position,
            Content: content,
            ExpectedSequenceNumber: expectedSequenceNumber
        );
    }
    
    /// <summary>
    /// Transform this insert operation's position based on another operation
    /// </summary>
    public InsertOperation WithTransformedPosition(int newPosition)
    {
        return this with { Position = Math.Max(0, newPosition) };
    }
}