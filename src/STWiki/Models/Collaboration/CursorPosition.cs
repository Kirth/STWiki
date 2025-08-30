namespace STWiki.Models.Collaboration;

public class CursorPosition
{
    public string UserId { get; set; } = string.Empty;
    public int Start { get; set; }
    public int End { get; set; }
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    
    public bool HasSelection => Start != End;
    public int SelectionLength => Math.Abs(End - Start);
}