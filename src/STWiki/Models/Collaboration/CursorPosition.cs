namespace STWiki.Models.Collaboration;

/// <summary>
/// Class representing a cursor or selection position
/// </summary>
public sealed class CursorPosition
{
    public string UserId { get; set; } = string.Empty;
    public int Start { get; set; }
    public int End { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    
    public CursorPosition()
    {
        Timestamp = DateTimeOffset.UtcNow;
    }
    
    public CursorPosition(string userId, int start, int end, DateTimeOffset timestamp)
    {
        UserId = userId;
        Start = start;
        End = end;
        Timestamp = timestamp;
    }
    /// <summary>
    /// Whether this represents a selection (not just a cursor)
    /// </summary>
    public bool HasSelection => Start != End;
    
    /// <summary>
    /// The length of the selection (0 for cursor)
    /// </summary>
    public int SelectionLength => Math.Abs(End - Start);
    
    /// <summary>
    /// The normalized start position (min of start/end)
    /// </summary>
    public int NormalizedStart => Math.Min(Start, End);
    
    /// <summary>
    /// The normalized end position (max of start/end)
    /// </summary>
    public int NormalizedEnd => Math.Max(Start, End);
    
    /// <summary>
    /// Create a cursor position (no selection)
    /// </summary>
    public static CursorPosition CreateCursor(string userId, int position)
    {
        return new CursorPosition(userId, position, position, DateTimeOffset.UtcNow);
    }
    
    /// <summary>
    /// Create a selection position
    /// </summary>
    public static CursorPosition CreateSelection(string userId, int start, int end)
    {
        return new CursorPosition(userId, start, end, DateTimeOffset.UtcNow);
    }
    
    /// <summary>
    /// Check if this cursor position is within the given bounds
    /// </summary>
    public bool IsValidFor(int contentLength)
    {
        return Start >= 0 && End >= 0 && 
               Start <= contentLength && End <= contentLength;
    }
}