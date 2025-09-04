namespace STWiki.Models.Collaboration.Operations;

/// <summary>
/// Abstract base record for all text operations providing common properties
/// </summary>
public abstract record BaseTextOperation(
    string OperationId,
    string UserId,
    DateTimeOffset ClientTimestamp,
    long ServerSequenceNumber = 0,
    long ExpectedSequenceNumber = 0,
    DateTimeOffset? ServerTimestamp = null,
    int RetryCount = 0
) : ITextOperation
{
    /// <summary>
    /// Apply this operation to a text content string
    /// </summary>
    public abstract string Apply(string content);
    
    /// <summary>
    /// Validate that this operation can be applied to the given content
    /// </summary>
    public abstract bool CanApplyTo(string content);
    
    /// <summary>
    /// Get the operation type for pattern matching
    /// </summary>
    public abstract TextOperationType OperationType { get; }
    
    /// <summary>
    /// Create a copy of this operation with updated server information
    /// </summary>
    public abstract ITextOperation WithServerInfo(long serverSequenceNumber, DateTimeOffset serverTimestamp);
    
    /// <summary>
    /// Create a copy of this operation with incremented retry count
    /// </summary>
    public abstract ITextOperation WithRetry();
    
    /// <summary>
    /// Validate common operation properties
    /// </summary>
    protected virtual bool ValidateCommonProperties()
    {
        return !string.IsNullOrEmpty(OperationId) &&
               !string.IsNullOrEmpty(UserId) &&
               ClientTimestamp != default &&
               RetryCount >= 0;
    }
}