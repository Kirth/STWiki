namespace STWiki.Models.Collaboration.Operations;

/// <summary>
/// Base interface for all text operations in collaborative editing
/// </summary>
public interface ITextOperation
{
    /// <summary>
    /// Unique identifier for this operation
    /// </summary>
    string OperationId { get; }
    
    /// <summary>
    /// User who created this operation
    /// </summary>
    string UserId { get; }
    
    /// <summary>
    /// When this operation was created (client timestamp)
    /// </summary>
    DateTimeOffset ClientTimestamp { get; }
    
    /// <summary>
    /// Server sequence number (assigned when processed)
    /// </summary>
    long ServerSequenceNumber { get; }
    
    /// <summary>
    /// Expected sequence number when operation was created (for conflict detection)
    /// </summary>
    long ExpectedSequenceNumber { get; }
    
    /// <summary>
    /// When this operation was processed by the server
    /// </summary>
    DateTimeOffset? ServerTimestamp { get; }
    
    /// <summary>
    /// Number of retry attempts for this operation
    /// </summary>
    int RetryCount { get; }
    
    /// <summary>
    /// Apply this operation to a text content string
    /// </summary>
    string Apply(string content);
    
    /// <summary>
    /// Validate that this operation can be applied to the given content
    /// </summary>
    bool CanApplyTo(string content);
    
    /// <summary>
    /// Get the operation type for pattern matching
    /// </summary>
    TextOperationType OperationType { get; }
}