using STWiki.Models.Collaboration.Operations;

namespace STWiki.Models.Collaboration.Events;

/// <summary>
/// Event arguments for text change events from the editor
/// </summary>
public class TextChangeEventArgs : EventArgs
{
    public Guid PageId { get; }
    public string UserId { get; }
    public string NewContent { get; }
    public int Position { get; }
    public string ChangeType { get; }
    public string ChangedText { get; }
    
    // For replace operations
    public int SelectionStart { get; }
    public int SelectionEnd { get; }
    
    public TextChangeEventArgs(
        Guid pageId,
        string userId,
        string newContent,
        int position,
        string changeType,
        string changedText,
        int selectionStart = -1,
        int selectionEnd = -1)
    {
        PageId = pageId;
        UserId = userId;
        NewContent = newContent;
        Position = position;
        ChangeType = changeType;
        ChangedText = changedText;
        SelectionStart = selectionStart;
        SelectionEnd = selectionEnd;
    }
    
    /// <summary>
    /// Convert this event to the appropriate text operation
    /// </summary>
    public ITextOperation ToOperation(long expectedSequenceNumber = 0)
    {
        return ChangeType.ToLower() switch
        {
            "insert" => InsertOperation.Create(UserId, Position, ChangedText, expectedSequenceNumber),
            "delete" => DeleteOperation.Create(UserId, Position, ChangedText.Length, expectedSequenceNumber),
            "replace" when SelectionStart >= 0 && SelectionEnd >= SelectionStart => 
                ReplaceOperation.Create(UserId, SelectionStart, SelectionEnd, ChangedText, expectedSequenceNumber),
            _ => throw new InvalidOperationException($"Unknown change type: {ChangeType}")
        };
    }
}