namespace STWiki.Models.Collaboration.Events;

/// <summary>
/// Event arguments for content change events
/// </summary>
public class ContentChangedEventArgs : EventArgs
{
    public Guid PageId { get; }
    public string NewContent { get; }
    public string? PreviousContent { get; }
    public string ChangeSource { get; }
    public DateTimeOffset Timestamp { get; }
    
    public ContentChangedEventArgs(
        Guid pageId,
        string newContent,
        string? previousContent = null,
        string changeSource = "local",
        DateTimeOffset? timestamp = null)
    {
        PageId = pageId;
        NewContent = newContent;
        PreviousContent = previousContent;
        ChangeSource = changeSource;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
    }
}