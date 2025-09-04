namespace STWiki.Models.Collaboration.Operations;

/// <summary>
/// Immutable record representing a replace text operation (selection-based)
/// </summary>
public sealed record ReplaceOperation(
    string OperationId,
    string UserId,
    DateTimeOffset ClientTimestamp,
    int SelectionStart,
    int SelectionEnd,
    string NewContent,
    string? OriginalContent = null,
    long ServerSequenceNumber = 0,
    long ExpectedSequenceNumber = 0,
    DateTimeOffset? ServerTimestamp = null,
    int RetryCount = 0
) : BaseTextOperation(OperationId, UserId, ClientTimestamp, ServerSequenceNumber, ExpectedSequenceNumber, ServerTimestamp, RetryCount)
{
    public override TextOperationType OperationType => TextOperationType.Replace;
    
    /// <summary>
    /// The effective position for this operation (start of selection)
    /// </summary>
    public int Position => SelectionStart;
    
    /// <summary>
    /// The length of the selection being replaced
    /// </summary>
    public int SelectionLength => SelectionEnd - SelectionStart;
    
    /// <summary>
    /// Whether this is effectively an insert operation (empty selection)
    /// </summary>
    public bool IsInsert => SelectionStart == SelectionEnd;
    
    /// <summary>
    /// Whether this is effectively a delete operation (empty new content)
    /// </summary>
    public bool IsDelete => string.IsNullOrEmpty(NewContent) && SelectionLength > 0;
    
    public override string Apply(string content)
    {
        if (!CanApplyTo(content))
            throw new InvalidOperationException($"Cannot apply replace operation at selection {SelectionStart}-{SelectionEnd} to content of length {content.Length}");
            
        return content.Remove(SelectionStart, SelectionLength).Insert(SelectionStart, NewContent);
    }
    
    public override bool CanApplyTo(string content)
    {
        return ValidateCommonProperties() &&
               NewContent != null &&
               SelectionStart >= 0 &&
               SelectionEnd >= SelectionStart &&
               SelectionEnd <= content.Length;
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
    /// Create a new replace operation
    /// </summary>
    public static ReplaceOperation Create(string userId, int selectionStart, int selectionEnd, string newContent, long expectedSequenceNumber = 0)
    {
        return new ReplaceOperation(
            OperationId: Guid.NewGuid().ToString(),
            UserId: userId,
            ClientTimestamp: DateTimeOffset.UtcNow,
            SelectionStart: selectionStart,
            SelectionEnd: selectionEnd,
            NewContent: newContent,
            ExpectedSequenceNumber: expectedSequenceNumber
        );
    }
    
    /// <summary>
    /// Create a replace operation with the original content (for better conflict resolution)
    /// </summary>
    public static ReplaceOperation CreateWithOriginal(string userId, int selectionStart, int selectionEnd, string newContent, string originalContent, long expectedSequenceNumber = 0)
    {
        return new ReplaceOperation(
            OperationId: Guid.NewGuid().ToString(),
            UserId: userId,
            ClientTimestamp: DateTimeOffset.UtcNow,
            SelectionStart: selectionStart,
            SelectionEnd: selectionEnd,
            NewContent: newContent,
            OriginalContent: originalContent,
            ExpectedSequenceNumber: expectedSequenceNumber
        );
    }
    
    /// <summary>
    /// Transform this replace operation's selection based on another operation
    /// </summary>
    public ReplaceOperation WithTransformedSelection(int newStart, int newEnd)
    {
        return this with 
        { 
            SelectionStart = Math.Max(0, newStart),
            SelectionEnd = Math.Max(newStart, newEnd)
        };
    }
    
    /// <summary>
    /// Convert this replace operation to an insert operation (when selection becomes empty)
    /// </summary>
    public InsertOperation ToInsertOperation()
    {
        return new InsertOperation(
            OperationId: OperationId,
            UserId: UserId,
            ClientTimestamp: ClientTimestamp,
            Position: SelectionStart,
            Content: NewContent,
            ServerSequenceNumber: ServerSequenceNumber,
            ExpectedSequenceNumber: ExpectedSequenceNumber,
            ServerTimestamp: ServerTimestamp,
            RetryCount: RetryCount
        );
    }
    
    /// <summary>
    /// Convert this replace operation to a delete operation (when new content is empty)
    /// </summary>
    public DeleteOperation ToDeleteOperation()
    {
        return new DeleteOperation(
            OperationId: OperationId,
            UserId: UserId,
            ClientTimestamp: ClientTimestamp,
            Position: SelectionStart,
            Length: SelectionLength,
            DeletedContent: OriginalContent,
            ServerSequenceNumber: ServerSequenceNumber,
            ExpectedSequenceNumber: ExpectedSequenceNumber,
            ServerTimestamp: ServerTimestamp,
            RetryCount: RetryCount
        );
    }
    
    /// <summary>
    /// Set the original content (useful when applying the operation)
    /// </summary>
    public ReplaceOperation WithOriginalContent(string originalContent)
    {
        return this with { OriginalContent = originalContent };
    }
}