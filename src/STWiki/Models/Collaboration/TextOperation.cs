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