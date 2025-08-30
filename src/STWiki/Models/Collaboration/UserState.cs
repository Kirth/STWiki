namespace STWiki.Models.Collaboration;

public class UserState
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public CursorPosition? Cursor { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public string Color { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    
    public bool IsActive => DateTime.UtcNow.Subtract(LastSeen).TotalMinutes < 5;
}