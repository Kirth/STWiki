namespace STWiki.Models.Collaboration;

public class TextOperation
{
    public enum OperationType { Insert, Delete, Retain, Replace }
    
    public OperationType OpType { get; set; }
    public int Position { get; set; }
    public string? Content { get; set; }
    public int Length { get; set; }
    public string UserId { get; set; } = string.Empty;
    public long Timestamp { get; set; }
    public string OperationId { get; set; } = Guid.NewGuid().ToString();
    public int SelectionStart { get; set; } // For replace operations
    public int SelectionEnd { get; set; }   // For replace operations
    
    // Server-assigned sequence number for strict ordering (prevents race conditions)
    public long ServerSequenceNumber { get; set; } = 0;
    
    // Phase 1: Enhanced State Reconciliation - Additional tracking properties
    
    /// <summary>
    /// Expected server sequence number when operation was created
    /// Used for stale state detection
    /// </summary>
    public long ExpectedSequenceNumber { get; set; }
    
    /// <summary>
    /// When this operation was processed by the server
    /// </summary>
    public DateTime? ProcessedAt { get; set; }
    
    /// <summary>
    /// Number of retry attempts for this operation
    /// </summary>
    public int RetryCount { get; set; }
    
    public static TextOperation Insert(int position, string content, string userId)
    {
        return new TextOperation
        {
            OpType = OperationType.Insert,
            Position = position,
            Content = content,
            Length = content.Length,
            UserId = userId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
    
    public static TextOperation Delete(int position, int length, string userId)
    {
        return new TextOperation
        {
            OpType = OperationType.Delete,
            Position = position,
            Length = length,
            UserId = userId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
    
    public static TextOperation Retain(int length, string userId)
    {
        return new TextOperation
        {
            OpType = OperationType.Retain,
            Position = 0,
            Length = length,
            UserId = userId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
    
    public static TextOperation Replace(int selectionStart, int selectionEnd, string newContent, string userId)
    {
        // Validate and log potentially problematic content
        if (!string.IsNullOrEmpty(newContent) && newContent.Contains("]]") && newContent.Contains("media:"))
        {
            Console.WriteLine($"🎯 Creating Replace operation with media template:");
            Console.WriteLine($"  Content: '{newContent}' (length: {newContent.Length})");
            Console.WriteLine($"  Range: {selectionStart}-{selectionEnd}");
        }
        
        return new TextOperation
        {
            OpType = OperationType.Replace,
            Position = selectionStart,
            Content = newContent,
            Length = newContent.Length,
            SelectionStart = selectionStart,
            SelectionEnd = selectionEnd,
            UserId = userId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
}